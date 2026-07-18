// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Apu;

namespace GbcNet.App.Audio;

/// <summary>
/// Single-producer/single-consumer ring buffer for audio samples.
/// </summary>
internal sealed class AudioRingBuffer
{
    private readonly ApuStereoSample[] _samples;
    private long _clearCursor;
    private long _readCursor;
    private long _writeCursor;

    internal AudioRingBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _samples = new ApuStereoSample[capacity];
    }

    /// <summary>
    /// Number of queued sample frames visible to the audio output.
    /// </summary>
    internal int Count
    {
        get
        {
            var readCursor = Math.Max(
                val1: Volatile.Read(ref _readCursor),
                val2: Volatile.Read(ref _clearCursor)
            );
            var count = Volatile.Read(ref _writeCursor) - readCursor;
            return (int)Math.Clamp(value: count, min: 0, max: _samples.Length);
        }
    }

    /// <summary>
    /// Adds samples from the emulation thread, dropping new samples when the buffer is full.
    /// </summary>
    internal void Enqueue(ReadOnlySpan<ApuStereoSample> samples)
    {
        var writeCursor = Volatile.Read(ref _writeCursor);
        var readCursor = Math.Max(
            val1: Volatile.Read(ref _readCursor),
            val2: Volatile.Read(ref _clearCursor)
        );

        foreach (var sample in samples)
        {
            if (writeCursor - readCursor >= _samples.Length)
            {
                readCursor = Math.Max(
                    val1: Volatile.Read(ref _readCursor),
                    val2: Volatile.Read(ref _clearCursor)
                );
                if (writeCursor - readCursor >= _samples.Length)
                {
                    break;
                }
            }

            _samples[(int)(writeCursor % _samples.Length)] = sample;
            writeCursor++;
        }

        Volatile.Write(location: ref _writeCursor, value: writeCursor);
    }

    /// <summary>
    /// Reads one sample from the audio callback thread.
    /// </summary>
    internal bool TryDequeue(out ApuStereoSample sample)
    {
        var writeCursor = Volatile.Read(ref _writeCursor);
        var readCursor = Math.Max(
            val1: Volatile.Read(ref _readCursor),
            val2: Volatile.Read(ref _clearCursor)
        );

        if (readCursor >= writeCursor)
        {
            sample = default;
            return false;
        }

        sample = _samples[(int)(readCursor % _samples.Length)];
        Volatile.Write(location: ref _readCursor, value: readCursor + 1);
        return true;
    }

    /// <summary>
    /// Drops queued samples without reallocating the backing buffer.
    /// </summary>
    internal void Clear()
    {
        var clearCursor = Volatile.Read(ref _writeCursor);
        var publishedCursor = Volatile.Read(ref _clearCursor);
        while (clearCursor > publishedCursor)
        {
            var observedCursor = Interlocked.CompareExchange(
                location1: ref _clearCursor,
                value: clearCursor,
                comparand: publishedCursor
            );
            if (observedCursor == publishedCursor)
            {
                return;
            }

            publishedCursor = observedCursor;
        }
    }
}
