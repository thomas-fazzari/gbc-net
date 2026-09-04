// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Memory;
using GbcNet.Core.Ppu;

namespace GbcNet.Tests.Unit.Ppu;

/// <summary>
/// Writes compact tile and palette fixtures through the PPU test seam.
/// </summary>
internal static class PpuTestHelpers
{
    /// <summary>
    /// Writes one two-bit tile row as its low and high bitplane bytes.
    /// </summary>
    /// <param name="ppu">The PPU whose video RAM is changed.</param>
    /// <param name="tileAddress">The first video RAM address of the tile.</param>
    /// <param name="row">The zero-based row within the eight-row tile.</param>
    /// <param name="lowByte">The low color bits, with bit 7 holding the left pixel.</param>
    /// <param name="highByte">The high color bits, with bit 7 holding the left pixel.</param>
    internal static void WriteTileRow(
        PpuController ppu,
        ushort tileAddress,
        int row,
        byte lowByte,
        byte highByte
    )
    {
        var rowAddress = (ushort)(tileAddress + (row * 2));
        ppu.VideoRam.Write(rowAddress, lowByte);
        ppu.VideoRam.Write((ushort)(rowAddress + 1), highByte);
    }

    /// <summary>
    /// Writes one little-endian RGB555 color through the CGB background palette registers.
    /// </summary>
    /// <param name="paletteIndex">The background palette index. Only its low three bits are used.</param>
    /// <param name="colorId">The color index. Only its low two bits are used.</param>
    /// <param name="rgb555">The packed 15-bit RGB color.</param>
    internal static void WriteBackgroundColor(
        PpuController ppu,
        int paletteIndex,
        byte colorId,
        ushort rgb555
    )
    {
        var offset = (byte)((((paletteIndex & 0x07) * 4) + (colorId & 0x03)) * 2);
        ppu.WriteRegister(AddressMap.BackgroundPaletteIndexRegister, offset);
        ppu.WriteRegister(AddressMap.BackgroundPaletteDataRegister, (byte)rgb555);
        ppu.WriteRegister(AddressMap.BackgroundPaletteIndexRegister, (byte)(offset + 1));
        ppu.WriteRegister(AddressMap.BackgroundPaletteDataRegister, (byte)(rgb555 >> 8));
    }
}
