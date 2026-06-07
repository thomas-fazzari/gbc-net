using GbcNet.Core.Apu;
using GbcNet.Core.Apu.Components;
using GbcNet.Core.Apu.Profiles;

namespace GbcNet.Tests.Apu;

public sealed class ApuOutputFilterTests
{
    [Fact]
    public void Filter_ConstantInputDecaysTowardZero()
    {
        ApuOutputFilter filter = new(
            new DmgApuHardwareProfile().GetOutputHighPassChargeFactor(
                ApuSampleTiming.DefaultSampleRate
            )
        );

        int first = filter.Filter(new ApuAnalogStereoSample(1, 1), anyDacEnabled: true).Left;
        int later = 0;
        for (int sample = 0; sample < 1_000; sample++)
        {
            later = filter.Filter(new ApuAnalogStereoSample(1, 1), anyDacEnabled: true).Left;
        }

        Assert.True(Math.Abs(later) < Math.Abs(first));
    }

    [Fact]
    public void Filter_AllDacsOffOutputsZeroAndResetsCapacitor()
    {
        ApuOutputFilter filter = new(
            new DmgApuHardwareProfile().GetOutputHighPassChargeFactor(
                ApuSampleTiming.DefaultSampleRate
            )
        );

        ApuStereoSample first = filter.Filter(new ApuAnalogStereoSample(1, 1), anyDacEnabled: true);
        ApuStereoSample off = filter.Filter(new ApuAnalogStereoSample(1, 1), anyDacEnabled: false);
        ApuStereoSample afterReset = filter.Filter(
            new ApuAnalogStereoSample(1, 1),
            anyDacEnabled: true
        );

        Assert.Equal(default, off);
        Assert.Equal(first, afterReset);
    }

    [Fact]
    public void Filter_LeftAndRightHighPassStateIsIndependent()
    {
        ApuOutputFilter filter = new(
            new DmgApuHardwareProfile().GetOutputHighPassChargeFactor(
                ApuSampleTiming.DefaultSampleRate
            )
        );

        ApuStereoSample leftOnly = filter.Filter(
            new ApuAnalogStereoSample(1, 0),
            anyDacEnabled: true
        );
        ApuStereoSample rightOnly = filter.Filter(
            new ApuAnalogStereoSample(0, 1),
            anyDacEnabled: true
        );

        Assert.NotEqual(0, leftOnly.Left);
        Assert.Equal(0, leftOnly.Right);
        Assert.NotEqual(0, rightOnly.Right);
    }

    [Theory]
    [InlineData(-ApuOutputFilter.MaxAnalogMixerOutput)]
    [InlineData(ApuOutputFilter.MaxAnalogMixerOutput)]
    public void Filter_ScalingStaysBoundedForMixerExtremes(double analogSample)
    {
        ApuOutputFilter filter = new(
            new DmgApuHardwareProfile().GetOutputHighPassChargeFactor(
                ApuSampleTiming.DefaultSampleRate
            )
        );

        ApuStereoSample sample = filter.Filter(
            new ApuAnalogStereoSample(analogSample, analogSample),
            anyDacEnabled: true
        );

        Assert.InRange(sample.Left, short.MinValue, short.MaxValue);
        Assert.InRange(sample.Right, short.MinValue, short.MaxValue);
    }
}
