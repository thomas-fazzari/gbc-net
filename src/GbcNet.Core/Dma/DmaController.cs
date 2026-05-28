namespace GbcNet.Core.Dma;

/// <summary>
/// Stores OAM DMA register state.
/// </summary>
internal sealed class DmaController
{
    private byte _sourceHighByte;

    /// <summary>
    /// Reads FF46 as the last OAM DMA source high byte written by CPU or boot state.
    /// </summary>
    public byte ReadRegister() => _sourceHighByte;

    /// <summary>
    /// Starts an OAM DMA transfer from sourceHighByte * 0x100.
    /// </summary>
    public void StartOamTransfer(byte sourceHighByte)
    {
        _sourceHighByte = sourceHighByte;
    }

    /// <summary>
    /// Seeds FF46 without starting OAM DMA.
    /// </summary>
    internal void SetRegisterState(byte value)
    {
        _sourceHighByte = value;
    }
}
