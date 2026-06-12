using System.Globalization;
using System.Security.Cryptography;
using GbcNet.Core.Hardware;
using GbcNet.Core.Ppu;
using GbcNet.Tests.RomTesting.Utils;

namespace GbcNet.Tests.RomTesting.Visual;

[Collection<RomTestingGroup>]
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
        SkipIfVisualAssetsAreMissing();

        var rom = File.ReadAllBytes(RomPath);
        var expectedPixels = File.ReadAllBytes(GoldenPath);
        Assert.Equal(ExpectedRomSha256, ComputeSha256(rom));
        Assert.Equal(ExpectedGoldenSha256, ComputeSha256(expectedPixels));
        Assert.Equal(ExpectedPixelByteCount, expectedPixels.Length);

        var result = VisualRomTestRunner.RunToFrame(
            rom,
            TargetFrame,
            MaxMachineCycles,
            HardwareModel.Cgb
        );

        Assert.NotNull(result.Frame);
        Assert.Equal(LcdPixelFormat.Rgb555Le, result.Frame.PixelFormat);
        Assert.True(
            expectedPixels.AsSpan().SequenceEqual(result.Frame.Pixels.Span),
            CgbAcid2FrameDifference.CreateMessage(result, expectedPixels)
        );
    }

    private static void SkipIfVisualAssetsAreMissing()
    {
        if (!File.Exists(RomPath) || !File.Exists(GoldenPath))
        {
            Assert.Skip(
                "Missing cgb-acid2 visual assets. Add cgb-acid2.gbc and cgb-acid2.rgb555le.bin."
            );
        }
    }

    private static string ComputeSha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));

    private static class CgbAcid2FrameDifference
    {
        public static string CreateMessage(VisualRomTestResult result, ReadOnlySpan<byte> expected)
        {
            if (result.Frame is null)
            {
                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"No frame completed after {result.MachineCycles} M-cycles."
                );
            }

            var actual = result.Frame.Pixels.Span;
            var comparedLength = Math.Min(expected.Length, actual.Length);
            var unmatchedLength = Math.Abs(expected.Length - actual.Length);
            var differenceCount = unmatchedLength;
            var firstDifferences = new string[MaxReportedDiffOffsets];
            var reportedDifferenceCount = 0;

            for (var offset = 0; offset < comparedLength; offset++)
            {
                if (expected[offset] == actual[offset])
                {
                    continue;
                }

                if (reportedDifferenceCount < firstDifferences.Length)
                {
                    firstDifferences[reportedDifferenceCount] = FormatDifference(
                        offset,
                        expected[offset],
                        actual[offset]
                    );
                    reportedDifferenceCount++;
                }

                differenceCount++;
            }

            return string.Create(
                CultureInfo.InvariantCulture,
                $"Frame {result.CompletedFrames} differs at {differenceCount} bytes after {result.MachineCycles} M-cycles. Expected length={expected.Length}, actual length={actual.Length}, unmatched tail={unmatchedLength}. First differences: {string.Join(", ", firstDifferences.AsSpan()[..reportedDifferenceCount])}."
            );
        }

        private static string FormatDifference(int offset, byte expected, byte actual)
        {
            var pixel = offset / 2;
            var y = Math.DivRem(pixel, PpuGeometry.FrameWidth, out var x);
            return string.Create(
                CultureInfo.InvariantCulture,
                $"({x},{y}) byte{offset & 1} exp={expected:X2} act={actual:X2}"
            );
        }
    }
}
