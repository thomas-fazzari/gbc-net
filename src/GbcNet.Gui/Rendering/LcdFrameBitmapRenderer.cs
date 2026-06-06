using System.Runtime.InteropServices;
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
    private byte[] _bgraPixels = [];
    private int _nextBitmapIndex;

    public WriteableBitmap Render(LcdFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (frame.PixelFormat is not LcdPixelFormat.DmgShadeIndex8)
        {
            throw new NotSupportedException("Only DMG shade index frames are supported.");
        }

        WriteableBitmap bitmap = GetNextBitmap(frame.Width, frame.Height);

        using ILockedFramebuffer framebuffer = bitmap.Lock();
        int bgraLength = checked(framebuffer.RowBytes * frame.Height);

        if (_bgraPixels.Length != bgraLength)
        {
            _bgraPixels = new byte[bgraLength];
        }

        WritePixels(frame, framebuffer.RowBytes);
        Marshal.Copy(_bgraPixels, 0, framebuffer.Address, _bgraPixels.Length);
        return bitmap;
    }

    public void Dispose()
    {
        for (int index = 0; index < _bitmaps.Length; index++)
        {
            _bitmaps[index]?.Dispose();
            _bitmaps[index] = null;
        }

        _bgraPixels = [];
    }

    private WriteableBitmap GetNextBitmap(int width, int height)
    {
        int index = _nextBitmapIndex;
        _nextBitmapIndex = (_nextBitmapIndex + 1) % _bitmaps.Length;

        var pixelSize = new PixelSize(width, height);
        WriteableBitmap? bitmap = _bitmaps[index];

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
        _bgraPixels = [];
        return bitmap;
    }

    private void WritePixels(LcdFrame frame, int rowBytes)
    {
        ReadOnlySpan<byte> shades = frame.Pixels.Span;

        for (int y = 0; y < frame.Height; y++)
        {
            int sourceRowOffset = y * frame.Width;
            int targetRowOffset = y * rowBytes;

            for (int x = 0; x < frame.Width; x++)
            {
                int targetOffset = targetRowOffset + (x * BytesPerPixel);
                int colorOffset =
                    (shades[sourceRowOffset + x] > 3 ? 3 : shades[sourceRowOffset + x])
                    * BytesPerPixel;
                DmgLcdPalette
                    .Bgra[colorOffset..(colorOffset + BytesPerPixel)]
                    .CopyTo(_bgraPixels.AsSpan(targetOffset, BytesPerPixel));
            }
        }
    }
}
