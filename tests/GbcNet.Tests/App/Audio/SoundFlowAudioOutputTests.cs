// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Audio;

namespace GbcNet.Tests.App.Audio;

public sealed class SoundFlowAudioOutputTests
{
    [Theory]
    [InlineData(100, false, 1f)]
    [InlineData(50, false, 0.25f)]
    [InlineData(25, false, 0.0625f)]
    [InlineData(100, true, 0f)]
    [InlineData(-1, false, 0f)]
    [InlineData(101, false, 1f)]
    public void CalculateGain_UsesPerceptualCurveAndClamp(
        int volumePercent,
        bool muted,
        float expected
    )
    {
        Assert.Equal(expected, SoundFlowAudioOutput.CalculateGain(volumePercent, muted));
    }
}
