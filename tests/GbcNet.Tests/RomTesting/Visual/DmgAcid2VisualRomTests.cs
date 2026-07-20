// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Security.Cryptography;
using GbcNet.Core.Hardware;
using GbcNet.Core.Ppu;
using GbcNet.Tests.RomTesting.Utils;

namespace GbcNet.Tests.RomTesting.Visual;

[Collection<RomTestSuite>]
public sealed class DmgAcid2VisualRomTests
{
    private const string RomPath = "RomTesting/Resources/Visual/dmg-acid2/dmg-acid2.gb";
    private const string DmgGoldenPath =
        "RomTesting/Resources/Visual/dmg-acid2/dmg-acid2.dmgshade.bin";
    private const string CgbGoldenPath =
        "RomTesting/Resources/Visual/dmg-acid2/dmg-acid2-cgb.rgb555le.bin";

    private const string ExpectedRomSha256 =
        "464E14B7D42E7FEEA0B7EDE42BE7071DC88913F75B9FFA444299424B63D1DFF1";
    private const string ExpectedDmgGoldenSha256 =
        "F844EA760A6F1FE137F7F992C7AB1C72D34C7FCD3A807B4174A78EB04A32A458";
    private const string ExpectedCgbGoldenSha256 =
        "2EFAA8986C80AC0EBDFCF88EE6DB7AA745121EDC2C0F03BFBDD84D1B2537532A";

    private const int TargetFrame = 120;
    private const int MaxMachineCycles = 20_000_000;
    private const int ExpectedDmgPixelCount = PpuGeometry.FrameWidth * PpuGeometry.FrameHeight;
    private const int ExpectedCgbPixelByteCount =
        PpuGeometry.FrameWidth * PpuGeometry.FrameHeight * 2;
    private const int MaxReportedDiffOffsets = 16;

    [Fact]
    public void DmgAcid2FrameMatchesGolden()
    {
        var rom = File.ReadAllBytes(RomPath);
        var expectedPixels = File.ReadAllBytes(DmgGoldenPath);
        Assert.Equal(ExpectedRomSha256, ComputeSha256(rom));
        Assert.Equal(ExpectedDmgGoldenSha256, ComputeSha256(expectedPixels));
        Assert.Equal(ExpectedDmgPixelCount, expectedPixels.Length);

        using var result = VisualRomTestRunner.RunToFrame(rom, TargetFrame, MaxMachineCycles);

        Assert.NotNull(result.Frame);
        Assert.Equal(LcdPixelFormat.DmgShadeIndex8, result.Frame.PixelFormat);
        Assert.True(
            expectedPixels.AsSpan().SequenceEqual(result.Frame.Pixels.Span),
            Rgb555FrameDifference.CreateDmgShadeIndex8Message(
                result,
                expectedPixels,
                MaxReportedDiffOffsets
            )
        );
    }

    [Fact]
    public void DmgAcid2FrameMatchesCgbCompatibilityGolden()
    {
        var rom = File.ReadAllBytes(RomPath);
        var expectedPixels = File.ReadAllBytes(CgbGoldenPath);
        Assert.Equal(ExpectedRomSha256, ComputeSha256(rom));
        Assert.Equal(ExpectedCgbGoldenSha256, ComputeSha256(expectedPixels));
        Assert.Equal(ExpectedCgbPixelByteCount, expectedPixels.Length);

        using var result = VisualRomTestRunner.RunToFrame(
            rom,
            TargetFrame,
            MaxMachineCycles,
            HardwareModel.Cgb
        );

        Assert.NotNull(result.Frame);
        Assert.Equal(LcdPixelFormat.Rgb555Le, result.Frame.PixelFormat);
        Assert.True(
            expectedPixels.AsSpan().SequenceEqual(result.Frame.Pixels.Span),
            Rgb555FrameDifference.CreateMessage(result, expectedPixels, MaxReportedDiffOffsets)
        );
    }

    private static string ComputeSha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));
}
