// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Vonvert.Engine.DspEngine;
using Vonvert.Engine.DspEngine.Pitch;

namespace Vonvert.Engine.AudioEngine;

/*
 * Real-time voice engine for Vonvert.
 *
 * Pipeline:
 *   Thread 1 (TimeCritical): WASAPI capture → format converter → ring buffer
 *   Thread 2 (Highest):      ring buffer → DSP chain / pipeline → output
 *   Output:                  WASAPI shared / exclusive → speaker / VB-Cable
 */
public sealed class VoiceEngine : IDisposable
{
    public EngineSettings Settings    { get; } = new();
    public EngineStats    Stats       => _pipeline.Metrics;
    public DspEngine.DSPChain   Effects     => _pipeline.Chain;
    public IAudioProcessor Pipeline   => _pipeline;

    public VoiceEngine()
    {
        _pipeline = new NullAudioProcessor();
        AppLog.Information("VoiceEngine created with pipeline: {Pipeline}", _pipeline.GetType().Name);
    }

    public event Action<EngineStatus>? StatusChanged;
    public event Action<string>?       ErrorRaised;
    public event Action?               DeviceLost;

    public EngineStatus State { get; private set; } = EngineStatus.Idle;

    // --- Timing ---
    // Single source of truth so WAV-header rate always matches the
    // actual DSP chain rate (OSS engine was 44100 vs recorders/BGM 48000).
    private const int ENGINE_RATE = AudioConstants.EngineRate;
    private const int RING_CAPACITY  = ENGINE_RATE * 2;   // 2 s headroom
    private const int BLOCK_SAMPLES  = 4096;

    // Latency presets
    private const int LL_CAPTURE_MS  = 5;
    private const int LL_RENDER_MS   = 10;
    private const int NL_RENDER_MS   = 20;

    /// <summary>Effective latency window applied to capture + DSP block.</summary>
    internal int EffectiveLatencyMs => Settings.LowLatency ? LL_CAPTURE_MS : Settings.LatencyMs;

    // --- Audio graph ---
    private WasapiCapture?        _capture;
    private WasapiOut?            _render;
    private BufferedWaveProvider? _feed;
    private WasapiOut?            _monitor;
    private BufferedWaveProvider? _monitorFeed;
    private FormatConverter?      _conv;

    // --- Threading ---
    private readonly SampleQueue _rxRing  = new(RING_CAPACITY);
    private readonly float[]    _block   = new float[BLOCK_SAMPLES];
    private readonly byte[]     _outBuf  = new byte[BLOCK_SAMPLES * 4];
    private Thread?             _worker;
    private volatile bool       _running;
    private volatile bool       _halt;
    private CancellationTokenSource? _dspCts;
    private volatile float      _gain    = 1.0f;

    // --- Mutual exclusion ---
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _restartQueue = new(1, 1);
    private volatile bool _disposed;

    // --- Stats ---
    private readonly Stopwatch _sw = new();
    private int _underruns = 0;

    // --- Pipeline ---
    private readonly IAudioProcessor _pipeline;

    // --- WetOnly scratch ---
    private float[] _wetDryScratch = Array.Empty<float>();

    // --- Push-to-Talk ---
    private volatile bool _muted;

    // --- Public API ---

