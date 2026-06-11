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

        if (frame.PixelFormat is not LcdPixelFormat.DmgShadeIndex8)
        {
            throw new NotSupportedException("Only DMG shade index frames are supported.");
        }

        WriteableBitmap bitmap = GetNextBitmap(frame.Width, frame.Height);

        using ILockedFramebuffer framebuffer = bitmap.Lock();
        int bgraLength = checked(framebuffer.RowBytes * frame.Height);
        WritePixels(
            frame,
            destination: new Span<byte>(framebuffer.Address.ToPointer(), bgraLength),
            rowBytes: framebuffer.RowBytes
        );
        return bitmap;
    }

    public void Dispose()
    {
        for (int index = 0; index < _bitmaps.Length; index++)
        {
            _bitmaps[index]?.Dispose();
            _bitmaps[index] = null;
        }
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
        return bitmap;
    }

    internal static void WritePixels(LcdFrame frame, Span<byte> destination, int rowBytes)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentOutOfRangeException.ThrowIfLessThan(rowBytes, frame.Width * BytesPerPixel);

        int requiredLength = checked(rowBytes * frame.Height);

        if (destination.Length < requiredLength)
        {
            throw new ArgumentException("Destination buffer is too small.", nameof(destination));
        }

        ReadOnlySpan<byte> shades = frame.Pixels.Span;
        ReadOnlySpan<byte> colors = DmgLcdPalette.Bgra;

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
                destination[targetOffset] = colors[colorOffset];
                destination[targetOffset + 1] = colors[colorOffset + 1];
                destination[targetOffset + 2] = colors[colorOffset + 2];
                destination[targetOffset + 3] = colors[colorOffset + 3];
            }
        }
    }
}
