// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Apu;
using GbcNet.Core.Apu.Components;

namespace GbcNet.Tests.Unit.Apu;

public sealed class SampleBufferTests
{
    private const int SourceClockHz = 100;
    private const int SampleRate = 10;

    [Fact]
    public void Tick_ReturnsSampleRateAfterOneSourceSecond()
    {
        SampleBuffer<ApuStereoSample> buffer = new(SourceClockHz, SampleRate);

        var samplesDue = buffer.Tick(SourceClockHz);

        samplesDue.Should().Be(SampleRate);
    }

    [Fact]
    public void Tick_AccumulatesPartialTicksWithoutLosingProgress()
    {
        SampleBuffer<ApuStereoSample> buffer = new(SourceClockHz, SampleRate);

        buffer.Tick(9).Should().Be(0);
        buffer.Tick(1).Should().Be(1);
    }

    [Fact]
    public void Tick_ReturnsZeroBeforeSampleIsDue()
    {
        SampleBuffer<ApuStereoSample> buffer = new(SourceClockHz, SampleRate);

        buffer.Tick(9).Should().Be(0);
        buffer.Count.Should().Be(0);
    }

    [Fact]
    public void Drain_CopiesBufferedSamplesAndClearsBuffer()
    {
        SampleBuffer<ApuStereoSample> buffer = new(SourceClockHz, SampleRate);
        var destination = new ApuStereoSample[2];

        buffer.Add(new ApuStereoSample(1, 2));
        buffer.Add(new ApuStereoSample(3, 4));

        buffer.Drain(destination).Should().Be(2);
        destination.Should().Equal(new ApuStereoSample(1, 2), new ApuStereoSample(3, 4));
        buffer.Count.Should().Be(0);
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

        buffer.Drain(firstDrain).Should().Be(1);
        firstDrain.Should().Equal(new ApuStereoSample(1, 2));
        buffer.Count.Should().Be(2);
        buffer.Drain(secondDrain).Should().Be(2);
        secondDrain.Should().Equal(new ApuStereoSample(3, 4), new ApuStereoSample(5, 6));
    }

    [Fact]
    public void Drain_ReturnsZeroWhenEmpty()
    {
        SampleBuffer<ApuStereoSample> buffer = new(SourceClockHz, SampleRate);
        Span<ApuStereoSample> destination = stackalloc ApuStereoSample[1];

        buffer.Drain(destination).Should().Be(0);
    }

    [Fact]
    public void Add_DropsOldestSampleWhenFull()
    {
        SampleBuffer<ApuStereoSample> buffer = new(SourceClockHz, SampleRate, capacity: 2);
        var destination = new ApuStereoSample[2];

        buffer.Add(new ApuStereoSample(1, 2));
        buffer.Add(new ApuStereoSample(3, 4));
        buffer.Add(new ApuStereoSample(5, 6));

        buffer.Count.Should().Be(2);
        buffer.Drain(destination).Should().Be(2);
        destination.Should().Equal(new ApuStereoSample(3, 4), new ApuStereoSample(5, 6));
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

        buffer.Drain(destination).Should().Be(2);
        destination.Should().Equal(new ApuStereoSample(5, 6), new ApuStereoSample(7, 8));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNonPositiveSourceClock(int sourceClockHz)
    {
        FluentActions
            .Invoking(() => new SampleBuffer<ApuStereoSample>(sourceClockHz, SampleRate))
            .Should()
            .ThrowExactly<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNonPositiveSampleRate(int sampleRate)
    {
        FluentActions
            .Invoking(() => new SampleBuffer<ApuStereoSample>(SourceClockHz, sampleRate))
            .Should()
            .ThrowExactly<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNonPositiveCapacity(int capacity)
    {
        FluentActions
            .Invoking(() => new SampleBuffer<ApuStereoSample>(SourceClockHz, SampleRate, capacity))
            .Should()
            .ThrowExactly<ArgumentOutOfRangeException>();
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
        source.Tick(9).Should().Be(0);

        var state = source.CaptureState();
        SampleBuffer<ApuStereoSample> restored = new(SourceClockHz, SampleRate, capacity: 3);
        var destination = new ApuStereoSample[3];

        restored.RestoreState(state);

        restored.Drain(destination).Should().Be(3);
        destination
            .Should()
            .Equal(new ApuStereoSample(3, 4), new ApuStereoSample(5, 6), new ApuStereoSample(7, 8));
        restored.Tick(1).Should().Be(1);
    }

    [Fact]
    public void State_CaptureAndRestoreDoNotAliasBufferedSamples()
    {
        SampleBuffer<ApuStereoSample> source = new(SourceClockHz, SampleRate);
        source.Add(new ApuStereoSample(1, 2));
        var state = source.CaptureState();
        var sourceDestination = new ApuStereoSample[1];

        state.BufferedSamples[0] = new ApuStereoSample(3, 4);

        source.Drain(sourceDestination).Should().Be(1);
        sourceDestination.Should().Equal(new ApuStereoSample(1, 2));

        SampleBuffer<ApuStereoSample> restored = new(SourceClockHz, SampleRate);
        restored.RestoreState(state);
        state.BufferedSamples[0] = new ApuStereoSample(5, 6);
        var restoredDestination = new ApuStereoSample[1];

        restored.Drain(restoredDestination).Should().Be(1);
        restoredDestination.Should().Equal(new ApuStereoSample(3, 4));
    }

    [Fact]
    public void State_RestoreRejectsMalformedInputWithoutChangingBuffer()
    {
        SampleBuffer<ApuStereoSample> buffer = new(SourceClockHz, SampleRate, capacity: 2);
        buffer.Add(new ApuStereoSample(1, 2));
        buffer.Tick(9).Should().Be(0);

        FluentActions
            .Invoking(() => buffer.ValidateState(new SampleBufferState<ApuStereoSample>(null!, 0)))
            .Should()
            .ThrowExactly<ArgumentException>();
        FluentActions
            .Invoking(() =>
                buffer.ValidateState(
                    new SampleBufferState<ApuStereoSample>(
                        [new ApuStereoSample(), new ApuStereoSample(), new ApuStereoSample()],
                        0
                    )
                )
            )
            .Should()
            .ThrowExactly<ArgumentException>();
        FluentActions
            .Invoking(() => buffer.ValidateState(new SampleBufferState<ApuStereoSample>([], -1)))
            .Should()
            .ThrowExactly<ArgumentException>();
        FluentActions
            .Invoking(() =>
                buffer.RestoreState(new SampleBufferState<ApuStereoSample>([], SourceClockHz))
            )
            .Should()
            .ThrowExactly<ArgumentException>();

        var destination = new ApuStereoSample[1];
        buffer.Drain(destination).Should().Be(1);
        destination.Should().Equal(new ApuStereoSample(1, 2));
        buffer.Tick(1).Should().Be(1);
    }
}