    public void Start(AudioDeviceHub devices)
    {
        if (_disposed) return;
        // Acquire lock to prevent Start/Stop race condition
        if (!_gate.Wait(0)) return; // already starting/stopping
        try
        {
            if (_disposed) return;
            if (State == EngineStatus.Active) return;
            _halt = false;
            AppLog.Information("VoiceEngine.Start: initializing graph...");
            InitGraph(devices);
            AppLog.Debug("Start: graph ok");

            // Guard: if Stop() was called during InitGraph (e.g. from shutdown
            // racing with background engine start), bail out before touching objects.
            if (_halt)
            {
                AppLog.Warning("VoiceEngine.Start: halt requested during graph init — aborting");
                Stop();
                return;
            }

            _running = true;

            // Second guard: Stop() may have run between InitialiseGraph and setting _running.
            if (_halt)
            {
                AppLog.Warning("VoiceEngine.Start: halt requested after graph init — cleaning up");
                _running = false;
                Stop();
                return;
            }

            SpawnWorker();
            AppLog.Debug("Start: dsp thread started");
            _capture!.StartRecording();
            AppLog.Debug("Start: capture active");
            _render?.Play();
            AppLog.Debug("Start: render active");
            _monitor?.Play();
            AppLog.Debug("Start: loopback play");
            _pipeline.OnStart();
            SetState(EngineStatus.Active);
            AppLog.Information("VoiceEngine.Start: engine running (rate={Rate}, latencyMs={LatencyMs})",
                ENGINE_RATE, EffectiveLatencyMs);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "VoiceEngine.Start failed");
            AppLog.Flush(); // ensure error hits disk before potential crash
            SetState(EngineStatus.Faulted);
            Stats.Status = ex.Message;
            ErrorRaised?.Invoke(ex.Message);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Stop()
    {
        if (_disposed) return;
        // Acquire lock to prevent Start/Stop race condition
        if (!_gate.Wait(0)) return;
        try
        {
            Teardown();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Internal teardown — caller must hold <see cref="_gate"/>.</summary>
    private void Teardown()
    {
        AppLog.Information("VoiceEngine.Stop: stopping engine...");
        _halt = true;
        _running = false;

        // Wait for worker thread with progressive timeout
        _dspCts?.Cancel();
        if (_worker != null && _worker.IsAlive)
        {
            if (!_worker.Join(1500))
                AppLog.Warning("VoiceEngine: worker thread did not exit within 1.5s");
        }

        // Release WASAPI objects with timeout protection
        try
        {
            var stopTask = Task.Run(() =>
            {
                try { _capture?.StopRecording(); } catch { /* WASAPI capture stop is best-effort */ }
                try { _render?.Stop(); } catch { /* WASAPI render stop is best-effort */ }
                try { _monitor?.Stop(); } catch { /* WASAPI monitor stop is best-effort */ }
            });
            if (!stopTask.Wait(TimeSpan.FromMilliseconds(500)))
                AppLog.Warning("VoiceEngine: WASAPI stop timed out (500ms)");
        }
        catch { /* WASAPI stop timeout recovery is best-effort */ }

        // Dispose WASAPI handles
        var disposeTask = Task.Run(() =>
        {
            try { _capture?.Dispose(); } catch { /* WASAPI capture dispose is best-effort */ }
            try { _render?.Dispose(); } catch { /* WASAPI render dispose is best-effort */ }
            try { _monitor?.Dispose(); } catch { /* WASAPI monitor dispose is best-effort */ }
        });
        if (!disposeTask.Wait(TimeSpan.FromSeconds(2)))
            AppLog.Warning("VoiceEngine: WASAPI dispose timed out (2s)");
        _capture = null; _render = null; _monitor = null;

        _rxRing.Purge();
        _pipeline.OnStop();
        SetState(EngineStatus.Idle);
        AppLog.Information("VoiceEngine.Stop: engine stopped");
    }

    /// <summary>Restart the audio graph synchronously (blocks the caller — prefer <see cref="RestartAsync"/>).</summary>
    public void Restart(AudioDeviceHub devices) { Stop(); Start(devices); }

    /// <summary>
    /// Restart the audio graph without blocking the caller (UI) thread.
    /// Stop + Start run on a background thread; concurrent restart requests
    /// are serialised through <see cref="_restartQueue"/> so a device change
    /// that arrives mid-restart is applied right after the current one
    /// finishes (no dropped restarts, no UI freeze).
    /// </summary>
    public async Task RestartAsync(AudioDeviceHub devices)
    {
        await _restartQueue.WaitAsync().ConfigureAwait(false);
        try
        {
            await Task.Run(() => Restart(devices)).ConfigureAwait(false);
        }
        finally
        {
            _restartQueue.Release();
        }
    }

    public void SetGain(float g)              => _gain = Math.Clamp(g, 0f, 4f);
    /// <summary>Current output gain (test accessor for restart/state assertions).</summary>
    internal float Gain                       => _gain;
    /// <summary>Backward-compatible: maps bool to CompareMode (true=Dry, false=Normal).</summary>
    public void SetBypass(bool on)            => Settings.Compare = on ? CompareMode.Dry : CompareMode.Normal;
    public void SetCompareMode(CompareMode mode) => Settings.Compare = mode;
    public void SetMute(bool m)               => _muted = m;
    public bool Muted                         => _muted;

    // --- Graph init ---

    private void InitGraph(AudioDeviceHub devices)
    {
        // Capture endpoint
        var inDevice = string.IsNullOrEmpty(Settings.InputDeviceId)
            ? devices.DefaultInputDevice()
            : devices.Resolve(Settings.InputDeviceId);
        if (inDevice == null) throw new Exception("No input device available.");

        AppLog.Information("Setting up capture on device: {Device}", inDevice.FriendlyName);
        _conv = new FormatConverter();

        // WasapiCapture may throw COMException when the device is in exclusive
        // mode by another process, or when the requested latency is unsupported.
        // Fallback: retry with default latency, then with 0 (NAudio default).
        try
        {
            _capture = new WasapiCapture(inDevice, false, EffectiveLatencyMs);
        }
        catch (Exception capEx)
        {
            AppLog.Warning(capEx,
                "WasapiCapture failed with latency {Latency}ms on {Device}, retrying with default",
                EffectiveLatencyMs, inDevice.FriendlyName);
            try
            {
                _capture = new WasapiCapture(inDevice, false, 0);
            }
            catch (Exception retryEx)
            {
                AppLog.Error(retryEx,
                    "WasapiCapture retry also failed on {Device}", inDevice.FriendlyName);
                throw;
            }
        }
        _capture.DataAvailable   += OnCapture;
        _capture.RecordingStopped += OnCaptureStopped;

        // Output endpoint (prefer VB-Cable)
        var outDevice = devices.FindVBCable()
            ?? (string.IsNullOrEmpty(Settings.OutputDeviceId)
                ? devices.DefaultOutputDevice()
                : devices.Resolve(Settings.OutputDeviceId));
        if (outDevice == null) throw new Exception("No output device available.");

        AppLog.Information("Setting up output on device: {Device}", outDevice.FriendlyName);

        var fmt = WaveFormat.CreateIeeeFloatWaveFormat(ENGINE_RATE, 1);
        _feed  = new BufferedWaveProvider(fmt)
        {
            BufferDuration          = TimeSpan.FromSeconds(1),
            DiscardOnBufferOverflow = true
        };

        if (Settings.LowLatency)
        {
            // Exclusive WASAPI at minimum buffer for lowest latency
            try
            {
                _render = new WasapiOut(outDevice, AudioClientShareMode.Exclusive, true, LL_RENDER_MS);
            }
            catch (Exception llEx)
            {
                AppLog.Warning(llEx,
                    "Low-latency: exclusive WASAPI unavailable on {Device}, falling back to shared {Buf}ms",
                    outDevice.FriendlyName, LL_RENDER_MS);
                _render = new WasapiOut(outDevice, AudioClientShareMode.Shared, true, LL_RENDER_MS);
            }
            _render.Init(_feed);
        }
        else
        {
            _render = new WasapiOut(outDevice, AudioClientShareMode.Shared, true, NL_RENDER_MS);
            _render.Init(_feed);
        }

        // HearMyself loopback
        if (Settings.HearMyself)
        {
            var defOut = devices.DefaultOutputDevice();
            if (defOut != null && defOut.ID != outDevice.ID)
            {
                try
                {
                    _monitorFeed = new BufferedWaveProvider(fmt)
                    {
                        BufferDuration          = TimeSpan.FromSeconds(1),
                        DiscardOnBufferOverflow = true
                    };
                    _monitor = new WasapiOut(defOut, AudioClientShareMode.Shared, true,
                        Settings.LowLatency ? LL_RENDER_MS : NL_RENDER_MS);
                    _monitor.Init(_monitorFeed);
                }
                catch { _monitor = null; _monitorFeed = null; }
            }
        }
        else
        {
            _monitor = null;
            _monitorFeed = null;
        }
    }

    // --- Capture callback ---

    private void OnCapture(object? _, WaveInEventArgs e)
    {
        try
        {
            if (!_running || e.BytesRecorded == 0) return;
            _sw.Restart();

            var samples = _conv!.Convert(e.Buffer, e.BytesRecorded, _capture!.WaveFormat);
            float g = _gain;
            float lvl  = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= g;
                lvl = Math.Max(lvl, MathF.Abs(samples[i]));
            }
            Stats.InputLevel = lvl;

            if (_rxRing.Enqueue(samples) < samples.Length)
                Interlocked.Increment(ref _underruns);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex, "OnCapture callback failed");
        }
    }

    private void OnCaptureStopped(object? _, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            AppLog.Error(e.Exception, "Capture stopped with exception");
            ErrorRaised?.Invoke(e.Exception.Message);
            DeviceLost?.Invoke();
        }
    }

