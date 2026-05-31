using GbcNet.Core.Memory;

namespace GbcNet.Core.Dma;

/// <summary>
/// Copies bytes from the FF46-selected source page into OAM over machine cycles.
/// </summary>
internal sealed class DmaController
{
    private const int SourceAddressShift = 8;
    private const int TransferLength = 0xA0;
    private const int StartupDelayMachineCycles = 2;

    private byte _sourceHighByte;
    private int _nextOffset;
    private int _startupDelayMachineCycles;

    /// <summary>
    /// Reads FF46 as the last OAM DMA source high byte written by CPU or boot state.
    /// </summary>
    public byte ReadRegister() => _sourceHighByte;

    /// <summary>
    /// Indicates that OAM DMA has been requested and has not completed yet.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Gets the most recently copied source address when OAM DMA can conflict with CPU bus access.
    /// </summary>
    public bool TryGetCpuConflictSourceAddress(out ushort sourceAddress)
    {
        if (!IsActive || _startupDelayMachineCycles != 0 || _nextOffset == 0)
        {
            sourceAddress = 0;
            return false;
        }

        sourceAddress = (ushort)((_sourceHighByte << SourceAddressShift) + _nextOffset - 1);
        return true;
    }

    /// <summary>
    /// Starts an OAM DMA transfer from sourceHighByte * 0x100.
    /// </summary>
    public void StartOamTransfer(byte sourceHighByte)
    {
        _sourceHighByte = sourceHighByte;
        _nextOffset = 0;
        IsActive = true;
        _startupDelayMachineCycles = StartupDelayMachineCycles;
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

        if (!IsActive || machineCycles == 0)
        {
            return;
        }

        int copyCycles = ConsumeStartupDelay(machineCycles);
        CopyBytes(copyCycles, readSourceByte, writeOamByte);
    }

    /// <summary>
    /// Seeds FF46 without starting OAM DMA.
    /// </summary>
    internal void SetRegisterState(byte value)
    {
        _sourceHighByte = value;
        _nextOffset = 0;
        IsActive = false;
        _startupDelayMachineCycles = 0;
    }

    private int ConsumeStartupDelay(int machineCycles)
    {
        int elapsedDelayCycles = Math.Min(_startupDelayMachineCycles, machineCycles);

        _startupDelayMachineCycles -= elapsedDelayCycles;

        return machineCycles - elapsedDelayCycles;
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
            IsActive = false;
        }
    }
}
