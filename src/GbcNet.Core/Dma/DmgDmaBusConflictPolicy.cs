using GbcNet.Core.Memory;

namespace GbcNet.Core.Dma;

/// <summary>
/// DMG OAM DMA conflict policy, where main memory and VRAM behave as separate DMA buses.
/// </summary>
internal sealed class DmgDmaBusConflictPolicy : IDmaBusConflictPolicy
{
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
