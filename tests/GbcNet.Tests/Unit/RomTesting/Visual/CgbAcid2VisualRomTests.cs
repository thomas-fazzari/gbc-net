// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Hardware;
using GbcNet.Core.Ppu;
using GbcNet.Tests.Unit.RomTesting.Utils;

namespace GbcNet.Tests.Unit.RomTesting.Visual;

[Collection<RomTestCollectionDefinition>]
public sealed class CgbAcid2VisualRomTests
{
    private const string RomPath = "RomTesting/Resources/Visual/cgb-acid2/cgb-acid2.gbc";
    private const string GoldenPath =
        "RomTesting/Resources/Visual/cgb-acid2/cgb-acid2.rgb555le.bin";

    private const string ExpectedRomSha256 =
        "197FB0BCEC544F0400527FC707E0A94F55435974986E6986B424ACE5DE81720E";
    private const string ExpectedGoldenSha256 =
        "C587A0E67F4A9E7CECCFC3B1C1991510A6476BD6B4A8B2F109F83E94F97116CB";

    private const int TargetFrame = 600;
    private const int MaxMachineCycles = 20_000_000;
    private const int ExpectedPixelByteCount = PpuGeometry.FrameWidth * PpuGeometry.FrameHeight * 2;
    private const int MaxReportedDiffOffsets = 16;

    [Fact]
    public void CgbAcid2FrameMatchesSameBoyGolden()
    {
        var rom = File.ReadAllBytes(RomPath);
        var expectedPixels = File.ReadAllBytes(GoldenPath);
        RomTestHashing.ComputeSha256(rom).Should().Be(ExpectedRomSha256);
        RomTestHashing.ComputeSha256(expectedPixels).Should().Be(ExpectedGoldenSha256);
        expectedPixels.Length.Should().Be(ExpectedPixelByteCount);

        using var result = VisualRomTestRunner.RunToFrame(
            rom,
            TargetFrame,
            MaxMachineCycles,
            HardwareModel.Cgb
        );

        result.Frame.Should().NotBeNull();
        result.Frame.PixelFormat.Should().Be(LcdPixelFormat.Rgb555Le);
        expectedPixels
            .AsSpan()
            .SequenceEqual(result.Frame.Pixels.Span)
            .Should()
            .BeTrue(
                FrameDifferenceReporter.CreateMessage(
                    result,
                    expectedPixels,
                    MaxReportedDiffOffsets
                )
            );
    }
}
