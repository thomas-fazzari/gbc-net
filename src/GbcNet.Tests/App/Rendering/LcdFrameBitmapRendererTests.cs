using GbcNet.App.Rendering;
using GbcNet.Core.Ppu;

namespace GbcNet.Tests.App.Rendering;

public sealed class LcdFrameBitmapRendererTests
{
    [Fact]
    public void WritePixels_ConvertsShadeIndicesToBgra()
    {
        var frame = CreateFrame(width: 2, height: 2, 0, 1, 2, 3);
        var destination = new byte[16];

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
        var frame = CreateFrame(width: 1, height: 2, 0, 7);
        var destination = Enumerable.Repeat((byte)0xCC, count: 16).ToArray();

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
        var destination = new byte[12];

        LcdFrameBitmapRenderer.WritePixels(frame, destination, rowBytes: 12);

        Assert.Equal(
            [0x00, 0x00, 0xFF, 0xFF, 0x00, 0xFF, 0x00, 0xFF, 0xFF, 0x00, 0x00, 0xFF],
            destination
        );
    }

    private static LcdFrame CreateFrame(int width, int height, params byte[] pixels) =>
        new(width, height, LcdPixelFormat.DmgShadeIndex8, pixels);
}
