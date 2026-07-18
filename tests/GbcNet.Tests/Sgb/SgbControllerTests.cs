// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Ppu;
using GbcNet.Core.Snes;

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
        Assert.Equal(160, colorized.Width);
        Assert.Equal(144, colorized.Height);
        Rgb555Assertions.PixelEquals(colorized, GameBoyPixelIndex(x: 0, y: 0), expected: 0x6666);
        Rgb555Assertions.PixelEquals(colorized, GameBoyPixelIndex(x: 0, y: 8), expected: 0x3333);
    }

    [Fact]
    public void ApplyPalettes_UsesDivisionAttributes()
    {
        var sgb = new SgbController(commandsEnabled: true);
        WriteSgbPacket(sgb, command: 0x00, Pal01Payload);
        WriteSgbPacket(sgb, command: 0x06, [0x10, 0x01]);

        var frame = CreateDmgFrame(shade: 3);
        var colorized = sgb.ApplyPalettes(frame);

        Rgb555Assertions.PixelEquals(colorized, GameBoyPixelIndex(x: 0, y: 0), expected: 0x4444);
        Rgb555Assertions.PixelEquals(colorized, GameBoyPixelIndex(x: 8, y: 0), expected: 0x7777);
        Rgb555Assertions.PixelEquals(colorized, GameBoyPixelIndex(x: 16, y: 0), expected: 0x4444);
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

        Rgb555Assertions.PixelEquals(firstFrame, GameBoyPixelIndex(x: 0, y: 0), expected: 0x2222);
        Rgb555Assertions.PixelEquals(frozenFrame, GameBoyPixelIndex(x: 0, y: 0), expected: 0x2222);
        Rgb555Assertions.PixelEquals(currentFrame, GameBoyPixelIndex(x: 0, y: 0), expected: 0x3333);
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

        Rgb555Assertions.PixelEquals(blackFrame, GameBoyPixelIndex(x: 0, y: 0), expected: 0x0000);
        Rgb555Assertions.PixelEquals(
            colorZeroFrame,
            GameBoyPixelIndex(x: 0, y: 0),
            expected: 0x1111
        );
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
        Rgb555Assertions.PixelEquals(colorized, GameBoyPixelIndex(x: 0, y: 0), expected: 0x3333);
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

        Rgb555Assertions.PixelEquals(colorized, GameBoyPixelIndex(x: 0, y: 0), expected: 0x2222);
    }

    [Fact]
    public void Write_DataSndDoesNotRequestVramTransfer()
    {
        var sgb = new SgbController(commandsEnabled: true);

        WriteSgbPacket(sgb, command: 0x0F, [0x00, 0x18, 0x00, 0x01, 0x42]);

        Assert.False(sgb.HasPendingVramTransfer);
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
        Rgb555Assertions.PixelEquals(colorized, GameBoyPixelIndex(x: 0, y: 0), expected: 0x6666);
        Rgb555Assertions.PixelEquals(colorized, GameBoyPixelIndex(x: 8, y: 0), expected: 0x3333);
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

        Rgb555Assertions.PixelEquals(colorized, GameBoyPixelIndex(x: 0, y: 0), expected: 0x7777);
        Rgb555Assertions.PixelEquals(colorized, GameBoyPixelIndex(x: 8, y: 0), expected: 0x3333);
    }

    [Fact]
    public void ApplyPendingVramTransfer_LoadsBorderTilesAndMap()
    {
        var sgb = new SgbController(commandsEnabled: true);
        var tileTransfer = new byte[4096];
        var mapTransfer = new byte[4096];
        WriteBorderTilePixel(tileTransfer, tileIndex: 1, color: 5);
        WriteUInt16(mapTransfer, offset: 0, (4 << 10) | 1);
        WriteUInt16(mapTransfer, offset: 0x800 + (5 * 2), 0x1234);
        WriteUInt16(mapTransfer, offset: (7 + (5 * 32)) * 2, (4 << 10) | 1);

        WriteSgbPacket(sgb, command: 0x13, [0x00]);
        Assert.True(sgb.HasPendingVramTransfer);
        sgb.ApplyPendingVramTransfer(tileTransfer);
        WriteSgbPacket(sgb, command: 0x14, []);
        Assert.True(sgb.HasPendingVramTransfer);
        sgb.ApplyPendingVramTransfer(mapTransfer);

        var colorized = sgb.ApplyPalettes(CreateDmgFrame(shade: 0));

        Assert.False(sgb.HasPendingVramTransfer);
        Rgb555Assertions.PixelEquals(colorized, pixelIndex: 0, expected: 0x1234);
        Rgb555Assertions.PixelEquals(colorized, SgbGameBoyPixelIndex(x: 0, y: 0), expected: 0x7FFF);
        Rgb555Assertions.PixelEquals(colorized, SgbGameBoyPixelIndex(x: 8, y: 0), expected: 0x1234);
    }

    [Fact]
    public void ApplyPalettes_RebuildsCachedBorderAfterTileTransfer()
    {
        var sgb = new SgbController(commandsEnabled: true);
        var firstTiles = new byte[4096];
        var updatedTiles = new byte[4096];
        var map = new byte[4096];
        WriteBorderTilePixel(firstTiles, tileIndex: 1, color: 5);
        WriteBorderTilePixel(updatedTiles, tileIndex: 1, color: 6);
        WriteUInt16(map, offset: 0, (4 << 10) | 1);
        WriteUInt16(map, offset: 0x800 + (5 * 2), 0x1234);
        WriteUInt16(map, offset: 0x800 + (6 * 2), 0x5678);
        ApplyBorderTransfers(sgb, firstTiles, map);
        var firstFrame = sgb.ApplyPalettes(CreateDmgFrame(shade: 0));

        WriteSgbPacket(sgb, command: 0x13, [0x00]);
        sgb.ApplyPendingVramTransfer(updatedTiles);
        var updatedFrame = sgb.ApplyPalettes(CreateDmgFrame(shade: 0));

        Rgb555Assertions.PixelEquals(firstFrame, pixelIndex: 0, expected: 0x1234);
        Rgb555Assertions.PixelEquals(updatedFrame, pixelIndex: 0, expected: 0x5678);
    }

    [Fact]
    public void ApplyPalettes_RebuildsCachedBorderAfterMapTransfer()
    {
        var sgb = new SgbController(commandsEnabled: true);
        var tiles = new byte[4096];
        var firstMap = new byte[4096];
        var updatedMap = new byte[4096];
        WriteBorderTilePixel(tiles, tileIndex: 1, color: 5);
        WriteUInt16(firstMap, offset: 0, (4 << 10) | 1);
        WriteUInt16(firstMap, offset: 0x800 + (5 * 2), 0x1234);
        WriteUInt16(updatedMap, offset: 0, (4 << 10) | 1);
        WriteUInt16(updatedMap, offset: 0x800 + (5 * 2), 0x5678);
        ApplyBorderTransfers(sgb, tiles, firstMap);
        var firstFrame = sgb.ApplyPalettes(CreateDmgFrame(shade: 0));

        WriteSgbPacket(sgb, command: 0x14, []);
        sgb.ApplyPendingVramTransfer(updatedMap);
        var updatedFrame = sgb.ApplyPalettes(CreateDmgFrame(shade: 0));

        Rgb555Assertions.PixelEquals(firstFrame, pixelIndex: 0, expected: 0x1234);
        Rgb555Assertions.PixelEquals(updatedFrame, pixelIndex: 0, expected: 0x5678);
    }

    [Fact]
    public void ApplyPalettes_RebuildsCachedBorderAfterSharedColorZeroChanges()
    {
        var sgb = new SgbController(commandsEnabled: true);
        ApplyBorderTransfers(sgb, new byte[4096], new byte[4096]);
        var firstFrame = sgb.ApplyPalettes(CreateDmgFrame(shade: 1));

        WriteSgbPacket(sgb, command: 0x00, Pal01Payload);
        var updatedFrame = sgb.ApplyPalettes(CreateDmgFrame(shade: 1));

        Rgb555Assertions.PixelEquals(firstFrame, pixelIndex: 0, expected: 0x7FFF);
        Rgb555Assertions.PixelEquals(updatedFrame, pixelIndex: 0, expected: 0x1111);
        Rgb555Assertions.PixelEquals(
            updatedFrame,
            SgbGameBoyPixelIndex(x: 0, y: 0),
            expected: 0x2222
        );
    }

    [Fact]
    public void ApplyPalettes_RebuildsCachedBorderAfterPalSet()
    {
        var sgb = new SgbController(commandsEnabled: true);
        var paletteTransfer = new byte[4096];
        WriteSystemPalette(paletteTransfer, paletteId: 5, 0x1357, 0x2222, 0x3333, 0x4444);
        WriteSgbPacket(sgb, command: 0x0B, []);
        sgb.ApplyPendingVramTransfer(paletteTransfer);
        ApplyBorderTransfers(sgb, new byte[4096], new byte[4096]);
        var firstFrame = sgb.ApplyPalettes(CreateDmgFrame(shade: 1));

        WriteSgbPacket(sgb, command: 0x0A, CreatePalSetPayload(5, 5, 5, 5));
        var updatedFrame = sgb.ApplyPalettes(CreateDmgFrame(shade: 1));

        Rgb555Assertions.PixelEquals(firstFrame, pixelIndex: 0, expected: 0x7FFF);
        Rgb555Assertions.PixelEquals(updatedFrame, pixelIndex: 0, expected: 0x1357);
    }

    private static void ApplyBorderTransfers(
        SgbController sgb,
        byte[] tileTransfer,
        byte[] mapTransfer
    )
    {
        WriteSgbPacket(sgb, command: 0x13, [0x00]);
        sgb.ApplyPendingVramTransfer(tileTransfer);
        WriteSgbPacket(sgb, command: 0x14, []);
        sgb.ApplyPendingVramTransfer(mapTransfer);
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

    private static void WriteBorderTilePixel(byte[] transferData, int tileIndex, byte color)
    {
        var offset = tileIndex * 32;

        if ((color & 0x01) != 0)
        {
            transferData[offset] = 0x80;
        }

        if ((color & 0x02) != 0)
        {
            transferData[offset + 1] = 0x80;
        }

        if ((color & 0x04) != 0)
        {
            transferData[offset + 16] = 0x80;
        }

        if ((color & 0x08) != 0)
        {
            transferData[offset + 17] = 0x80;
        }
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
    }

    private static int GameBoyPixelIndex(int x, int y) => (y * 160) + x;

    private static int SgbGameBoyPixelIndex(int x, int y) => ((40 + y) * 256) + 48 + x;

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
        WriteSgbJoyp(sgb, ref selectedGroups, 0x00);
        WriteSgbJoyp(sgb, ref selectedGroups, 0x30);
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
