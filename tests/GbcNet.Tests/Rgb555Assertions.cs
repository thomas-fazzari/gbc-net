// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers.Binary;
using GbcNet.Core.Ppu;

namespace GbcNet.Tests;

internal static class Rgb555Assertions
{
    public static void PixelEquals(LcdFrame frame, int pixelIndex, ushort expected)
    {
        Assert.Equal(LcdPixelFormat.Rgb555Le, frame.PixelFormat);
        Assert.Equal(
            expected,
            BinaryPrimitives.ReadUInt16LittleEndian(frame.Pixels.Span.Slice(pixelIndex * 2, 2))
        );
    }
}
