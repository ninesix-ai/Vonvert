// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.DspEngine;

/*
 * Stereo width processor — converts mono DSP output to interleaved stereo
 * with configurable width and Haas delay for spatial perception.
 *
 * This effect operates on interleaved stereo buffers (L, R, L, R, ...).
 * It reads the mono-processed signal and creates a stereo image by:
 *   - Mid/Side processing: Mid = mono, Side = width × delayed/processed mono
 *   - Haas effect: tiny delay (0–2 ms) on one channel for spatial separation
 *   - Width control: 0 = mono, 1 = normal stereo, 2 = exaggerated stereo
 *
 * Integration point: VoiceEngine applies this AFTER the mono DSP chain,
 * converting from mono to interleaved stereo for WASAPI output.
 *
 * The Process() method expects a buffer that is 2× the mono chunk size
 * (interleaved L/R). The mono input is provided via SetMonoInput().
 *
 * NOTE (GitHub build): this class and its unit tests are ported for parity,
 * but it is intentionally NOT wired into EffectRegistry / the mono DSP chain
 * because the minimal VoiceEngine output path has no stereo post-processing
 * hook yet. It is dormant until that integration point is added.
 *
 * Zero-alloc on DSP thread.
 */
public sealed class StereoWidthEffect : IAudioEffect
{
    public string Name      { get; } = "VoiceStereoWidth";
    public bool   IsEnabled { get; set; }

    // ── Parameters ────────────────────────────────────────────────────────

    private float _width = 1.0f;
    private float _haasMs = 0.3f;
    private float _balance = 0f;

    /// <summary>Stereo width (0 = mono, 1 = normal, 2 = exaggerated).</summary>
    public float Width
    {
        get => _width;
        set => _width = Math.Clamp(value, 0f, 2f);
    }

    /// <summary>Haas delay in milliseconds (0 – 2.0). Creates spatial separation.</summary>
    public float HaasDelayMs
    {
        get => _haasMs;
        set => _haasMs = Math.Clamp(value, 0f, 2.0f);
    }

    /// <summary>L/R balance (-1 = left only, 0 = centre, +1 = right only).</summary>
    public float Balance
    {
        get => _balance;
        set => _balance = Math.Clamp(value, -1f, 1f);
    }

    // ── Internal state ────────────────────────────────────────────────────

    private const int SAMPLE_RATE = 48000;
    private const int MAX_HAAS_SAMPLES = 96; // 2 ms @ 48 kHz

    private readonly float[] _haasLine = new float[MAX_HAAS_SAMPLES + 1];
    private int _haasWrite;
    private int _haasDelaySamples;

    // Mono input buffer (set externally before Process)
    private float[] _monoInput = Array.Empty<float>();
    private int _monoInputLen;

    /// <summary>Set the mono DSP chain output to be converted to stereo.</summary>
    public void SetMonoInput(ReadOnlySpan<float> mono)
    {
        // Ensure backing array is large enough
        if (_monoInput.Length < mono.Length)
            _monoInput = new float[mono.Length];
        mono.CopyTo(_monoInput);
        _monoInputLen = mono.Length;
    }

    public void Process(Span<float> stereoBuf)
    {
        // Update Haas delay
        _haasDelaySamples = (int)(_haasMs * SAMPLE_RATE / 1000f);
        _haasDelaySamples = Math.Clamp(_haasDelaySamples, 0, MAX_HAAS_SAMPLES);

        float width = _width;
        float balL = 1f - Math.Max(0f, _balance);
        float balR = 1f + Math.Min(0f, _balance);

        int monoLen = _monoInputLen;
        int stereoLen = stereoBuf.Length;
        int frames = Math.Min(monoLen, stereoLen / 2);

        for (int i = 0; i < frames; i++)
        {
            float mono = _monoInput[i];

            // Haas delay: read from delay line
            int readPos = _haasWrite - _haasDelaySamples;
            if (readPos < 0) readPos += MAX_HAAS_SAMPLES + 1;
            float delayed = _haasLine[readPos];

            // Write current sample to delay line
            _haasLine[_haasWrite] = mono;
            _haasWrite = (_haasWrite + 1) % (MAX_HAAS_SAMPLES + 1);

            // Mid/Side decomposition
            float mid = mono;
            float side = (mono - delayed) * width;

            // L/R from Mid/Side
            float left = (mid + side) * 0.5f * balL;
            float right = (mid - side) * 0.5f * balR;

            // Write interleaved stereo
            stereoBuf[i * 2] = left;
            stereoBuf[i * 2 + 1] = right;
        }
    }

    public void Reset()
    {
        Array.Clear(_haasLine);
        _haasWrite = 0;
    }
}
