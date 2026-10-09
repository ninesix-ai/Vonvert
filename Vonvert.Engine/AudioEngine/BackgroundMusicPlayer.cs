// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Numerics;
using NAudio.Wave;

namespace Vonvert.Engine.AudioEngine;

/// <summary>
/// Background music player that provides resampled 48 kHz mono samples
/// for mixing into the DSP output.  Supports MP3 / WAV / any format
/// decodeable by NAudio's AudioFileReader.
///
/// Threading contract:
///   UI thread   — Load / Play / Pause / Stop / Seek
///   DSP thread  — ReadSamples (zero-alloc hot path)
/// </summary>
public sealed class BackgroundMusicPlayer : IBgmPlayer, IDisposable
{
    private const int TARGET_RATE = AudioConstants.EngineRate;

    /// <summary>Mixer target rate (Hz) — exposed for tests to assert rate alignment.</summary>
    internal static int TargetRate => TARGET_RATE;

    // --- State (volatile for cross-thread visibility) -----------------------
    private volatile bool _isPlaying;
    private volatile bool _isLoop;
    private volatile float _volume = 0.5f;

    // --- Audio source -------------------------------------------------------
    private AudioFileReader? _reader;
    // Serializes access to the non-thread-safe AudioFileReader so the DSP
    // thread's Read() and the UI thread's Load()/Stop() Dispose()/reassign can
    // never overlap. Reading a reader that is being disposed throws types
    // (NAudio internals, COM, IO) that an exception allow-list cannot cover,
    // so mutual exclusion — not catch filtering — is the correct guarantee.
    private readonly object _ioLock = new();
    private int _srcSampleRate;
    private int _srcChannels;
    private long _srcTotalSamples;   // per-channel sample count of source
    private double _durationSec;

    // --- Resampling state (DSP thread only) ---------------------------------
    private double _srcPos;          // fractional source position (per-channel samples)
    private long _samplesRead;       // output samples produced (for position tracking)

    // --- Pre-allocated read buffer (DSP thread only) ------------------------
    private float[] _readBuf = new float[4096];

    // --- Metadata -----------------------------------------------------------
    private string _filePath = string.Empty;
    private string _trackName = string.Empty;

    // --- Fade envelope (DSP thread only) ------------------------------------
    private double _fadeInDurationSec;
    private double _fadeOutDurationSec;

    // --- Events -------------------------------------------------------------
    public event Action? PlaybackEnded;

    // ════ Public Properties ════

    public bool IsPlaying => _isPlaying;
    public bool IsPaused => !_isPlaying && _reader != null && _samplesRead > 0;

    public bool IsLoop
    {
        get => _isLoop;
        set => _isLoop = value;
    }

