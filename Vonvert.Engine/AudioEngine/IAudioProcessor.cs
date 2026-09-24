// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.AudioEngine;

/*
 * IAudioProcessor — audio processor abstraction.
 * NullAudioProcessor — the default open-source implementation.
 */

// Main processor interface
public interface IAudioProcessor
{
    DspEngine.DSPChain Chain { get; }
    EngineStats Metrics { get; }

    void PreMix(Span<float> work, int read);
    void Process(Span<float> work);
    void PostAnalyze(Span<float> work, int read);
    void OnStart();
    void OnStop();


    SpectrumAnalyzer Spectrum { get; }
    LoudnessMeter Loudness { get; }
    IPitchAnalyzer Pitch { get; }
    IDuckingProcessor Ducking { get; }

    void Dispose();
}

// NullAudioProcessor — open-source build
public sealed class NullAudioProcessor : IAudioProcessor
{
    public DspEngine.DSPChain Chain { get; }
    public EngineStats Metrics { get; } = new();

    // ── Analysis features ─────────────────────────────────────────
    private readonly SpectrumAnalyzer _spectrum;
    private readonly PitchAnalyzer _pitch;
    private readonly DuckingProcessor _ducking;
    private readonly LoudnessMeter _loudness;

    // Heavy analysis (FFT / pitch / loudness) runs on a dedicated
    // low-priority thread, never on the real-time audio thread.
    private readonly AnalyzerPump _pump;

    // Closed-loop pitch-register normalizer driven from the analyzer thread.
    private readonly AdaptivePitchNormalizer _pitchNormalizer = new();

    // Optional file recorder. Fed from the real-time audio thread; the service
    // uses non-blocking writes so a stalled disk can never underrun playback.
    private RecordingService? _recording;

    public SpectrumAnalyzer Spectrum => _spectrum;
    public LoudnessMeter Loudness => _loudness;
    public IPitchAnalyzer Pitch => _pitch;
    public IDuckingProcessor Ducking => _ducking;

    /// <summary>Auto-pitch closed-loop control. Exposed on the concrete type only
    /// (not on IAudioProcessor).</summary>
    public IPitchNormalizerControl PitchNormalizer => _pitchNormalizer;

    /// <summary>Attach (or detach with <c>null</c>) the file recorder that captures
    /// the pre/post-DSP signal. Exposed on the concrete type only.</summary>
    public void AttachRecordingService(RecordingService? service) => _recording = service;

    public NullAudioProcessor()
    {
        Chain = DspEngine.DSPChain.CreateDefault(Metrics);

        // Initialize analysis features
        _spectrum = new SpectrumAnalyzer();
        _pitch = new PitchAnalyzer();
        _ducking = new DuckingProcessor();
        _loudness = new LoudnessMeter();

        // Submit() only enqueues on the audio thread; the analyzers run on the
        // pump's worker thread.
        _pump = new AnalyzerPump(block =>
        {
            _spectrum.FeedSamples(block);
            _pitch.FeedSamples(block);
            _loudness.FeedSamples(block);

            if (_pitchNormalizer.Enabled)
            {
                var sn = _pitch.GetSnapshot();
                _pitchNormalizer.Update(sn.IsVoiced, sn.Frequency);
            }
        });
    }

    public void PreMix(Span<float> work, int read)
    {
        // Original mode records the pre-DSP signal captured here.
        if (_recording?.IsRecording == true && _recording.RecordMode == RecordMode.Original)
            _recording.WriteRawSamples(work[..read]);
    }

    public void Process(Span<float> work) => Chain.Process(work);

    public void PostAnalyze(Span<float> work, int read)
    {
        // Hand the heavy analyzers (FFT / AMDF / loudness) to the low-priority
        // pump thread — Submit only copies the block and returns immediately.
        _pump.Submit(work[..read]);

        // Processed mode records the post-DSP signal captured here.
        if (_recording?.IsRecording == true && _recording.RecordMode == RecordMode.Processed)
            _recording.WriteSamples(work[..read]);
    }

    public void OnStart() { _pitchNormalizer.Reset(); _pump.Start(); }

    public void OnStop()
    {
        // Stop the analysis worker first so it is not running while the
        // analyzers are reset.
        _pump.Stop();
        _spectrum.Reset();
        _pitch.Reset();
        _pitchNormalizer.Reset();
        _ducking.Reset();
        _loudness.Reset();
    }

    public void Dispose()
    {
        _pump.Dispose();
        _ducking.Reset();
    }
}
