namespace GbcNet.Core.Dma;

/// <summary>
/// Determines which CPU address ranges are blocked by an active OAM DMA source.
/// </summary>
internal interface IDmaBusConflictPolicy
{
    /// <summary>
    /// Returns whether a CPU access conflicts with the DMA source bus.
    /// </summary>
    bool IsCpuAddressBlocked(ushort address, ushort sourceAddress);
}
