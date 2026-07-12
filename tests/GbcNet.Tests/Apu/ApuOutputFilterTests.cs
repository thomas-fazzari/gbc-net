// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Apu;
using GbcNet.Core.Apu.Components;

namespace GbcNet.Tests.Apu;

public sealed class ApuOutputFilterTests
{
    [Fact]
    public void Filter_ConstantInputDecaysTowardZero()
    {
        ApuOutputFilter filter = new(
            ApuModelSpec.Dmg.GetOutputHighPassChargeFactor(ApuSampleTiming.DefaultSampleRate)
        );

        var first = filter.Filter(new ApuAnalogStereoSample(1, 1), anyDacEnabled: true).Left;
        var later = 0;
        for (var sample = 0; sample < 1_000; sample++)
        {
            later = filter.Filter(new ApuAnalogStereoSample(1, 1), anyDacEnabled: true).Left;
        }

        Assert.True(Math.Abs(later) < Math.Abs(first));
    }

    [Fact]
    public void Filter_AllDacsOffOutputsZeroAndResetsCapacitor()
    {
        ApuOutputFilter filter = new(
            ApuModelSpec.Dmg.GetOutputHighPassChargeFactor(ApuSampleTiming.DefaultSampleRate)
        );

        var first = filter.Filter(new ApuAnalogStereoSample(1, 1), anyDacEnabled: true);
        var off = filter.Filter(new ApuAnalogStereoSample(1, 1), anyDacEnabled: false);
        var afterReset = filter.Filter(new ApuAnalogStereoSample(1, 1), anyDacEnabled: true);

        Assert.Equal(default, off);
        Assert.Equal(first, afterReset);
    }

    [Fact]
    public void Filter_LeftAndRightHighPassStateIsIndependent()
    {
        ApuOutputFilter filter = new(
            ApuModelSpec.Dmg.GetOutputHighPassChargeFactor(ApuSampleTiming.DefaultSampleRate)
        );

        var leftOnly = filter.Filter(new ApuAnalogStereoSample(1, 0), anyDacEnabled: true);
        var rightOnly = filter.Filter(new ApuAnalogStereoSample(0, 1), anyDacEnabled: true);

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
            ApuModelSpec.Dmg.GetOutputHighPassChargeFactor(ApuSampleTiming.DefaultSampleRate)
        );

        var sample = filter.Filter(
            new ApuAnalogStereoSample(analogSample, analogSample),
            anyDacEnabled: true
        );

        Assert.InRange(sample.Left, short.MinValue, short.MaxValue);
        Assert.InRange(sample.Right, short.MinValue, short.MaxValue);
    }

    [Fact]
    public void State_RestoreContinuesStereoHighPassDecay()
    {
        var chargeFactor = ApuModelSpec.Dmg.GetOutputHighPassChargeFactor(
            ApuSampleTiming.DefaultSampleRate
        );
        ApuOutputFilter source = new(chargeFactor);
        source.Filter(new ApuAnalogStereoSample(24, -16), anyDacEnabled: true);
        source.Filter(new ApuAnalogStereoSample(12, 8), anyDacEnabled: true);
        var state = source.CaptureState();

        ApuOutputFilter restored = new(chargeFactor);
        restored.RestoreState(state);

        var expected = source.Filter(new ApuAnalogStereoSample(6, -4), anyDacEnabled: true);
        var actual = restored.Filter(new ApuAnalogStereoSample(6, -4), anyDacEnabled: true);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void State_RestoreRejectsMalformedInputWithoutChangingCapacitors()
    {
        var chargeFactor = ApuModelSpec.Dmg.GetOutputHighPassChargeFactor(
            ApuSampleTiming.DefaultSampleRate
        );
        ApuOutputFilter filter = new(chargeFactor);
        ApuOutputFilter expected = new(chargeFactor);
        var priorSample = new ApuAnalogStereoSample(12, -8);
        filter.Filter(priorSample, anyDacEnabled: true);
        expected.Filter(priorSample, anyDacEnabled: true);

        Assert.Throws<ArgumentException>(() =>
            ApuOutputFilter.ValidateState(new ApuOutputFilterState(double.NaN, 0))
        );
        Assert.Throws<ArgumentException>(() =>
            ApuOutputFilter.ValidateState(new ApuOutputFilterState(0, double.PositiveInfinity))
        );
        Assert.Throws<ArgumentException>(() =>
            filter.RestoreState(
                new ApuOutputFilterState(ApuOutputFilter.MaxAnalogMixerOutput + 1, 0)
            )
        );

        var nextSample = new ApuAnalogStereoSample(6, -4);
        Assert.Equal(
            expected.Filter(nextSample, anyDacEnabled: true),
            filter.Filter(nextSample, anyDacEnabled: true)
        );
    }
}
