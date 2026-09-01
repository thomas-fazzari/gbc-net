// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers.Binary;
using GbcNet.Core.Ppu;

namespace GbcNet.Tests.Unit;

internal static class Rgb555Assertions
{
    public static void PixelEquals(LcdFrame frame, int pixelIndex, ushort expected)
    {
        frame.PixelFormat.Should().Be(LcdPixelFormat.Rgb555Le);
        BinaryPrimitives
            .ReadUInt16LittleEndian(frame.Pixels.Span.Slice(pixelIndex * 2, 2))
            .Should()
            .Be(expected);
    }
}