    // --- Worker thread ---
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod", SetLastError = true)]
    private static extern uint TimeBeginPeriod(uint uPeriod);
    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod", SetLastError = true)]
    private static extern uint TimeEndPeriod(uint uPeriod);

    private void SpawnWorker()
    {
        _dspCts = new CancellationTokenSource();
        _worker = new Thread(() =>
        {
            TimeBeginPeriod(1);
            try { ProcessLoop(); }
            finally { TimeEndPeriod(1); }
        })
        {
            Name         = "Vonvert.DSP",
            Priority     = ThreadPriority.Highest,
            IsBackground = true
        };
        _worker.Start();
    }

    private void ProcessLoop()
    {
        int chunkSize = ENGINE_RATE * EffectiveLatencyMs / 1000;

        while (_running)
        {
            try
            {
                if (_rxRing.Count < chunkSize) { Thread.Sleep(1); continue; }
                if (!_running) break;

                var chunk = _block.AsSpan(0, Math.Min(chunkSize, BLOCK_SAMPLES));
                int read  = _rxRing.Dequeue(chunk);
                if (read == 0) continue;

                var work = chunk[..read];

                // Push-to-Talk: silence output when muted
                if (_muted) { work.Clear(); }

                // A/B Compare: three-mode processing
                switch (Settings.Compare)
                {
                    case CompareMode.Normal:
                        _pipeline.PreMix(work, read);
                        _pipeline.Process(work);
                        _pipeline.PostAnalyze(work, read);
                        break;

                    case CompareMode.Dry:
                        // Bypass all DSP — output original dry signal
                        break;

                    case CompareMode.WetOnly:
                        if (_wetDryScratch.Length < read)
                            _wetDryScratch = new float[read * 2];
                        work.Slice(0, read).CopyTo(_wetDryScratch);
                        _pipeline.PreMix(work, read);
                        _pipeline.Process(work);
                        _pipeline.PostAnalyze(work, read);
                        for (int i = 0; i < read; i++)
                            work[i] -= _wetDryScratch[i];
                        break;
                }

                // Track output level
                float lvl = 0f;
                foreach (var s in work) lvl = Math.Max(lvl, MathF.Abs(s));
                Stats.OutputLevel  = lvl;
                Stats.LatencyMs    = _sw.Elapsed.TotalMilliseconds;
                Stats.Underruns    = _underruns;

                // Float span → byte array → BufferedWaveProvider
                int outLen = work.Length * 4;
                MemoryMarshal.AsBytes(work).CopyTo(_outBuf);
                _feed?.AddSamples(_outBuf, 0, outLen);
                _monitorFeed?.AddSamples(_outBuf, 0, outLen);
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "DSP loop iteration failed");
                // Don't break — a single failure should not kill the audio thread.
                // Sleep briefly to avoid tight-loop logging if the error is persistent.
                Thread.Sleep(1);
            }
        }
    }

    // --- Helpers -------------------------------------------------------------

    private void SetState(EngineStatus s)
    {
        if (State != s)
            AppLog.Information("VoiceEngine status: {Old} → {New}", State, s);
        State = s;
        StatusChanged?.Invoke(s);
        Stats.Status = s.ToString();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Acquire the lock to ensure no Start/Stop is in-flight before teardown.
        // This prevents ObjectDisposedException on _gate when Stop()
        // is called concurrently or after Dispose().
        _gate.Wait();
        try
        {
            if (State == EngineStatus.Active)
                Teardown();
            else
            {
                _halt = true;
                _running = false;
                _dspCts?.Cancel();
                if (_worker != null && _worker.IsAlive)
                {
                    if (!_worker.Join(1500))
                        AppLog.Warning("VoiceEngine: worker did not exit within 1.5s in Dispose");
                }
            }
        }
        finally
        {
            _gate.Release();
        }

        _pipeline.Dispose();

        // Release WASAPI handles with timeout
        var disposeTask = Task.Run(() =>
        {
            try { _capture?.Dispose(); } catch { /* WASAPI capture dispose is best-effort */ }
            try { _render?.Dispose(); } catch { /* WASAPI render dispose is best-effort */ }
            try { _monitor?.Dispose(); } catch { /* WASAPI monitor dispose is best-effort */ }
        });

        if (!disposeTask.Wait(TimeSpan.FromSeconds(2)))
            AppLog.Warning("VoiceEngine: WASAPI dispose timed out (2s)");

        _capture = null; _render = null; _monitor = null;

        _rxRing.Purge();
        SetState(EngineStatus.Idle);

        _gate.Dispose();
    }
}
