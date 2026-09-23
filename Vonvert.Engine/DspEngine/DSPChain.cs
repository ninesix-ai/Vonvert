// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Diagnostics;
using Vonvert.Engine.AudioEngine;

namespace Vonvert.Engine.DspEngine;

/*
 * DSP effect chain with free routing support.
 */
public sealed class DSPChain
{
    private readonly List<IAudioEffect> _effects = [];
    // Volatile snapshot of all effects, rebuilt only on structural change.
    // Enabled-state is evaluated live per block in Process(), so toggling a
    // preset's effect on/off takes effect on the next block without any
    // per-block allocation or locking on the audio thread.
    private volatile IAudioEffect[] _snapshot = [];
    private readonly EngineStats? _metrics;

    // Protects _effects during concurrent access (UI thread modifies, DSP
    // thread reads through the snapshot).
    private readonly object _effectsLock = new();

    public IReadOnlyList<IAudioEffect> Effects { get { lock (_effectsLock) return _effects.ToList(); } }

    public DSPChain() { }

    public DSPChain(EngineStats metrics) => _metrics = metrics;

    private void RebuildSnapshot()
    {
        lock (_effectsLock)
        {
            _snapshot = _effects.ToArray();
        }
    }

    public void Add(IAudioEffect effect)
    {
        lock (_effectsLock) { _effects.Add(effect); }
        RebuildSnapshot();
    }

    public void Remove(IAudioEffect effect)
    {
        lock (_effectsLock) { _effects.Remove(effect); RebuildSnapshot(); }
    }

    public void Clear()
    {
        lock (_effectsLock) { _effects.Clear(); RebuildSnapshot(); }
    }

    // ── Free routing: reorder effects ─────────────────────────────────────

    /// <summary>Insert an effect at a specific position in the chain.</summary>
    public void InsertAt(int index, IAudioEffect effect)
    {
        lock (_effectsLock)
        {
            _effects.Insert(Math.Clamp(index, 0, _effects.Count), effect);
        }
        RebuildSnapshot();
    }

    /// <summary>Reorder an effect within the chain.</summary>
    public void Move(int fromIndex, int toIndex)
    {
        lock (_effectsLock)
        {
            if (fromIndex < 0 || fromIndex >= _effects.Count) return;
            if (toIndex < 0 || toIndex >= _effects.Count) return;
            if (fromIndex == toIndex) return;
            var effect = _effects[fromIndex];
            _effects.RemoveAt(fromIndex);
            _effects.Insert(toIndex > fromIndex ? toIndex - 1 : toIndex, effect);
        }
        RebuildSnapshot();
    }

    public bool Move(string effectName, int toIndex)
    {
        int idx = _effects.FindIndex(e => e.Name == effectName);
        if (idx < 0) return false;
        Move(idx, toIndex);
        return true;
    }

    public void ReorderByName(IEnumerable<string> orderedNames)
    {
        lock (_effectsLock)
        {
            var lookup = new Dictionary<string, IAudioEffect>();
            foreach (var fx in _effects)
                lookup.TryAdd(fx.Name, fx); // first occurrence wins

            var reordered = new List<IAudioEffect>(_effects.Count);
            foreach (var name in orderedNames)
            {
                if (lookup.TryGetValue(name, out var fx))
                {
                    reordered.Add(fx);
                    lookup.Remove(name);
                }
            }
            // Append any remaining effects not mentioned in the name list
            foreach (var fx in lookup.Values)
                reordered.Add(fx);

            _effects.Clear();
            _effects.AddRange(reordered);
        }
        RebuildSnapshot();
    }

    public List<string> GetEffectOrder()
    {
        lock (_effectsLock) return _effects.Select(e => e.Name).ToList();
    }

    public void Process(Span<float> buffer)
    {
        // Hot path: a single volatile read, no lock and no allocation. The
        // chain is responsible for the enabled check (per the IAudioEffect
        // contract), evaluated live each block so a runtime IsEnabled change
        // is honored without rebuilding the chain.
        var fxArray = _snapshot;
        foreach (var fx in fxArray)
        {
            if (!fx.IsEnabled) continue;
            try { fx.Process(buffer); }
            catch (Exception ex)
            {
                if (_metrics != null)
                    _metrics.DspExceptions++;
                Debug.WriteLine($"[DSPChain] Exception in {fx.Name}: {ex.GetType().Name} — {ex.Message}");
            }
        }
    }

    public void Reset()
    {
        var fxArray = _snapshot;
        foreach (var fx in fxArray) fx.Reset();
    }

    // ── Effect factory (delegates to EffectRegistry) ──
    public static IAudioEffect? CreateEffectByName(string name) => EffectRegistry.Create(name);

    // ── Default chain factory (delegates to EffectRegistry) ──

    public static DSPChain CreateDefault()
    {
        return CreateDefault(null);
    }

    public static DSPChain CreateDefault(EngineStats? metrics) => EffectRegistry.CreateDefault(metrics);
}
