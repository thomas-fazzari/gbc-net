using GbcNet.Core.Memory;

namespace GbcNet.Core.Dma.Policies;

/// <summary>
/// CGB OAM DMA source decoding with cartridge, VRAM, and WRAM CPU bus conflict splitting.
/// </summary>
internal sealed class CgbOamDmaTransferPolicy : ITransferPolicy
{
    /// <summary>
    /// Clears address bit 13 for E000-FFFF OAM DMA source pages.
    /// </summary>
    private const ushort HighSourceMirrorMask = 0xDFFF;

    /// <summary>
    /// CGB OAM DMA bus used to compare a CPU address with the OAM DMA source address.
    /// </summary>
    private enum DmaBus
    {
        /// <summary>
        /// Cartridge ROM and external cartridge RAM bus.
        /// </summary>
        Cartridge = 0,

        /// <summary>
        /// VRAM bus used by the 8000-9FFF range.
        /// </summary>
        Video = 1,

        /// <summary>
        /// WRAM bus used by C000-DFFF and its echo.
        /// </summary>
        WorkRam = 2,

        /// <summary>
        /// CPU-internal or unmapped addresses not conflicted by OAM DMA source reads here.
        /// </summary>
        None = 3,
    }

    public bool TryMapSourceAddress(ushort sourceAddress, out ushort mappedAddress)
    {
        mappedAddress = MapHighSourceAddress(sourceAddress);
        return true;
    }

    public bool IsCpuAddressBlocked(ushort address, ushort sourceAddress)
    {
        if (address >= AddressMap.ObjectAttributeMemoryStart)
        {
            return address <= AddressMap.ObjectAttributeMemoryEnd;
        }

        var sourceBus = GetBus(MapHighSourceAddress(sourceAddress));
        return sourceBus is not DmaBus.None && GetBus(address) == sourceBus;
    }

    private static ushort MapHighSourceAddress(ushort sourceAddress) =>
        sourceAddress >= AddressMap.EchoRamStart
            ? (ushort)(sourceAddress & HighSourceMirrorMask)
            : sourceAddress;

    private static DmaBus GetBus(ushort address) =>
        address switch
        {
            <= AddressMap.RomEnd => DmaBus.Cartridge,
            <= AddressMap.VideoRamEnd => DmaBus.Video,
            <= AddressMap.ExternalRamEnd => DmaBus.Cartridge,
            <= AddressMap.EchoRamEnd => DmaBus.WorkRam,
            _ => DmaBus.None,
        };
}
