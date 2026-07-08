// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using GbcNet.Core.Ppu;

namespace GbcNet.Tests.RomTesting.Utils;

internal static class Rgb555FrameDifference
{
    public static string CreateMessage(
        VisualRomTestResult result,
        ReadOnlySpan<byte> expected,
        int maxReportedDiffOffsets
    ) => CreateMessage(result, expected, maxReportedDiffOffsets, bytesPerPixel: 2);

    public static string CreateDmgShadeIndex8Message(
        VisualRomTestResult result,
        ReadOnlySpan<byte> expected,
        int maxReportedDiffOffsets
    ) => CreateMessage(result, expected, maxReportedDiffOffsets, bytesPerPixel: 1);

    private static string CreateMessage(
        VisualRomTestResult result,
        ReadOnlySpan<byte> expected,
        int maxReportedDiffOffsets,
        int bytesPerPixel
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
                    actual[offset],
                    bytesPerPixel
                );
                reportedDifferenceCount++;
            }

            differenceCount++;
        }

        var unit = bytesPerPixel == 1 ? "pixels" : "bytes";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Frame {result.CompletedFrames} differs at {differenceCount} {unit} after {result.MachineCycles} M-cycles. Expected length={expected.Length}, actual length={actual.Length}, unmatched tail={unmatchedLength}. First differences: {string.Join(", ", firstDifferences.AsSpan()[..reportedDifferenceCount])}."
        );
    }

    private static string FormatDifference(
        int offset,
        byte expected,
        byte actual,
        int bytesPerPixel
    )
    {
        var pixel = offset / bytesPerPixel;
        var y = Math.DivRem(pixel, PpuGeometry.FrameWidth, out var x);

        return bytesPerPixel == 1
            ? string.Create(CultureInfo.InvariantCulture, $"({x},{y}) exp={expected} act={actual}")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"({x},{y}) byte{offset & 1} exp={expected:X2} act={actual:X2}"
            );
    }
}
