// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Audio;
using GbcNet.Core.Apu;

namespace GbcNet.Tests.Unit.App.Audio;

public sealed class SdlAudioOutputTests
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
        SdlAudioOutput.CalculateGain(volumePercent, muted).Should().Be(expected);
    }

    [Fact]
    public void ConvertSamples_SaturatesAndInterleavesStereoFrames()
    {
        ApuStereoSample[] samples =
        [
            new(int.MinValue, int.MaxValue),
            new(short.MinValue, short.MaxValue),
            new(short.MinValue - 1, short.MaxValue + 1),
            new(-1, 2),
        ];
        var destination = new short[samples.Length * 2];

        SdlAudioOutput.ConvertSamples(samples, destination);

        destination
            .Should()
            .Equal(
                short.MinValue,
                short.MaxValue,
                short.MinValue,
                short.MaxValue,
                short.MinValue,
                short.MaxValue,
                -1,
                2
            );
    }

    [Theory]
    [InlineData(0, 24_001, 24_000)]
    [InlineData(95_996, 2, 1)]
    [InlineData(96_000, 1, 0)]
    [InlineData(100_000, 1, 0)]
    public void GetFrameCountToQueue_DropsOnlyNewFramesAtFiveHundredMillisecondLimit(
        int queuedByteCount,
        int requestedFrameCount,
        int expectedFrameCount
    )
    {
        SdlAudioOutput
            .GetFrameCountToQueue(queuedByteCount, requestedFrameCount)
            .Should()
            .Be(expectedFrameCount);
    }
}
