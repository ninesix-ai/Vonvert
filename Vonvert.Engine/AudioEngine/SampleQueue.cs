// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Engine.AudioEngine;

/*
 * Lock-free-style sample queue for mono float audio data.
 *
 * Capacity is always rounded up to the next power of two so that the
 * wrap-around index can be computed with a single bitwise AND.
 *
 * All public methods are serialised through a lightweight monitor lock.
 * The critical sections are proportional to the bulk-copy size
 * (~1 µs per 4096 samples) which is negligible at audio block rates.
 *
 * MPMC-safe: any number of producers / consumers may call Enqueue / Dequeue
 * concurrently because every operation is fully serialised.
 */
public sealed class SampleQueue
{
    private readonly float[] _data;
    private readonly int     _mask;
    private readonly object  _gate = new();

    private int _writePos;   // next slot to fill
    private int _readPos;    // next slot to drain

    /// <summary>Create a new queue whose usable capacity ≥ <paramref name="capacity"/> samples.</summary>
    public SampleQueue(int capacity)
    {
        int pow2 = 1;
        while (pow2 < capacity) pow2 <<= 1;
        _mask = pow2 - 1;
        _data = GC.AllocateArray<float>(pow2, pinned: true);
    }

    /// <summary>Number of readable samples currently queued.</summary>
    public int Count
    {
        get { lock (_gate) return (_writePos - _readPos) & _mask; }
    }

    /// <summary>True when no samples are queued.</summary>
    public bool IsIdle
    {
        get { lock (_gate) return _writePos == _readPos; }
    }

    /// <summary>
    /// Enqueue samples into the queue.  Returns the number of samples
    /// actually written (may be less than <paramref name="data"/>.Length
    /// when the queue is full).
    /// </summary>
    public int Enqueue(ReadOnlySpan<float> data)
    {
        lock (_gate)
        {
            int copied = 0;
            while (copied < data.Length)
            {
                int free = (_readPos - _writePos - 1) & _mask;
                int room = Math.Min(free, _mask + 1 - _writePos);
                if (room <= 0) break;

                int batch = Math.Min(data.Length - copied, room);
                data.Slice(copied, batch).CopyTo(_data.AsSpan(_writePos, batch));
                _writePos = (_writePos + batch) & _mask;
                copied += batch;
            }
            return copied;
        }
    }

    /// <summary>
    /// Dequeue up to <paramref name="dest"/>.Length samples from the queue.
    /// Returns the number of samples actually read.
    /// </summary>
    public int Dequeue(Span<float> dest)
    {
        lock (_gate)
        {
            int consumed = 0;
            while (consumed < dest.Length && _readPos != _writePos)
            {
                int filled = (_writePos - _readPos) & _mask;
                int avail  = Math.Min(filled, _mask + 1 - _readPos);
                if (avail <= 0) break;

                int batch = Math.Min(dest.Length - consumed, avail);
                _data.AsSpan(_readPos, batch).CopyTo(dest.Slice(consumed, batch));
                _readPos = (_readPos + batch) & _mask;
                consumed += batch;
            }
            return consumed;
        }
    }

    /// <summary>Reset both pointers — safe to call from any thread.</summary>
    public void Purge()
    {
        lock (_gate) { _writePos = 0; _readPos = 0; }
    }
}
