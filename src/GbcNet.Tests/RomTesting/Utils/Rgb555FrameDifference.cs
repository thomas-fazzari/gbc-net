using System.Globalization;
using GbcNet.Core.Ppu;

namespace GbcNet.Tests.RomTesting.Utils;

internal static class Rgb555FrameDifference
{
    public static string CreateMessage(
        VisualRomTestResult result,
        ReadOnlySpan<byte> expected,
        int maxReportedDiffOffsets
    )
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
        var firstDifferences = new string[maxReportedDiffOffsets];
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