    /// <summary>Mix volume.  0.0 = silent, 1.0 = unity, 2.0 = double.</summary>
    public float Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0f, 2f);
    }

    public string FilePath  => _filePath;
    public string TrackName => _trackName;
    public double DurationSec => _durationSec;

    /// <summary>Current playback position in seconds (updated by DSP thread, safe to poll from UI).</summary>
    public double PositionSec => _srcSampleRate > 0 ? (double)_samplesRead / TARGET_RATE : 0;

    public bool IsReady => _reader != null;

    /// <summary>Fade-in duration in seconds. 0 = no fade.</summary>
    public double FadeInDurationSec
    {
        get => _fadeInDurationSec;
        set => _fadeInDurationSec = Math.Max(0, value);
    }

    /// <summary>Fade-out duration in seconds. 0 = no fade.</summary>
    public double FadeOutDurationSec
    {
        get => _fadeOutDurationSec;
        set => _fadeOutDurationSec = Math.Max(0, value);
    }

    // ════ Load ════

    /// <summary>Load an audio file.  Stops any current playback first.</summary>
    public void Load(string path)
    {
        // 1. Stop DSP thread from reading — ReadSamples checks _isPlaying first
        _isPlaying = false;

        _filePath  = path;
        _trackName = Path.GetFileNameWithoutExtension(path);

        // 2. Decode the new file OUTSIDE the lock so a slow open cannot stall
        //    the real-time ReadSamples thread. Only the atomic swap of the
        //    reader (and disposal of the previous one) needs mutual exclusion.
        AudioFileReader reader;
        int rate, channels;
        long frames;
        double dur;
        try
        {
            reader = new AudioFileReader(path);
            // Some AudioFileReader constructors advance position during format
            // detection / internal buffering — rewind to the start of audio.
            try { reader.Position = 0; } catch { /* seek is best-effort */ }

            int bps      = reader.WaveFormat.BitsPerSample;
            rate         = reader.WaveFormat.SampleRate;
            channels     = reader.WaveFormat.Channels;
            frames       = reader.Length / ((bps / 8) * channels);
            dur          = (double)frames / rate;
        }
        catch (Exception ex)
        {
            // Failed load leaves no reader (ReadSamples then returns silence).
            lock (_ioLock)
            {
                var old = _reader;
                _reader = null;
                ResetState();
                try { old?.Dispose(); } catch { /* dispose is best-effort */ }
            }
            AppLog.Error(ex, "[BgmPlayer] Failed to load: {File}", path);
            return;
        }

        // 3. Atomically publish metadata + new reader and retire the old one.
        //    All fields are written while holding the lock, so ReadSamples
        //    never observes a non-null reader with stale metadata, and the old
        //    reader is never disposed while a DSP Read() is in flight.
        lock (_ioLock)
        {
            _srcSampleRate   = rate;
            _srcChannels     = channels;
            _srcTotalSamples = frames;
            _durationSec     = dur;
            _srcPos          = 0;
            _samplesRead     = 0;

            var previous = _reader;
            _reader = reader;
            try { previous?.Dispose(); } catch { /* dispose is best-effort */ }
        }

        AppLog.Information("[BgmPlayer] Loaded: {File} ({Rate}Hz, {Ch}ch, {Duration:F1}s)",
            path, rate, channels, dur);
    }

    // ════ Transport Controls ════

    public void Play()
    {
        if (_reader == null) return;
        _isPlaying = true;
        AppLog.Debug("[BgmPlayer] Play: {Track}", _trackName);
    }

    public void Pause()
    {
        if (_isPlaying)
            AppLog.Debug("[BgmPlayer] Pause: {Track}", _trackName);
        _isPlaying = false;
    }

    public void Stop()
    {
        if (_isPlaying || _samplesRead > 0)
            AppLog.Debug("[BgmPlayer] Stop: {Track}", _trackName);
        _isPlaying = false;
        lock (_ioLock)
        {
            if (_reader != null)
            {
                try { _reader.Position = 0; } catch { /* seek is best-effort */ }
                _srcPos      = 0;
                _samplesRead = 0;
            }
        }
    }

    /// <summary>Seek to a position in seconds.</summary>
    public void Seek(double positionSec)
    {
        if (positionSec < 0) return;
        lock (_ioLock)
        {
            var reader = _reader;
            if (reader == null) return;
            positionSec = Math.Min(positionSec, _durationSec);
            long targetSample = (long)(positionSec * _srcSampleRate);
            try
            {
                reader.Position = targetSample * _srcChannels * (reader.WaveFormat.BitsPerSample / 8);
                _srcPos      = targetSample;
                _samplesRead = (long)(positionSec * TARGET_RATE);
            }
            catch { /* seek position calc is best-effort */ }
        }
    }

    // ════ DSP Thread: ReadSamples ════

    /// <summary>
    /// Read resampled 48 kHz mono float samples into <paramref name="dest"/>.
    /// Called from the DSP thread.  Zero allocation on the hot path (buffer is
    /// pre-allocated and only grown, never shrunk).
    ///
    /// Thread safety: the whole read runs under <see cref="_ioLock"/>, so the
    /// non-thread-safe AudioFileReader can never be disposed or reassigned by a
    /// concurrent Load()/Stop()/Seek()/Dispose() while a Read() is in flight.
    /// </summary>
    public int ReadSamples(Span<float> dest)
    {
        lock (_ioLock)
        {
        // Snapshot shared state — consistent for the duration under the lock.
        var reader = _reader;
        if (!_isPlaying || reader == null) return 0;

        int destLen = dest.Length;
        int written = 0;

        double ratio = (double)_srcSampleRate / TARGET_RATE;
        int srcCh    = _srcChannels;

        // Guard against zero/invalid metadata during Load() transition
        if (srcCh <= 0 || _srcSampleRate <= 0 || ratio <= 0) return 0;

        // Pre-compute fade envelope boundaries
        long samplesSoFar = _samplesRead;
        double fadeOutStart = _durationSec - _fadeOutDurationSec;
        long fadeInEndSample  = (long)(_fadeInDurationSec  * TARGET_RATE);
        long fadeOutStartSample = (long)(fadeOutStart * TARGET_RATE);

        try
        {
            while (written < destLen)
            {
                // Ensure read buffer is large enough for one source frame batch
                int needFrames = (int)Math.Ceiling((destLen - written) * ratio) + 2;
                int needSamples = needFrames * srcCh;
                EnsureReadBuffer(needSamples);

                // Snapshot _readBuf AFTER EnsureReadBuffer — use the same array
                // for both ReadSourceFrames and the downmix loop below.
                // This prevents a concurrent Load() from swapping _readBuf
                // between the two accesses.
                var readBuf = _readBuf;

                int framesRead = ReadSourceFrames(reader, readBuf, needFrames, srcCh);
                if (framesRead <= 0)
                {
                    if (_isLoop)
                    {
                        try { reader.Position = 0; } catch { break; }
                        _srcPos = 0;
                        continue;
                    }
                    _isPlaying = false;
                    PlaybackEnded?.Invoke();
                    break;
                }

                // Resample + downmix source frames — mono dest samples
                for (int f = 0; f < framesRead && written < destLen; f++)
                {
                    dest[written++] = readBuf[f * srcCh];
                    _srcPos += ratio;
                }
            }
        }
        catch (Exception ex) when (ex is ObjectDisposedException
                                 or InvalidOperationException
                                 or NullReferenceException
                                 or IndexOutOfRangeException
                                 or ArgumentException)
        {
            // Reader was disposed/replaced/resized by concurrent Load()
            // — stop gracefully.  The DSP loop will get silence until the
            // next Load+Play.
            _isPlaying = false;
            return written;
        }

        // Apply fade envelope to the batch
        if (_fadeInDurationSec > 0 || _fadeOutDurationSec > 0)
        {
            for (int i = 0; i < written; i++)
            {
                long absPos = samplesSoFar + i;
                float fade = 1f;

                if (_fadeInDurationSec > 0 && absPos < fadeInEndSample)
                    fade *= (float)(absPos / (double)fadeInEndSample);

                if (_fadeOutDurationSec > 0 && absPos > fadeOutStartSample)
                {
                    long fadeOutLen = (long)(_fadeOutDurationSec * TARGET_RATE);
                    if (fadeOutLen > 0)
                        fade *= MathF.Max(0f, (float)((fadeOutStartSample + fadeOutLen - absPos) / (double)fadeOutLen));
                }

                dest[i] *= fade;
            }
        }

        _samplesRead += written;
        return written;
        }
    }

    // ════ Dispose ════

    public void Dispose()
    {
        _isPlaying = false;
        DisposeReader();
        AppLog.Information("[BgmPlayer] Disposed");
    }

    // ══════════════════════════════════════════════════════════════════
    //  Private helpers
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Read up to <paramref name="maxFrames"/> frames from the source at the
    /// current position.  Each frame is downmixed to mono and stored at
    /// <c>dst[frameIndex]</c>.  Returns the number of frames actually read.
    /// Uses the caller-supplied <paramref name="reader"/> snapshot for
    /// thread safety.
    /// </summary>
    private int ReadSourceFrames(AudioFileReader reader, float[] dst, int maxFrames, int srcCh)
    {
        if (srcCh <= 0) return 0;
        // AudioFileReader.Read(float[], int, int) uses SAMPLE offsets, not bytes.
        int samplesNeeded = maxFrames * srcCh;

        int totalSamplesRead = 0;
        int offset = 0;

        while (samplesNeeded > 0)
        {
            // dst.Length is in samples; limit read to available buffer space
            int toRead = Math.Min(samplesNeeded, dst.Length - offset);
            if (toRead <= 0) break;
            int n = reader.Read(dst, offset, toRead);
            if (n <= 0) break;
            offset += n;
            samplesNeeded -= n;
            totalSamplesRead += n;
        }

        int totalFrames = totalSamplesRead / srcCh;

        // In-place downmix: if stereo, average L+R into dst[0..totalFrames]
        if (srcCh == 2)
        {
            for (int f = 0; f < totalFrames; f++)
                dst[f] = (dst[f * 2] + dst[f * 2 + 1]) * 0.5f;
        }
        // Mono: data is already at dst[0..totalFrames] (no conversion needed)

        return totalFrames;
    }

    private void EnsureReadBuffer(int minSize)
    {
        if (_readBuf.Length < minSize)
        {
            int newSize = Math.Max(minSize, _readBuf.Length * 2);
            // Round up to next power of 2 for fewer reallocations
            newSize = (int)BitOperations.RoundUpToPowerOf2((uint)newSize);
            _readBuf = new float[newSize];
        }
    }

    private void ResetState()
    {
        _srcSampleRate   = 0;
        _srcChannels     = 0;
        _srcTotalSamples = 0;
        _durationSec     = 0;
        _srcPos          = 0;
        _samplesRead     = 0;
    }

    private void DisposeReader()
    {
        // Swap out and dispose the reader under the lock so no concurrent
        // ReadSamples can be mid-Read on it.
        lock (_ioLock)
        {
            var old = _reader;
            _reader = null;
            ResetState();
            try { old?.Dispose(); } catch { /* old reader dispose is best-effort */ }
        }
    }
}
