// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers.Binary;
using GbcNet.Core.Ppu;

namespace GbcNet.Tests.Unit;

/// <summary>
/// Checks pixels stored as little-endian packed RGB555 values.
/// </summary>
internal static class Rgb555Assertions
{
    /// <summary>
    /// Checks the pixel format and the 16-bit value at a zero-based pixel index.
    /// </summary>
    public static void PixelEquals(LcdFrame frame, int pixelIndex, ushort expected)
    {
        frame.PixelFormat.Should().Be(LcdPixelFormat.Rgb555Le);
        BinaryPrimitives
            .ReadUInt16LittleEndian(frame.Pixels.Span.Slice(pixelIndex * 2, 2))
            .Should()
            .Be(expected);
    }
}
