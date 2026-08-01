// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers.Binary;
using GbcNet.Core.Ppu;
using GbcNet.Core.Snes;

namespace GbcNet.Tests.Sgb;

internal static class SgbTestHelpers
{
    internal static ReadOnlySpan<byte> Pal01Payload =>
        [0x11, 0x11, 0x22, 0x22, 0x33, 0x33, 0x44, 0x44, 0x55, 0x55, 0x66, 0x66, 0x77, 0x77, 0x00];

    internal static ReadOnlySpan<byte> WhiteColorZeroPal01Payload =>
        [0xFF, 0x7F, 0x22, 0x22, 0x33, 0x33, 0x44, 0x44, 0x55, 0x55, 0x66, 0x66, 0x77, 0x77, 0x00];

    internal static LcdFrame CreateDmgFrame(byte shade)
    {
        var pixels = new byte[160 * 144];
        Array.Fill(pixels, shade);
        return new LcdFrame(160, 144, LcdPixelFormat.DmgShadeIndex8, pixels);
    }

    internal static byte[] CreatePacket(byte command, ReadOnlySpan<byte> payload)
    {
        var packet = new byte[16];
        packet[0] = (byte)((command << 3) | 0x01);
        payload.CopyTo(packet.AsSpan(1));
        return packet;
    }

    internal static byte[] CreatePalSetPayload(
        ushort palette0,
        ushort palette1,
        ushort palette2,
        ushort palette3,
        byte flags = 0
    )
    {
        var payload = new byte[15];
        WriteUInt16(payload, 0, palette0);
        WriteUInt16(payload, 2, palette1);
        WriteUInt16(payload, 4, palette2);
        WriteUInt16(payload, 6, palette3);
        payload[8] = flags;
        return payload;
    }

    internal static void WriteSystemPalette(
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

    internal static void WriteAttributeFile(
        byte[] transferData,
        int fileIndex,
        byte packedFirstFourTiles
    )
    {
        transferData[fileIndex * 90] = packedFirstFourTiles;
    }

    internal static void WriteBorderTilePixel(byte[] transferData, int tileIndex, byte color)
    {
        var offset = tileIndex * 32;
        transferData[offset] = (byte)((color & 0x01) != 0 ? 0x80 : 0);
        transferData[offset + 1] = (byte)((color & 0x02) != 0 ? 0x80 : 0);
        transferData[offset + 16] = (byte)((color & 0x04) != 0 ? 0x80 : 0);
        transferData[offset + 17] = (byte)((color & 0x08) != 0 ? 0x80 : 0);
    }

    internal static void WriteBorderMapEntry(
        byte[] transferData,
        int tileX,
        int tileY,
        int tileIndex,
        int palette
    ) =>
        WriteUInt16(
            transferData,
            ((tileY * 32) + tileX) * 2,
            (ushort)((palette << 10) | tileIndex)
        );

    internal static void WriteBorderPaletteColor(
        byte[] transferData,
        int paletteColor,
        ushort color
    ) => WriteUInt16(transferData, 0x800 + (paletteColor * 2), color);

    internal static void ApplyBorderTransfers(SgbController sgb, byte[] tiles, byte[] map)
    {
        WriteSgbPacket(sgb, command: 0x13, [0x00]);
        sgb.ApplyPendingVramTransfer(tiles);
        WriteSgbPacket(sgb, command: 0x14, []);
        sgb.ApplyPendingVramTransfer(map);
    }

    internal static void WriteSgbPacket(SgbController sgb, byte command, ReadOnlySpan<byte> payload)
    {
        var selectedGroups = (byte)0x30;
        WriteSgbStartPulse(sgb, ref selectedGroups);
        WriteBits(sgb, ref selectedGroups, CreatePacket(command, payload));
        WriteSgbBit(sgb, ref selectedGroups, value: false);
    }

    internal static void WriteBits(
        SgbController sgb,
        ref byte selectedGroups,
        ReadOnlySpan<byte> bytes,
        int start = 0,
        int count = int.MaxValue
    )
    {
        var end = (int)Math.Min(bytes.Length * 8L, (long)start + count);
        for (var bit = start; bit < end; bit++)
        {
            WriteSgbBit(sgb, ref selectedGroups, (bytes[bit / 8] & (1 << (bit & 7))) != 0);
        }
    }

    internal static void WriteSgbStartPulse(SgbController sgb, ref byte selectedGroups)
    {
        WriteSgbJoyp(sgb, ref selectedGroups, 0x00);
        WriteSgbJoyp(sgb, ref selectedGroups, 0x30);
    }

    internal static void WriteSgbBit(SgbController sgb, ref byte selectedGroups, bool value)
    {
        WriteSgbJoyp(sgb, ref selectedGroups, 0x30);
        WriteSgbJoyp(sgb, ref selectedGroups, value ? (byte)0x10 : (byte)0x20);
    }

    internal static int GameBoyPixelIndex(int x, int y) => (y * 160) + x;

    internal static int SgbGameBoyPixelIndex(int x, int y) => ((40 + y) * 256) + 48 + x;

    private static void WriteSgbJoyp(SgbController sgb, ref byte selectedGroups, byte value)
    {
        sgb.Write(value, selectedGroups);
        selectedGroups = (byte)(value & 0x30);
    }

    internal static void WriteUInt16(Span<byte> bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.Slice(offset), value);
}
