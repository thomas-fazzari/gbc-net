namespace GbcNet.Core.Dma;

/// <summary>
/// Models hardware-specific OAM DMA source decoding and CPU bus conflicts.
/// </summary>
internal interface IDmaTransferPolicy
{
    /// <summary>
    /// Maps an OAM DMA source address to the memory address read by this hardware model.
    /// </summary>
    bool TryMapSourceAddress(ushort sourceAddress, out ushort mappedAddress);

    /// <summary>
    /// Returns whether a CPU access conflicts with the active OAM DMA source bus.
    /// </summary>
    bool IsCpuAddressBlocked(ushort address, ushort sourceAddress);
}
