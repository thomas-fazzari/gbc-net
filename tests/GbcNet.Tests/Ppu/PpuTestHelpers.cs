// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Memory;
using GbcNet.Core.Ppu;

namespace GbcNet.Tests.Ppu;

internal static class PpuTestHelpers
{
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
