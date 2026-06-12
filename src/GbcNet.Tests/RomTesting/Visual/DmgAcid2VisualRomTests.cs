using System.Globalization;
using System.Security.Cryptography;
using GbcNet.Core.Ppu;
using GbcNet.Tests.RomTesting.Utils;

namespace GbcNet.Tests.RomTesting.Visual;

[Collection<RomTestingGroup>]
public sealed class DmgAcid2VisualRomTests
{
    private const string RomPath = "RomTesting/Resources/Visual/dmg-acid2/dmg-acid2.gb";
    private const string GoldenPath =
        "RomTesting/Resources/Visual/dmg-acid2/dmg-acid2.dmgshade.bin";

    private const string ExpectedRomSha256 =
        "464E14B7D42E7FEEA0B7EDE42BE7071DC88913F75B9FFA444299424B63D1DFF1";
    private const string ExpectedGoldenSha256 =
        "F844EA760A6F1FE137F7F992C7AB1C72D34C7FCD3A807B4174A78EB04A32A458";

    private const int TargetFrame = 120;
    private const int MaxMachineCycles = 20_000_000;
    private const int ExpectedPixelCount = PpuGeometry.FrameWidth * PpuGeometry.FrameHeight;
    private const int MaxReportedDiffOffsets = 16;

    [Fact]
    public void DmgAcid2FrameMatchesGolden()
    {
        SkipIfVisualAssetsAreMissing();

        var rom = File.ReadAllBytes(RomPath);
        var expectedPixels = File.ReadAllBytes(GoldenPath);
        Assert.Equal(ExpectedRomSha256, ComputeSha256(rom));
        Assert.Equal(ExpectedGoldenSha256, ComputeSha256(expectedPixels));
        Assert.Equal(ExpectedPixelCount, expectedPixels.Length);

        var result = VisualRomTestRunner.RunToFrame(rom, TargetFrame, MaxMachineCycles);

        Assert.NotNull(result.Frame);
        Assert.Equal(LcdPixelFormat.DmgShadeIndex8, result.Frame.PixelFormat);
        Assert.True(
            expectedPixels.AsSpan().SequenceEqual(result.Frame.Pixels.Span),
            DmgAcid2FrameDifference.CreateMessage(result, expectedPixels)
        );
    }

    private static void SkipIfVisualAssetsAreMissing()
    {
        if (!File.Exists(RomPath) || !File.Exists(GoldenPath))
        {
            Assert.Skip(
                "Missing dmg-acid2 visual assets. Add dmg-acid2.gb and dmg-acid2.dmgshade.bin."
            );
        }
    }

    private static string ComputeSha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));

    private static class DmgAcid2FrameDifference
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
                $"Frame {result.CompletedFrames} differs at {differenceCount} pixels after {result.MachineCycles} M-cycles. Expected length={expected.Length}, actual length={actual.Length}, unmatched tail={unmatchedLength}. First differences: {string.Join(", ", firstDifferences.AsSpan()[..reportedDifferenceCount])}."
            );
        }

        private static string FormatDifference(int offset, byte expected, byte actual)
        {
            var y = Math.DivRem(offset, PpuGeometry.FrameWidth, out var x);
            return string.Create(
                CultureInfo.InvariantCulture,
                $"({x},{y}) exp={expected} act={actual}"
            );
        }
    }
}
