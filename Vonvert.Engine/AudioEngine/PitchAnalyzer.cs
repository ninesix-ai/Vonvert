// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Runtime.CompilerServices;

namespace Vonvert.Engine.AudioEngine;

/// <summary>
/// Real-time pitch analyzer for visualization.
/// Detects fundamental frequency using AMDF + parabolic interpolation,
/// then maps to musical note name + cents deviation.
/// Thread-safe: DSP thread calls FeedSamples(), UI thread reads snapshot via GetSnapshot().
/// Uses lock-free double-buffering for pitch output.
/// </summary>
public sealed class PitchAnalyzer : IPitchAnalyzer
{
    // ════ Configuration ════

    private const int SampleRate = 48000;
    private const int FrameSize  = 2048;   // — 42.7 ms @ 48 kHz — good low-freq resolution
    private const int MaxLag     = 600;    // ~80 Hz lower bound
    private const int MinLag     = 48;     // ~1000 Hz upper bound
    private const float RmsThreshold = 0.012f; // voiced / silence gate
    private const int HistorySize    = 200;  // pitch curve history length

    // Note names (chromatic, sharps)
    private static readonly string[] NoteNames =
        { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    // ════ Internal state ════

    private readonly float[] _frame = new float[FrameSize];
    private int _fill;

    // Double-buffered pitch snapshot (lock-free)
    private PitchSnapshot _snapA = new();
    private PitchSnapshot _snapB = new();
    private volatile PitchSnapshot _activeSnap;
    private volatile PitchSnapshot _backSnap;

    // Pitch history for curve drawing (circular buffer, lock-free)
    private readonly float[] _historyA = new float[HistorySize];
    private readonly float[] _historyB = new float[HistorySize];
    private volatile float[] _activeHistory;
    private volatile float[] _backHistory;
    private int _historyWrite;
    private int _historyCount;

    // Smoothing for visual stability
    private float _smoothedFreq;
    private float _smoothedCents;
    private const float FreqSmoothing = 0.4f;
    private const float CentsSmoothing = 0.3f;

    public PitchAnalyzer()
    {
        _activeSnap = _snapA;
        _backSnap = _snapB;
        _activeHistory = _historyA;
        _backHistory = _historyB;
        _smoothedFreq = 0f;
        _smoothedCents = 0f;
    }

    // ════ Public API ════

    /// <summary>
    /// Feed raw audio samples from the DSP thread.
    /// When enough samples accumulate, pitch detection runs automatically.
    /// Non-allocating on the hot path.
    /// </summary>
    public void FeedSamples(ReadOnlySpan<float> samples)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            _frame[_fill] = samples[i];
            _fill++;

            if (_fill >= FrameSize)
            {
                DetectPitch();
                _fill = 0;
            }
        }
    }

    /// <summary>Get the latest pitch snapshot. Safe to call from UI thread.</summary>
    public PitchSnapshot GetSnapshot() => _activeSnap;

    /// <summary>Get the pitch history buffer (0..1 normalized frequency or 0 for unvoiced). Safe from UI thread.</summary>
    public float[] GetHistory() => _activeHistory;

    /// <summary>Get the number of valid history entries.</summary>
    public int GetHistoryCount() => _historyCount;

    /// <summary>Reset all state.</summary>
    public void Reset()
    {
        _fill = 0;
        _smoothedFreq = 0f;
        _smoothedCents = 0f;
        _historyWrite = 0;
        _historyCount = 0;
        Array.Clear(_frame);
        Array.Clear(_historyA);
        Array.Clear(_historyB);

        _snapA.Clear();
        _snapB.Clear();
        AppLog.Debug("[PitchAnalyzer] Reset");
    }

    // ════ Pitch detection ════

    private void DetectPitch()
    {
        // 1. RMS energy — skip silent frames
        float rms = RmsEnergy();
        if (rms < RmsThreshold)
        {
            WriteSilence();
            return;
        }

        // 2. AMDF pitch detection
        float period = DetectPeriod();
        if (period < MinLag || period > MaxLag)
        {
            WriteSilence();
            return;
        }

        // 3. Convert period to frequency
        float freq = SampleRate / period;

        // 4. Clamp to reasonable voice range (60-1000 Hz)
        if (freq < 60f || freq > 1000f)
        {
            WriteSilence();
            return;
        }

        // 5. Convert to MIDI note number
        float midiNote = 69f + 12f * MathF.Log2(freq / 440f);

        // 6. Find nearest note
        int nearestNote = (int)MathF.Round(midiNote) % 12;
        if (nearestNote < 0) nearestNote += 12;
        int octave = ((int)MathF.Round(midiNote) / 12) - 1;

        // 7. Compute cents deviation from nearest note
        float nearestMidi = MathF.Round(midiNote);
        float cents = (midiNote - nearestMidi) * 100f;

        // 8. Apply smoothing
        _smoothedFreq = _smoothedFreq * FreqSmoothing + freq * (1f - FreqSmoothing);
        _smoothedCents = _smoothedCents * CentsSmoothing + cents * (1f - CentsSmoothing);

        // 9. Write to back buffer
        var snap = _backSnap;
        snap.Frequency    = _smoothedFreq;
        snap.MidiNote     = midiNote;
        snap.NoteName     = NoteNames[nearestNote];
        snap.Octave       = octave;
        snap.CentsDeviation = _smoothedCents;
        snap.IsVoiced     = true;

        // 10. Write to history (normalized: 0-1 range mapping 60-1000 Hz log scale)
        float normalizedPitch = (MathF.Log2(_smoothedFreq / 60f)) / MathF.Log2(1000f / 60f);
        _backHistory[_historyWrite] = Math.Clamp(normalizedPitch, 0f, 1f);
        _historyWrite = (_historyWrite + 1) % HistorySize;
        _historyCount = Math.Min(_historyCount + 1, HistorySize);

        // 11. Swap buffers
        SwapBuffers();
    }

    private void WriteSilence()
    {
        // Decay smoothed values toward zero
        _smoothedFreq *= 0.9f;
        _smoothedCents *= 0.8f;

        var snap = _backSnap;
        snap.Frequency    = _smoothedFreq;
        snap.MidiNote     = 0f;
        snap.NoteName     = "—";
        snap.Octave       = 0;
        snap.CentsDeviation = _smoothedCents;
        snap.IsVoiced     = false;

        // Mark as silence in history
        _backHistory[_historyWrite] = -1f; // -1 = silence
        _historyWrite = (_historyWrite + 1) % HistorySize;
        _historyCount = Math.Min(_historyCount + 1, HistorySize);

        SwapBuffers();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SwapBuffers()
    {
        // Swap pitch snapshot
        var tempSnap = _activeSnap;
        _activeSnap = _backSnap;
        _backSnap = tempSnap;

        // Swap history
        var tempHist = _activeHistory;
        _activeHistory = _backHistory;
        _backHistory = tempHist;
    }

    // ════ AMDF period detection ════

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float DetectPeriod()
    {
        // Average Squared Difference Function — find the lag — that minimises
        //   ASDF(—) = (1/N) — (frame[i] ? frame[i + —])2
        // The deepest dip (below a voicing threshold) corresponds to the
        // fundamental period.

        float bestVal = float.MaxValue;
        int   bestLag = 0;

        for (int lag = MinLag; lag <= MaxLag; lag++)
        {
            float sum = 0f;
            int   n   = FrameSize - lag;
            for (int i = 0; i < n; i++)
            {
                float diff = _frame[i] - _frame[i + lag];
                sum += diff * diff;
            }
            float avg = sum / n;
            if (avg < bestVal)
            {
                bestVal = avg;
                bestLag = lag;
            }
        }

        // Voicing check: if the best ASDF is not significantly below the
        // zero-lag energy, the frame is likely unvoiced — skip.
        float energy = 0f;
        for (int i = 0; i < FrameSize; i++)
            energy += _frame[i] * _frame[i];
        energy /= FrameSize;

        if (bestVal > energy * 0.5f)
            return -1f; // unvoiced

        // Parabolic interpolation around the best lag for sub-sample precision
        if (bestLag > MinLag && bestLag < MaxLag)
        {
            float prev = AsdfAt(bestLag - 1);
            float curr = bestVal;
            float next = AsdfAt(bestLag + 1);
            float denom = 2f * (prev + next - 2f * curr);
            if (MathF.Abs(denom) > 1e-12f)
            {
                float shift = (prev - next) / denom;
                if (MathF.Abs(shift) < 1f)
                    return bestLag + shift;
            }
        }

        return bestLag;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float AsdfAt(int lag)
    {
        float sum = 0f;
        int   n   = FrameSize - lag;
        for (int i = 0; i < n; i++)
        {
            float diff = _frame[i] - _frame[i + lag];
            sum += diff * diff;
        }
        return sum / n;
    }

    // ════ Utility ════

    private float RmsEnergy()
    {
        float sum = 0f;
        for (int i = 0; i < FrameSize; i++)
            sum += _frame[i] * _frame[i];
        return MathF.Sqrt(sum / FrameSize);
    }
}

/// <summary>
/// Immutable-ish snapshot of pitch detection result.
/// Written by DSP thread (back buffer), read by UI thread (active buffer).
/// </summary>
public sealed class PitchSnapshot
{
    /// <summary>Detected fundamental frequency in Hz (0 if unvoiced).</summary>
    public float Frequency;

    /// <summary>Detected MIDI note number (e.g. 69 = A4).</summary>
    public float MidiNote;

    /// <summary>Note name string (e.g. "C", "F#").</summary>
    public string NoteName = "—";

    /// <summary>Octave number (e.g. 4 for C4).</summary>
    public int Octave;

    /// <summary>Deviation from nearest note in cents (-50 to +50).</summary>
    public float CentsDeviation;

    /// <summary>Whether the frame is voiced (has a clear pitch).</summary>
    public bool IsVoiced;

    /// <summary>Full display string: "C4", "F#3", etc.</summary>
    public string DisplayNote => IsVoiced ? $"{NoteName}{Octave}" : "—";

    /// <summary>Cents display string: "+12", "-8", etc.</summary>
    public string DisplayCents => IsVoiced ? $"{(CentsDeviation >= 0 ? "+" : "")}{CentsDeviation:F0}" : "—";

    public void Clear()
    {
        Frequency = 0f;
        MidiNote = 0f;
        NoteName = "—";
        Octave = 0;
        CentsDeviation = 0f;
        IsVoiced = false;
    }
}
