using GbcNet.Core.Apu;
using GbcNet.Core.Apu.Components;

namespace GbcNet.Tests.Apu;

public sealed class SampleBufferTests
{
    [Fact]
    public void Tick_ReturnsDefaultSampleRateAfterOneDmgSecond()
    {
        SampleBuffer buffer = new();

        int samplesDue = buffer.Tick(SampleBuffer.DmgClockHz);

        Assert.Equal(SampleBuffer.DefaultSampleRate, samplesDue);
    }

    [Fact]
    public void Tick_AccumulatesPartialTicksWithoutLosingProgress()
    {
        SampleBuffer buffer = new();

        Assert.Equal(0, buffer.Tick(87));
        Assert.Equal(1, buffer.Tick(1));
    }

    [Fact]
    public void Tick_ReturnsZeroBeforeSampleIsDue()
    {
        SampleBuffer buffer = new();

        Assert.Equal(0, buffer.Tick(87));
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void Add_BuffersMixedSamples()
    {
        SampleBuffer buffer = new();

        buffer.Add(new ApuStereoSample(1, 2));
        buffer.Add(new ApuStereoSample(3, 4));

        Assert.Equal([new ApuStereoSample(1, 2), new ApuStereoSample(3, 4)], buffer.Drain());
    }

    [Fact]
    public void Drain_ReturnsBufferedSamplesAndClearsBuffer()
    {
        SampleBuffer buffer = new();

        buffer.Add(new ApuStereoSample(1, 2));

        Assert.Equal([new ApuStereoSample(1, 2)], buffer.Drain());
        Assert.Empty(buffer.Drain());
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void Add_DropsOldestSampleWhenFull()
    {
        SampleBuffer buffer = new(capacity: 2);

        buffer.Add(new ApuStereoSample(1, 2));
        buffer.Add(new ApuStereoSample(3, 4));
        buffer.Add(new ApuStereoSample(5, 6));

        Assert.Equal(2, buffer.Count);
        Assert.Equal([new ApuStereoSample(3, 4), new ApuStereoSample(5, 6)], buffer.Drain());
    }

    [Fact]
    public void Add_PreservesPlaybackOrderAfterRingWrap()
    {
        SampleBuffer buffer = new(capacity: 3);

        buffer.Add(new ApuStereoSample(1, 2));
        buffer.Add(new ApuStereoSample(3, 4));
        buffer.Drain();
        buffer.Add(new ApuStereoSample(5, 6));
        buffer.Add(new ApuStereoSample(7, 8));

        Assert.Equal([new ApuStereoSample(5, 6), new ApuStereoSample(7, 8)], buffer.Drain());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNonPositiveSampleRate(int sampleRate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SampleBuffer(sampleRate));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNonPositiveCapacity(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SampleBuffer(capacity: capacity));
    }
}
