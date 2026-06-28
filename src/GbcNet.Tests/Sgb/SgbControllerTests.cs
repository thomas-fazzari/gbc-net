using GbcNet.Core.Ppu;
using GbcNet.Core.Sgb;

namespace GbcNet.Tests.Sgb;

public sealed class SgbControllerTests
{
    private static ReadOnlySpan<byte> Pal01Payload =>
        [0x11, 0x11, 0x22, 0x22, 0x33, 0x33, 0x44, 0x44, 0x55, 0x55, 0x66, 0x66, 0x77, 0x77, 0x00];

    [Fact]
    public void ApplyPalettes_UsesPal01ColorsAndLineAttributes()
    {
        var sgb = new SgbController(commandsEnabled: true);
        WriteSgbPacket(sgb, command: 0x00, Pal01Payload);
        WriteSgbPacket(sgb, command: 0x05, [0x01, 0xA0]);

        var frame = CreateDmgFrame(shade: 2);
        var colorized = sgb.ApplyPalettes(frame);

        Assert.Equal(LcdPixelFormat.Rgb555Le, colorized.PixelFormat);
        AssertRgb555(colorized, pixelIndex: 0, expected: 0x6666);
        AssertRgb555(colorized, pixelIndex: 160 * 8, expected: 0x3333);
    }

    [Fact]
    public void ApplyPalettes_UsesDivisionAttributes()
    {
        var sgb = new SgbController(commandsEnabled: true);
        WriteSgbPacket(sgb, command: 0x00, Pal01Payload);
        WriteSgbPacket(sgb, command: 0x06, [0x10, 0x01]);

        var frame = CreateDmgFrame(shade: 3);
        var colorized = sgb.ApplyPalettes(frame);

        AssertRgb555(colorized, pixelIndex: 0, expected: 0x4444);
        AssertRgb555(colorized, pixelIndex: 8, expected: 0x7777);
        AssertRgb555(colorized, pixelIndex: 16, expected: 0x4444);
    }

    [Fact]
    public void ApplyPalettes_MaskFreezeKeepsPreviousVisibleFrame()
    {
        var sgb = new SgbController(commandsEnabled: true);
        WriteSgbPacket(sgb, command: 0x00, Pal01Payload);

        var firstFrame = sgb.ApplyPalettes(CreateDmgFrame(shade: 1));
        WriteSgbPacket(sgb, command: 0x17, [0x01]);
        var frozenFrame = sgb.ApplyPalettes(CreateDmgFrame(shade: 2));
        WriteSgbPacket(sgb, command: 0x17, [0x00]);
        var currentFrame = sgb.ApplyPalettes(CreateDmgFrame(shade: 2));

        AssertRgb555(firstFrame, pixelIndex: 0, expected: 0x2222);
        AssertRgb555(frozenFrame, pixelIndex: 0, expected: 0x2222);
        AssertRgb555(currentFrame, pixelIndex: 0, expected: 0x3333);
    }

    [Fact]
    public void ApplyPalettes_MaskBlankOutputsBlackOrColorZero()
    {
        var sgb = new SgbController(commandsEnabled: true);
        WriteSgbPacket(sgb, command: 0x00, Pal01Payload);

        WriteSgbPacket(sgb, command: 0x17, [0x02]);
        var blackFrame = sgb.ApplyPalettes(CreateDmgFrame(shade: 1));
        WriteSgbPacket(sgb, command: 0x17, [0x03]);
        var colorZeroFrame = sgb.ApplyPalettes(CreateDmgFrame(shade: 1));

        AssertRgb555(blackFrame, pixelIndex: 0, expected: 0x0000);
        AssertRgb555(colorZeroFrame, pixelIndex: 0, expected: 0x1111);
    }

    private static LcdFrame CreateDmgFrame(byte shade)
    {
        var pixels = new byte[160 * 144];
        Array.Fill(pixels, shade);
        return new LcdFrame(160, 144, LcdPixelFormat.DmgShadeIndex8, pixels);
    }

    private static void AssertRgb555(LcdFrame frame, int pixelIndex, ushort expected)
    {
        var pixels = frame.Pixels.Span;
        var offset = pixelIndex * 2;
        var actual = (ushort)(pixels[offset] | (pixels[offset + 1] << 8));
        Assert.Equal(expected, actual);
    }

    private static void WriteSgbPacket(SgbController sgb, byte command, ReadOnlySpan<byte> payload)
    {
        Span<byte> packet = stackalloc byte[16];
        packet[0] = (byte)((command << 3) | 0x01);
        payload.CopyTo(packet[1..]);

        var selectedGroups = (byte)0x30;
        WriteSgbStartPulse(sgb, ref selectedGroups);
        foreach (var value in packet)
        {
            for (var bit = 0; bit < 8; bit++)
            {
                WriteSgbBit(sgb, ref selectedGroups, (value & (1 << bit)) != 0);
            }
        }

        WriteSgbBit(sgb, ref selectedGroups, value: false);
    }

    private static void WriteSgbStartPulse(SgbController sgb, ref byte selectedGroups)
    {
        WriteSgbJoyp(sgb, ref selectedGroups, 0x30);
        WriteSgbJoyp(sgb, ref selectedGroups, 0x00);
    }

    private static void WriteSgbBit(SgbController sgb, ref byte selectedGroups, bool value)
    {
        WriteSgbJoyp(sgb, ref selectedGroups, 0x30);
        WriteSgbJoyp(sgb, ref selectedGroups, value ? (byte)0x10 : (byte)0x20);
    }

    private static void WriteSgbJoyp(SgbController sgb, ref byte selectedGroups, byte value)
    {
        sgb.Write(value, selectedGroups);
        selectedGroups = (byte)(value & 0x30);
    }
}
