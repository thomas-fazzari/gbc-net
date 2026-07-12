// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Apu;
using GbcNet.Core.Apu.Components;

namespace GbcNet.Tests.Apu;

public sealed class SampleBufferTests
{
    private const int SourceClockHz = 100;
    private const int SampleRate = 10;

    [Fact]
    public void Tick_ReturnsSampleRateAfterOneSourceSecond()
    {
        SampleBuffer<ApuStereoSample> buffer = new(SourceClockHz, SampleRate);

        var samplesDue = buffer.Tick(SourceClockHz);

        Assert.Equal(SampleRate, samplesDue);
    }

    [Fact]
    public void Tick_AccumulatesPartialTicksWithoutLosingProgress()
    {
        SampleBuffer<ApuStereoSample> buffer = new(SourceClockHz, SampleRate);

        Assert.Equal(0, buffer.Tick(9));
        Assert.Equal(1, buffer.Tick(1));
    }

    [Fact]
    public void Tick_ReturnsZeroBeforeSampleIsDue()
    {
        SampleBuffer<ApuStereoSample> buffer = new(SourceClockHz, SampleRate);

        Assert.Equal(0, buffer.Tick(9));
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void Drain_CopiesBufferedSamplesAndClearsBuffer()
    {
        SampleBuffer<ApuStereoSample> buffer = new(SourceClockHz, SampleRate);
        var destination = new ApuStereoSample[2];

        buffer.Add(new ApuStereoSample(1, 2));
        buffer.Add(new ApuStereoSample(3, 4));

        Assert.Equal(2, buffer.Drain(destination));
        Assert.Equal([new ApuStereoSample(1, 2), new ApuStereoSample(3, 4)], destination);
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void Drain_PreservesSamplesThatDoNotFit()
    {
        SampleBuffer<ApuStereoSample> buffer = new(SourceClockHz, SampleRate);
        var firstDrain = new ApuStereoSample[1];
        var secondDrain = new ApuStereoSample[2];

        buffer.Add(new ApuStereoSample(1, 2));
        buffer.Add(new ApuStereoSample(3, 4));
        buffer.Add(new ApuStereoSample(5, 6));

        Assert.Equal(1, buffer.Drain(firstDrain));
        Assert.Equal([new ApuStereoSample(1, 2)], firstDrain);
        Assert.Equal(2, buffer.Count);
        Assert.Equal(2, buffer.Drain(secondDrain));
        Assert.Equal([new ApuStereoSample(3, 4), new ApuStereoSample(5, 6)], secondDrain);
    }

    [Fact]
    public void Drain_ReturnsZeroWhenEmpty()
    {
        SampleBuffer<ApuStereoSample> buffer = new(SourceClockHz, SampleRate);
        Span<ApuStereoSample> destination = stackalloc ApuStereoSample[1];

        Assert.Equal(0, buffer.Drain(destination));
    }

    [Fact]
    public void Add_DropsOldestSampleWhenFull()
    {
        SampleBuffer<ApuStereoSample> buffer = new(SourceClockHz, SampleRate, capacity: 2);
        var destination = new ApuStereoSample[2];

        buffer.Add(new ApuStereoSample(1, 2));
        buffer.Add(new ApuStereoSample(3, 4));
        buffer.Add(new ApuStereoSample(5, 6));

        Assert.Equal(2, buffer.Count);
        Assert.Equal(2, buffer.Drain(destination));
        Assert.Equal([new ApuStereoSample(3, 4), new ApuStereoSample(5, 6)], destination);
    }

    [Fact]
    public void Add_PreservesPlaybackOrderAfterRingWrap()
    {
        SampleBuffer<ApuStereoSample> buffer = new(SourceClockHz, SampleRate, capacity: 3);
        Span<ApuStereoSample> discard = stackalloc ApuStereoSample[2];
        var destination = new ApuStereoSample[2];

        buffer.Add(new ApuStereoSample(1, 2));
        buffer.Add(new ApuStereoSample(3, 4));
        buffer.Drain(discard);
        buffer.Add(new ApuStereoSample(5, 6));
        buffer.Add(new ApuStereoSample(7, 8));

        Assert.Equal(2, buffer.Drain(destination));
        Assert.Equal([new ApuStereoSample(5, 6), new ApuStereoSample(7, 8)], destination);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNonPositiveSourceClock(int sourceClockHz)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SampleBuffer<ApuStereoSample>(sourceClockHz, SampleRate)
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNonPositiveSampleRate(int sampleRate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SampleBuffer<ApuStereoSample>(SourceClockHz, sampleRate)
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNonPositiveCapacity(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SampleBuffer<ApuStereoSample>(SourceClockHz, SampleRate, capacity)
        );
    }

    [Fact]
    public void State_RestoresWrappedSamplesAndNextSampleBoundary()
    {
        SampleBuffer<ApuStereoSample> source = new(SourceClockHz, SampleRate, capacity: 3);
        Span<ApuStereoSample> discarded = stackalloc ApuStereoSample[1];

        source.Add(new ApuStereoSample(1, 2));
        source.Add(new ApuStereoSample(3, 4));
        source.Drain(discarded);
        source.Add(new ApuStereoSample(5, 6));
        source.Add(new ApuStereoSample(7, 8));
        Assert.Equal(0, source.Tick(9));

        var state = source.CaptureState();
        SampleBuffer<ApuStereoSample> restored = new(SourceClockHz, SampleRate, capacity: 3);
        var destination = new ApuStereoSample[3];

        restored.RestoreState(state);

        Assert.Equal(3, restored.Drain(destination));
        Assert.Equal(
            [new ApuStereoSample(3, 4), new ApuStereoSample(5, 6), new ApuStereoSample(7, 8)],
            destination
        );
        Assert.Equal(1, restored.Tick(1));
    }

    [Fact]
    public void State_CaptureAndRestoreDoNotAliasBufferedSamples()
    {
        SampleBuffer<ApuStereoSample> source = new(SourceClockHz, SampleRate);
        source.Add(new ApuStereoSample(1, 2));
        var state = source.CaptureState();
        var sourceDestination = new ApuStereoSample[1];

        state.BufferedSamples[0] = new ApuStereoSample(3, 4);

        Assert.Equal(1, source.Drain(sourceDestination));
        Assert.Equal([new ApuStereoSample(1, 2)], sourceDestination);

        SampleBuffer<ApuStereoSample> restored = new(SourceClockHz, SampleRate);
        restored.RestoreState(state);
        state.BufferedSamples[0] = new ApuStereoSample(5, 6);
        var restoredDestination = new ApuStereoSample[1];

        Assert.Equal(1, restored.Drain(restoredDestination));
        Assert.Equal([new ApuStereoSample(3, 4)], restoredDestination);
    }

    [Fact]
    public void State_RestoreRejectsMalformedInputWithoutChangingBuffer()
    {
        SampleBuffer<ApuStereoSample> buffer = new(SourceClockHz, SampleRate, capacity: 2);
        buffer.Add(new ApuStereoSample(1, 2));
        Assert.Equal(0, buffer.Tick(9));

        Assert.Throws<ArgumentException>(() =>
            buffer.ValidateState(new SampleBufferState<ApuStereoSample>(null!, 0))
        );
        Assert.Throws<ArgumentException>(() =>
            buffer.ValidateState(
                new SampleBufferState<ApuStereoSample>(
                    [new ApuStereoSample(), new ApuStereoSample(), new ApuStereoSample()],
                    0
                )
            )
        );
        Assert.Throws<ArgumentException>(() =>
            buffer.ValidateState(new SampleBufferState<ApuStereoSample>([], -1))
        );
        Assert.Throws<ArgumentException>(() =>
            buffer.RestoreState(new SampleBufferState<ApuStereoSample>([], SourceClockHz))
        );

        var destination = new ApuStereoSample[1];
        Assert.Equal(1, buffer.Drain(destination));
        Assert.Equal([new ApuStereoSample(1, 2)], destination);
        Assert.Equal(1, buffer.Tick(1));
    }
}
