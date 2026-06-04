using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using GbcNet.Core.Ppu;
using GbcNet.Gui.Rendering.Palettes;

namespace GbcNet.Gui.Rendering;

/// <summary>
/// Converts immutable core LCD frames into Avalonia bitmaps.
/// </summary>
internal static class LcdFrameBitmapConverter
{
    private const int BytesPerPixel = 4;
    private const double Dpi = 96;

    public static WriteableBitmap ToBitmap(LcdFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (frame.PixelFormat is not LcdPixelFormat.DmgShadeIndex8)
        {
            throw new NotSupportedException("Only DMG shade index frames are supported.");
        }

        var bitmap = new WriteableBitmap(
            new PixelSize(frame.Width, frame.Height),
            new Vector(Dpi, Dpi),
            PixelFormats.Bgra8888,
            AlphaFormat.Opaque
        );

        using ILockedFramebuffer framebuffer = bitmap.Lock();
        byte[] bgraPixels = new byte[framebuffer.RowBytes * frame.Height];
        ReadOnlySpan<byte> shades = frame.Pixels.Span;

        for (int y = 0; y < frame.Height; y++)
        {
            int sourceRowOffset = y * frame.Width;
            int targetRowOffset = y * framebuffer.RowBytes;

            for (int x = 0; x < frame.Width; x++)
            {
                int targetOffset = targetRowOffset + (x * BytesPerPixel);
                WriteBgraPixel(bgraPixels, targetOffset, shades[sourceRowOffset + x]);
            }
        }

        Marshal.Copy(bgraPixels, 0, framebuffer.Address, bgraPixels.Length);
        return bitmap;
    }

    private static void WriteBgraPixel(byte[] pixels, int offset, byte shade)
    {
        int colorOffset = (shade > 3 ? 3 : shade) * BytesPerPixel;
        DmgLcdPalette
            .Bgra[colorOffset..(colorOffset + BytesPerPixel)]
            .CopyTo(pixels.AsSpan(offset, BytesPerPixel));
    }
}
