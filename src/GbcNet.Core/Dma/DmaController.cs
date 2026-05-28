using GbcNet.Core.Memory;

namespace GbcNet.Core.Dma;

/// <summary>
/// Copies bytes from the FF46-selected source page into OAM over machine cycles.
/// </summary>
internal sealed class DmaController
{
    private const int SourceAddressShift = 8;
    private const int TransferLength = 0xA0;

    private byte _sourceHighByte;
    private int _nextOffset;
    private bool _isTransferActive;
    private bool _skipNextTick;

    /// <summary>
    /// Reads FF46 as the last OAM DMA source high byte written by CPU or boot state.
    /// </summary>
    public byte ReadRegister() => _sourceHighByte;

    /// <summary>
    /// Indicates that OAM DMA currently owns the memory bus for CPU-visible memory regions.
    /// </summary>
    public bool IsActive => _isTransferActive;

    /// <summary>
    /// Starts an OAM DMA transfer from sourceHighByte * 0x100.
    /// </summary>
    public void StartOamTransfer(byte sourceHighByte)
    {
        _sourceHighByte = sourceHighByte;
        _nextOffset = 0;
        _isTransferActive = true;
        _skipNextTick = true;
    }

    /// <summary>
    /// Advances the active OAM DMA transfer by one byte per elapsed machine cycle.
    /// </summary>
    public void Tick(
        int machineCycles,
        Func<ushort, byte> readSourceByte,
        Action<ushort, byte> writeOamByte
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(machineCycles);
        ArgumentNullException.ThrowIfNull(readSourceByte);
        ArgumentNullException.ThrowIfNull(writeOamByte);

        if (!_isTransferActive || machineCycles == 0)
        {
            return;
        }

        if (_skipNextTick)
        {
            _skipNextTick = false;
            return;
        }

        CopyBytes(machineCycles, readSourceByte, writeOamByte);
    }

    /// <summary>
    /// Seeds FF46 without starting OAM DMA.
    /// </summary>
    internal void SetRegisterState(byte value)
    {
        _sourceHighByte = value;
        _nextOffset = 0;
        _isTransferActive = false;
        _skipNextTick = false;
    }

    private void CopyBytes(
        int machineCycles,
        Func<ushort, byte> readSourceByte,
        Action<ushort, byte> writeOamByte
    )
    {
        for (int cycle = 0; cycle < machineCycles && _nextOffset < TransferLength; cycle++)
        {
            ushort sourceAddress = (ushort)((_sourceHighByte << SourceAddressShift) + _nextOffset);
            ushort destinationAddress = (ushort)(
                AddressMap.ObjectAttributeMemoryStart + _nextOffset
            );

            writeOamByte(destinationAddress, readSourceByte(sourceAddress));
            _nextOffset++;
        }

        if (_nextOffset == TransferLength)
        {
            _isTransferActive = false;
        }
    }
}
