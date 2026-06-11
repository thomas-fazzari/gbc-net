using GbcNet.Core.Ppu;
using GbcNet.Gui.Rendering;

namespace GbcNet.Tests.Gui.Rendering;

public sealed class LcdFrameBitmapRendererTests
{
    [Fact]
    public void WritePixels_ConvertsShadeIndicesToBgra()
    {
        LcdFrame frame = CreateFrame(width: 2, height: 2, 0, 1, 2, 3);
        byte[] destination = new byte[16];

        LcdFrameBitmapRenderer.WritePixels(frame, destination, rowBytes: 8);

        Assert.Equal(
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
            ],
            destination
        );
    }

    [Fact]
    public void WritePixels_RespectsRowPaddingAndClampsInvalidShadeIndices()
    {
        LcdFrame frame = CreateFrame(width: 1, height: 2, 0, 7);
        byte[] destination = Enumerable.Repeat((byte)0xCC, count: 16).ToArray();

        LcdFrameBitmapRenderer.WritePixels(frame, destination, rowBytes: 8);

        Assert.Equal(
            [
                0xD0,
                0xF8,
                0xE0,
                0xFF,
                0xCC,
                0xCC,
                0xCC,
                0xCC,
                0x18,
                0x18,
                0x08,
                0xFF,
                0xCC,
                0xCC,
                0xCC,
                0xCC,
            ],
            destination
        );
    }

    [Fact]
    public void WritePixels_ConvertsRgb555LittleEndianToBgra()
    {
        LcdFrame frame = new(
            width: 3,
            height: 1,
            LcdPixelFormat.Rgb555Le,
            [0x1F, 0x00, 0xE0, 0x03, 0x00, 0x7C]
        );
        byte[] destination = new byte[12];

        LcdFrameBitmapRenderer.WritePixels(frame, destination, rowBytes: 12);

        Assert.Equal(
            [0x00, 0x00, 0xFF, 0xFF, 0x00, 0xFF, 0x00, 0xFF, 0xFF, 0x00, 0x00, 0xFF],
            destination
        );
    }

    private static LcdFrame CreateFrame(int width, int height, params byte[] pixels) =>
        new(width, height, LcdPixelFormat.DmgShadeIndex8, pixels);
}
