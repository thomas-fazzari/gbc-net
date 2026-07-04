// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Ppu;

/// <summary>
/// Immutable LCD frame snapshot emitted after a visible frame is complete.
/// </summary>
public sealed class LcdFrame
{
    internal LcdFrame(int width, int height, LcdPixelFormat pixelFormat, ReadOnlySpan<byte> pixels)
    {
        Width = width;
        Height = height;
        PixelFormat = pixelFormat;
        Pixels = pixels.ToArray();
    }

    /// <summary>
    /// Frame width in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Frame height in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Pixel encoding used by the frame data.
    /// </summary>
    public LcdPixelFormat PixelFormat { get; }

    /// <summary>
    /// Immutable row-major pixel data.
    /// </summary>
    public ReadOnlyMemory<byte> Pixels { get; }
}
