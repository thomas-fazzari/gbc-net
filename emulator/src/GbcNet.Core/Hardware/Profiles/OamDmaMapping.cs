// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Memory;

namespace GbcNet.Core.Hardware.Profiles;

/// <summary>
/// Shared OAM-DMA source address mirroring and CPU bus-blocking logic for DMG, CGB, and SGB.
/// </summary>
internal static class OamDmaMapping
{
    private const ushort HighSourceMirrorMask = 0xDFFF;

    /// <summary>
    /// Mirrors echo-RAM and high source addresses into their canonical WRAM range so the DMA
    /// source resolves to the same physical bytes the CPU bus exposes.
    /// </summary>
    public static ushort MapSourceAddress(ushort sourceAddress) =>
        sourceAddress >= AddressMap.EchoRamStart
            ? (ushort)(sourceAddress & HighSourceMirrorMask)
            : sourceAddress;

    /// <summary>
    /// Determines whether a CPU address is blocked by an in-progress OAM DMA transfer, using the
    /// DMG/SGB two-bus model (video RAM vs everything else). OAM itself is always blocked.
    /// </summary>
    public static bool IsCpuAddressBlockedDmg(ushort address, ushort sourceAddress)
    {
        if (address >= AddressMap.ObjectAttributeMemoryStart)
        {
            return address <= AddressMap.ObjectAttributeMemoryEnd;
        }

        return GetDmgOamDmaBus(address) == GetDmgOamDmaBus(sourceAddress);
    }

    private enum DmgOamDmaBus
    {
        Main = 0,
        Video = 1,
    }

    private static DmgOamDmaBus GetDmgOamDmaBus(ushort address) =>
        address is >= AddressMap.VideoRamStart and <= AddressMap.VideoRamEnd
            ? DmgOamDmaBus.Video
            : DmgOamDmaBus.Main;
}
