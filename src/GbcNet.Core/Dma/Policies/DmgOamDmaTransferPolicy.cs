// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Memory;

namespace GbcNet.Core.Dma.Policies;

/// <summary>
/// DMG OAM DMA behavior, where E000-FFFF source pages mirror C000-DFFF.
/// </summary>
internal sealed class DmgOamDmaTransferPolicy : ITransferPolicy
{
    /// <summary>
    /// Clears address bit 13 for E000-FFFF OAM DMA source pages.
    /// </summary>
    private const ushort HighSourceMirrorMask = 0xDFFF;

    /// <summary>
    /// DMG bus group used to compare a CPU address with the OAM DMA source address.
    /// </summary>
    private enum DmaBus
    {
        /// <summary>
        /// Non-VRAM DMA bus used by cartridge, external RAM, WRAM, and echo RAM ranges.
        /// </summary>
        Main = 0,

        /// <summary>
        /// VRAM DMA bus used by the 8000-9FFF range.
        /// </summary>
        Video = 1,
    }

    public ushort MapSourceAddress(ushort sourceAddress) =>
        sourceAddress >= AddressMap.EchoRamStart
            ? (ushort)(sourceAddress & HighSourceMirrorMask)
            : sourceAddress;

    public bool IsCpuAddressBlocked(ushort address, ushort sourceAddress)
    {
        if (address >= AddressMap.ObjectAttributeMemoryStart)
        {
            return address <= AddressMap.ObjectAttributeMemoryEnd;
        }

        return GetBus(address) == GetBus(sourceAddress);
    }

    private static DmaBus GetBus(ushort address) =>
        address is >= AddressMap.VideoRamStart and <= AddressMap.VideoRamEnd
            ? DmaBus.Video
            : DmaBus.Main;
}
