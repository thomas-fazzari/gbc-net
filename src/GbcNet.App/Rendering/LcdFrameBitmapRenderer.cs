// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using GbcNet.Core.Ppu;

namespace GbcNet.App.Rendering;

/// <summary>
/// Renders immutable core LCD frames into a reusable Avalonia bitmap.
/// </summary>
internal sealed class LcdFrameBitmapRenderer : IDisposable
{
    private const int BytesPerPixel = 4;
    private const double Dpi = 96;

    private static ReadOnlySpan<byte> DmgPaletteBgra =>
        [
            0xD0,
            0xF8,
            0xE0,
            0xFF,
            0x70,
            0xC0,
            0x88,
            0xFF,
            0x56,
            0x68,
            0x34,
            0xFF,
            0x18,
            0x18,
            0x08,
            0xFF,
        ];

    private readonly WriteableBitmap?[] _bitmaps = new WriteableBitmap?[2];
    private int _nextBitmapIndex;

    public unsafe WriteableBitmap Render(LcdFrame frame)
    {
        ArgumentNullException.ThrowIfNull(argument: frame);

        var bitmap = GetNextBitmap(width: frame.Width, height: frame.Height);

        using var framebuffer = bitmap.Lock();
        var bgraLength = checked(framebuffer.RowBytes * frame.Height);
        WritePixels(
            frame: frame,
            destination: new Span<byte>(
                pointer: framebuffer.Address.ToPointer(),
                length: bgraLength
            ),
            rowBytes: framebuffer.RowBytes
        );
        return bitmap;
    }

    public void Dispose()
    {
        for (var index = 0; index < _bitmaps.Length; index++)
        {
            _bitmaps[index]?.Dispose();
            _bitmaps[index] = null;
        }
    }

    private WriteableBitmap GetNextBitmap(int width, int height)
    {
        var index = _nextBitmapIndex;
        _nextBitmapIndex = (_nextBitmapIndex + 1) % _bitmaps.Length;

        var pixelSize = new PixelSize(width: width, height: height);
        var bitmap = _bitmaps[index];

        if (bitmap?.PixelSize == pixelSize)
        {
            return bitmap;
        }

        bitmap?.Dispose();
        bitmap = new WriteableBitmap(
            size: pixelSize,
            dpi: new Vector(x: Dpi, y: Dpi),
            format: PixelFormats.Bgra8888,
            alphaFormat: AlphaFormat.Opaque
        );
        _bitmaps[index] = bitmap;
        return bitmap;
    }

    internal static void WritePixels(LcdFrame frame, Span<byte> destination, int rowBytes)
    {
        ArgumentNullException.ThrowIfNull(argument: frame);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            value: rowBytes,
            other: frame.Width * BytesPerPixel
        );

        var requiredLength = checked(rowBytes * frame.Height);

        if (destination.Length < requiredLength)
        {
            throw new ArgumentException(
                message: "Destination buffer is too small.",
                paramName: nameof(destination)
            );
        }

        switch (frame.PixelFormat)
        {
            case LcdPixelFormat.DmgShadeIndex8:
                WriteDmgShadePixels(frame: frame, destination: destination, rowBytes: rowBytes);
                return;

            case LcdPixelFormat.Rgb555Le:
                WriteRgb555Pixels(frame: frame, destination: destination, rowBytes: rowBytes);
                return;

            default:
                throw new NotSupportedException(
                    message: $"Unsupported LCD pixel format: {frame.PixelFormat}."
                );
        }
    }

    private static void WriteDmgShadePixels(LcdFrame frame, Span<byte> destination, int rowBytes)
    {
        var shades = frame.Pixels.Span;
        var colors = DmgPaletteBgra;

        for (var y = 0; y < frame.Height; y++)
        {
            var sourceRowOffset = y * frame.Width;
            var targetRowOffset = y * rowBytes;

            for (var x = 0; x < frame.Width; x++)
            {
                var targetOffset = targetRowOffset + (x * BytesPerPixel);
                var colorOffset =
                    (
                        shades[index: sourceRowOffset + x] > 3
                            ? 3
                            : shades[index: sourceRowOffset + x]
                    ) * BytesPerPixel;
                destination[index: targetOffset] = colors[index: colorOffset];
                destination[index: targetOffset + 1] = colors[index: colorOffset + 1];
                destination[index: targetOffset + 2] = colors[index: colorOffset + 2];
                destination[index: targetOffset + 3] = colors[index: colorOffset + 3];
            }
        }
    }

    private static void WriteRgb555Pixels(LcdFrame frame, Span<byte> destination, int rowBytes)
    {
        var pixels = frame.Pixels.Span;

        for (var y = 0; y < frame.Height; y++)
        {
            var sourceRowOffset = y * frame.Width * 2;
            var targetRowOffset = y * rowBytes;

            for (var x = 0; x < frame.Width; x++)
            {
                var sourceOffset = sourceRowOffset + (x * 2);
                var color = pixels[index: sourceOffset] | (pixels[index: sourceOffset + 1] << 8);
                var targetOffset = targetRowOffset + (x * BytesPerPixel);

                destination[index: targetOffset] = ExpandRgb555Channel(value: (color >> 10) & 0x1F);
                destination[index: targetOffset + 1] = ExpandRgb555Channel(
                    value: (color >> 5) & 0x1F
                );
                destination[index: targetOffset + 2] = ExpandRgb555Channel(value: color & 0x1F);
                destination[index: targetOffset + 3] = 0xFF;
            }
        }
    }

    private static byte ExpandRgb555Channel(int value) => (byte)((value << 3) | (value >> 2));
}
