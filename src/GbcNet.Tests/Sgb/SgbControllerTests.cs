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

    [Fact]
    public void ApplyPendingVramTransfer_LoadsSystemPalettesUsedByPalSet()
    {
        var sgb = new SgbController(commandsEnabled: true);
        var transferData = new byte[4096];
        WriteSystemPalette(transferData, paletteId: 5, 0x1111, 0x2222, 0x3333, 0x4444);

        WriteSgbPacket(sgb, command: 0x0B, []);
        Assert.True(sgb.HasPendingVramTransfer);
        sgb.ApplyPendingVramTransfer(transferData);
        WriteSgbPacket(sgb, command: 0x0A, CreatePalSetPayload(5, 5, 5, 5));

        var colorized = sgb.ApplyPalettes(CreateDmgFrame(shade: 2));

        Assert.False(sgb.HasPendingVramTransfer);
        AssertRgb555(colorized, pixelIndex: 0, expected: 0x3333);
    }

    [Fact]
    public void ApplyPalettes_PalSetCanCancelMask()
    {
        var sgb = new SgbController(commandsEnabled: true);
        var transferData = new byte[4096];
        WriteSystemPalette(transferData, paletteId: 7, 0x1111, 0x2222, 0x3333, 0x4444);

        WriteSgbPacket(sgb, command: 0x0B, []);
        sgb.ApplyPendingVramTransfer(transferData);
        WriteSgbPacket(sgb, command: 0x17, [0x02]);
        WriteSgbPacket(sgb, command: 0x0A, CreatePalSetPayload(7, 7, 7, 7, flags: 0x40));

        var colorized = sgb.ApplyPalettes(CreateDmgFrame(shade: 1));

        AssertRgb555(colorized, pixelIndex: 0, expected: 0x2222);
    }

    [Fact]
    public void ApplyPendingVramTransfer_LoadsAttributeFilesUsedByAttrSet()
    {
        var sgb = new SgbController(commandsEnabled: true);
        var transferData = new byte[4096];
        WriteAttributeFile(transferData, fileIndex: 3, packedFirstFourTiles: 0x40);

        WriteSgbPacket(sgb, command: 0x00, Pal01Payload);
        WriteSgbPacket(sgb, command: 0x15, []);
        Assert.True(sgb.HasPendingVramTransfer);
        sgb.ApplyPendingVramTransfer(transferData);
        WriteSgbPacket(sgb, command: 0x16, [0x03]);

        var colorized = sgb.ApplyPalettes(CreateDmgFrame(shade: 2));

        Assert.False(sgb.HasPendingVramTransfer);
        AssertRgb555(colorized, pixelIndex: 0, expected: 0x6666);
        AssertRgb555(colorized, pixelIndex: 8, expected: 0x3333);
    }

    [Fact]
    public void ApplyPalettes_PalSetCanApplyAttributeFile()
    {
        var sgb = new SgbController(commandsEnabled: true);
        var paletteTransfer = new byte[4096];
        var attributeTransfer = new byte[4096];
        WriteSystemPalette(paletteTransfer, paletteId: 5, 0x1111, 0x2222, 0x3333, 0x4444);
        WriteSystemPalette(paletteTransfer, paletteId: 6, 0x5555, 0x6666, 0x7777, 0x7FFF);
        WriteAttributeFile(attributeTransfer, fileIndex: 4, packedFirstFourTiles: 0x40);

        WriteSgbPacket(sgb, command: 0x0B, []);
        sgb.ApplyPendingVramTransfer(paletteTransfer);
        WriteSgbPacket(sgb, command: 0x15, []);
        sgb.ApplyPendingVramTransfer(attributeTransfer);
        WriteSgbPacket(sgb, command: 0x0A, CreatePalSetPayload(5, 6, 5, 5, flags: 0x84));

        var colorized = sgb.ApplyPalettes(CreateDmgFrame(shade: 2));

        AssertRgb555(colorized, pixelIndex: 0, expected: 0x7777);
        AssertRgb555(colorized, pixelIndex: 8, expected: 0x3333);
    }

    private static LcdFrame CreateDmgFrame(byte shade)
    {
        var pixels = new byte[160 * 144];
        Array.Fill(pixels, shade);
        return new LcdFrame(160, 144, LcdPixelFormat.DmgShadeIndex8, pixels);
    }

    private static byte[] CreatePalSetPayload(
        ushort palette0,
        ushort palette1,
        ushort palette2,
        ushort palette3,
        byte flags = 0
    )
    {
        var payload = new byte[15];
        WriteUInt16(payload, offset: 0, palette0);
        WriteUInt16(payload, offset: 2, palette1);
        WriteUInt16(payload, offset: 4, palette2);
        WriteUInt16(payload, offset: 6, palette3);
        payload[8] = flags;
        return payload;
    }

    private static void WriteSystemPalette(
        byte[] transferData,
        int paletteId,
        ushort color0,
        ushort color1,
        ushort color2,
        ushort color3
    )
    {
        var offset = paletteId * 8;
        WriteUInt16(transferData, offset, color0);
        WriteUInt16(transferData, offset + 2, color1);
        WriteUInt16(transferData, offset + 4, color2);
        WriteUInt16(transferData, offset + 6, color3);
    }

    private static void WriteAttributeFile(
        byte[] transferData,
        int fileIndex,
        byte packedFirstFourTiles
    )
    {
        transferData[fileIndex * 90] = packedFirstFourTiles;
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
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
