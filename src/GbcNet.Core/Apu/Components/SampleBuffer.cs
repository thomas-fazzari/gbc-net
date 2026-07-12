// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Apu.Components;

/// <summary>
/// Converts elapsed APU T-cycles into fixed-rate sample slots without floating-point drift.
/// </summary>
internal sealed class SampleBuffer<TSample>
{
    private readonly TSample[] _samples;
    private readonly int _sampleRate;
    private readonly int _sourceClockHz;
    private int _start;
    private long _accumulator;

    public SampleBuffer(
        int sourceClockHz,
        int sampleRate = ApuSampleTiming.DefaultSampleRate,
        int capacity = ApuSampleTiming.DefaultSampleBufferCapacity
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceClockHz);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _sourceClockHz = sourceClockHz;
        _sampleRate = sampleRate;
        _samples = new TSample[capacity];
    }

    /// <summary>
    /// Number of samples waiting to be drained.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Advances sample timing and returns how many samples are due.
    /// </summary>
    public int Tick(int tCycles)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tCycles);

        _accumulator += (long)tCycles * _sampleRate;
        var samplesDue = 0;

        while (_accumulator >= _sourceClockHz)
        {
            samplesDue++;
            _accumulator -= _sourceClockHz;
        }

        return samplesDue;
    }

    /// <summary>
    /// Buffers one sample, dropping the oldest sample if full.
    /// </summary>
    public void Add(TSample sample)
    {
        if (Count == _samples.Length)
        {
            _samples[_start] = sample;
            _start = (_start + 1) % _samples.Length;
            return;
        }

        _samples[(_start + Count) % _samples.Length] = sample;
        Count++;
    }

    /// <summary>
    /// Drains buffered samples in playback order, preserving samples that do not fit.
    /// </summary>
    public int Drain(Span<TSample> destination)
    {
        var drained = Math.Min(destination.Length, Count);

        for (var index = 0; index < drained; index++)
        {
            destination[index] = _samples[(_start + index) % _samples.Length];
        }

        _start = (_start + drained) % _samples.Length;
        Count -= drained;
        if (Count == 0)
        {
            _start = 0;
        }

        return drained;
    }

    internal SampleBufferState<TSample> CaptureState()
    {
        var bufferedSamples = new TSample[Count];
        for (var index = 0; index < bufferedSamples.Length; index++)
        {
            bufferedSamples[index] = _samples[(_start + index) % _samples.Length];
        }

        return new SampleBufferState<TSample>(bufferedSamples, _accumulator);
    }

    internal void ValidateState(SampleBufferState<TSample> state)
    {
        if (state.BufferedSamples is null || state.BufferedSamples.Length > _samples.Length)
        {
            throw new ArgumentException(
                "Buffered samples must not exceed the sample buffer capacity.",
                nameof(state)
            );
        }

        if (state.Accumulator < 0 || state.Accumulator >= _sourceClockHz)
        {
            throw new ArgumentException(
                "Accumulator must be within one source clock period.",
                nameof(state)
            );
        }
    }

    internal void RestoreState(SampleBufferState<TSample> state)
    {
        ValidateState(state);
        state.BufferedSamples.CopyTo(_samples, 0);
        _start = 0;
        Count = state.BufferedSamples.Length;
        _accumulator = state.Accumulator;
    }
}

internal readonly record struct SampleBufferState<TSample>(
    TSample[] BufferedSamples,
    long Accumulator
);
