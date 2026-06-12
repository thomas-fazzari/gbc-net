using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using GbcNet.Core.Ppu;
using GbcNet.Gui.Rendering.Palettes;

namespace GbcNet.Gui.Rendering;

/// <summary>
/// Renders immutable core LCD frames into a reusable Avalonia bitmap.
/// </summary>
internal sealed class LcdFrameBitmapRenderer : IDisposable
{
    private const int BytesPerPixel = 4;
    private const double Dpi = 96;

    private readonly WriteableBitmap?[] _bitmaps = new WriteableBitmap?[2];
    private int _nextBitmapIndex;

    public unsafe WriteableBitmap Render(LcdFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var bitmap = GetNextBitmap(frame.Width, frame.Height);

        using var framebuffer = bitmap.Lock();
        var bgraLength = checked(framebuffer.RowBytes * frame.Height);
        WritePixels(
            frame,
            destination: new Span<byte>(framebuffer.Address.ToPointer(), bgraLength),
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

        var pixelSize = new PixelSize(width, height);
        var bitmap = _bitmaps[index];

        if (bitmap?.PixelSize == pixelSize)
        {
            return bitmap;
        }

        bitmap?.Dispose();
        bitmap = new WriteableBitmap(
            pixelSize,
            new Vector(Dpi, Dpi),
            PixelFormats.Bgra8888,
            AlphaFormat.Opaque
        );
        _bitmaps[index] = bitmap;
        return bitmap;
    }

    internal static void WritePixels(LcdFrame frame, Span<byte> destination, int rowBytes)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentOutOfRangeException.ThrowIfLessThan(rowBytes, frame.Width * BytesPerPixel);

        var requiredLength = checked(rowBytes * frame.Height);

        if (destination.Length < requiredLength)
        {
            throw new ArgumentException("Destination buffer is too small.", nameof(destination));
        }

        switch (frame.PixelFormat)
        {
            case LcdPixelFormat.DmgShadeIndex8:
                WriteDmgShadePixels(frame, destination, rowBytes);
                return;

            case LcdPixelFormat.Rgb555Le:
                WriteRgb555Pixels(frame, destination, rowBytes);
                return;

            default:
                throw new NotSupportedException(
                    $"Unsupported LCD pixel format: {frame.PixelFormat}."
                );
        }
    }

    private static void WriteDmgShadePixels(LcdFrame frame, Span<byte> destination, int rowBytes)
    {
        var shades = frame.Pixels.Span;
        var colors = DmgLcdPalette.Bgra;

        for (var y = 0; y < frame.Height; y++)
        {
            var sourceRowOffset = y * frame.Width;
            var targetRowOffset = y * rowBytes;

            for (var x = 0; x < frame.Width; x++)
            {
                var targetOffset = targetRowOffset + (x * BytesPerPixel);
                var colorOffset =
                    (shades[sourceRowOffset + x] > 3 ? 3 : shades[sourceRowOffset + x])
                    * BytesPerPixel;
                destination[targetOffset] = colors[colorOffset];
                destination[targetOffset + 1] = colors[colorOffset + 1];
                destination[targetOffset + 2] = colors[colorOffset + 2];
                destination[targetOffset + 3] = colors[colorOffset + 3];
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
                var color = pixels[sourceOffset] | (pixels[sourceOffset + 1] << 8);
                var targetOffset = targetRowOffset + (x * BytesPerPixel);

                destination[targetOffset] = ExpandRgb555Channel((color >> 10) & 0x1F);
                destination[targetOffset + 1] = ExpandRgb555Channel((color >> 5) & 0x1F);
                destination[targetOffset + 2] = ExpandRgb555Channel(color & 0x1F);
                destination[targetOffset + 3] = 0xFF;
            }
        }
    }

    private static byte ExpandRgb555Channel(int value) => (byte)((value << 3) | (value >> 2));
}
