// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers.Binary;
using GbcNet.Core.Ppu;
using GbcNet.Core.Snes;

namespace GbcNet.Tests.Unit.Sgb;

/// <summary>
/// Builds SGB command data and sends packets through the JOYP transfer protocol.
/// </summary>
internal static class SgbTestHelpers
{
    /// <summary>
    /// Gets a 15-byte PAL01 payload with distinct little-endian RGB555 colors.
    /// </summary>
    internal static ReadOnlySpan<byte> Pal01Payload =>
        [0x11, 0x11, 0x22, 0x22, 0x33, 0x33, 0x44, 0x44, 0x55, 0x55, 0x66, 0x66, 0x77, 0x77, 0x00];

    /// <summary>
    /// Gets <see cref="Pal01Payload"/> with shared color zero set to white.
    /// </summary>
    internal static ReadOnlySpan<byte> WhiteColorZeroPal01Payload =>
        [0xFF, 0x7F, 0x22, 0x22, 0x33, 0x33, 0x44, 0x44, 0x55, 0x55, 0x66, 0x66, 0x77, 0x77, 0x00];

    /// <summary>
    /// Creates a 160 by 144 DMG frame filled with one shade index.
    /// </summary>
    internal static LcdFrame CreateDmgFrame(byte shade)
    {
        var pixels = new byte[160 * 144];
        Array.Fill(pixels, shade);
        return new LcdFrame(160, 144, LcdPixelFormat.DmgShadeIndex8, pixels);
    }

    /// <summary>
    /// Creates one 16-byte SGB packet with its command header and payload.
    /// </summary>
    /// <param name="command">The five-bit SGB command number.</param>
    /// <param name="payload">Up to 15 payload bytes.</param>
    /// <param name="packetCount">The total packet count encoded in the header.</param>
    internal static byte[] CreatePacket(
        byte command,
        ReadOnlySpan<byte> payload,
        byte packetCount = 1
    )
    {
        var packet = new byte[16];
        packet[0] = (byte)((command << 3) | packetCount);
        payload.CopyTo(packet.AsSpan(1));
        return packet;
    }

    /// <summary>
    /// Creates a PAL_SET payload containing four system palette IDs and its flags.
    /// </summary>
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

    /// <summary>
    /// Writes four little-endian RGB555 colors into one eight-byte system palette slot.
    /// </summary>
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

    /// <summary>
    /// Writes the packed palette data for the first four tiles of an attribute file.
    /// </summary>
    internal static void WriteAttributeFile(
        byte[] transferData,
        int fileIndex,
        byte packedFirstFourTiles
    )
    {
        transferData[fileIndex * 90] = packedFirstFourTiles;
    }

    /// <summary>
    /// Writes the top-left four-bit color index of one SNES border tile.
    /// </summary>
    internal static void WriteBorderTilePixel(byte[] transferData, int tileIndex, byte color)
    {
        var offset = tileIndex * 32;
        transferData[offset] = (byte)((color & 0x01) != 0 ? 0x80 : 0);
        transferData[offset + 1] = (byte)((color & 0x02) != 0 ? 0x80 : 0);
        transferData[offset + 16] = (byte)((color & 0x04) != 0 ? 0x80 : 0);
        transferData[offset + 17] = (byte)((color & 0x08) != 0 ? 0x80 : 0);
    }

    /// <summary>
    /// Writes one little-endian SNES border map entry in the 32-tile-wide map.
    /// </summary>
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

    /// <summary>
    /// Writes one flat RGB555 color slot in the border palette area at offset 0x800.
    /// </summary>
    internal static void WriteBorderPaletteColor(
        byte[] transferData,
        int paletteColor,
        ushort color
    ) => WriteUInt16(transferData, 0x800 + (paletteColor * 2), color);

    /// <summary>
    /// Applies CHR_TRN tile data followed by PCT_TRN map and palette data.
    /// </summary>
    internal static void ApplyBorderTransfers(SgbController sgb, byte[] tiles, byte[] map)
    {
        WriteSgbPacket(sgb, command: 0x13, [0x00]);
        sgb.ApplyPendingVramTransfer(tiles);
        WriteSgbPacket(sgb, command: 0x14, []);
        sgb.ApplyPendingVramTransfer(map);
    }

    /// <summary>
    /// Builds and sends one SGB command packet through JOYP writes.
    /// </summary>
    internal static void WriteSgbPacket(SgbController sgb, byte command, ReadOnlySpan<byte> payload)
    {
        WriteSgbPacket(sgb, CreatePacket(command, payload));
    }

    /// <summary>
    /// Sends an encoded SGB packet through JOYP pulses without timing delays.
    /// </summary>
    internal static void WriteSgbPacket(SgbController sgb, ReadOnlySpan<byte> packet)
    {
        var selectedGroups = (byte)0x30;
        WriteSgbStartPulse(sgb, ref selectedGroups);
        WriteBits(sgb, ref selectedGroups, packet);
        WriteSgbBit(sgb, ref selectedGroups, value: false);
    }

    /// <summary>
    /// Sends a range of packet bits least-significant bit first through JOYP writes.
    /// </summary>
    /// <param name="selectedGroups">The current JOYP selection-line state, updated after each write.</param>
    /// <param name="bytes">The packet bytes whose bits are sent.</param>
    /// <param name="start">The zero-based bit offset.</param>
    /// <param name="count">The maximum number of bits to send.</param>
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

    /// <summary>
    /// Sends the JOYP reset pulse that starts an SGB packet.
    /// </summary>
    internal static void WriteSgbStartPulse(SgbController sgb, ref byte selectedGroups)
    {
        WriteSgbJoyp(sgb, ref selectedGroups, 0x00);
        WriteSgbJoyp(sgb, ref selectedGroups, 0x30);
    }

    /// <summary>
    /// Sends one SGB data bit through the JOYP selection lines.
    /// </summary>
    internal static void WriteSgbBit(SgbController sgb, ref byte selectedGroups, bool value)
    {
        WriteSgbJoyp(sgb, ref selectedGroups, 0x30);
        WriteSgbJoyp(sgb, ref selectedGroups, value ? (byte)0x10 : (byte)0x20);
    }

    /// <summary>
    /// Converts Game Boy screen coordinates to a row-major 160-pixel frame index.
    /// </summary>
    internal static int GameBoyPixelIndex(int x, int y) => (y * 160) + x;

    /// <summary>
    /// Converts Game Boy screen coordinates to their centered index in a 256 by 224 SGB frame.
    /// </summary>
    internal static int SgbGameBoyPixelIndex(int x, int y) => ((40 + y) * 256) + 48 + x;

    private static void WriteSgbJoyp(SgbController sgb, ref byte selectedGroups, byte value)
    {
        sgb.Write(value, selectedGroups);
        selectedGroups = (byte)(value & 0x30);
    }

    /// <summary>
    /// Writes a 16-bit value in the little-endian format used by SGB payloads.
    /// </summary>
    internal static void WriteUInt16(Span<byte> bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.Slice(offset), value);
}
