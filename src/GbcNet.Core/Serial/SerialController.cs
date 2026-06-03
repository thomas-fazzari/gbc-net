using GbcNet.Core.Interrupts;

namespace GbcNet.Core.Serial;

/// <summary>
/// Emulates the DMG serial transfer data and control registers.
/// </summary>
internal sealed class SerialController(InterruptController interrupts)
{
    /// <summary>
    /// DMG serial master clock toggles on falling edges of divider bit 7.
    /// </summary>
    private const ushort MasterClockFallingEdgeMask = 1 << 7;

    /// <summary>
    /// Serial transfers complete after shifting eight bits.
    /// </summary>
    private const int TransferBitCount = 8;

    /// <summary>
    /// SC bit 7 starts or marks an active serial transfer.
    /// </summary>
    private const byte TransferStartMask = 0x80;

    /// <summary>
    /// SC bit 0 selects internal clock when set and external clock when clear.
    /// </summary>
    private const byte InternalClockMask = 0x01;

    /// <summary>
    /// Unused SC bits read back as set in DMG mode.
    /// </summary>
    private const byte ControlReadMask = 0x7E;

    /// <summary>
    /// DMG serial control stores only transfer start and clock select bits.
    /// </summary>
    private const byte ControlWriteMask = TransferStartMask | InternalClockMask;

    /// <summary>
    /// Disconnected internal-clock serial input is pulled high.
    /// </summary>
    private const byte DisconnectedInputBit = 0x01;

    private byte _control;
    private byte _outgoingTransferData;
    private int _transferredBitCount;
    private bool _masterClockHigh;
    private bool _transferActive;

    /// <summary>
    /// Raised when a serial transfer completes, carrying the byte latched at transfer start.
    /// </summary>
    internal event EventHandler<SerialByteTransferredEventArgs>? ByteTransferred;

    /// <summary>
    /// SB register at FF01, holding outgoing and incoming serial data.
    /// </summary>
    public byte TransferData { get; set; }

    /// <summary>
    /// Reads SC with unused DMG bits set.
    /// </summary>
    public byte ReadControl() => (byte)(_control | ControlReadMask);

    /// <summary>
    /// Writes SC as the CPU sees it, starting or cancelling transfer state.
    /// </summary>
    public void WriteControl(byte value)
    {
        _transferredBitCount = 0;

        if (_masterClockHigh)
        {
            TickSerialMasterClock();
        }

        _control = (byte)(value & ControlWriteMask);
        _transferActive = (_control & TransferStartMask) != 0;
        _outgoingTransferData = _transferActive ? TransferData : (byte)0;
    }

    /// <summary>
    /// Seeds SC without starting a serial transfer.
    /// </summary>
    internal void SetControlState(byte value)
    {
        _control = (byte)(value & ControlWriteMask);
        _transferredBitCount = 0;
        _transferActive = false;
        _outgoingTransferData = 0;
    }

    /// <summary>
    /// Seeds the serial master clock phase from the shared divider counter.
    /// </summary>
    internal void SetMasterClockStateFromCounter(ushort counter)
    {
        _masterClockHigh = ((counter >> 8) & 1) != 0;
    }

    /// <summary>
    /// Applies falling edges produced by the shared divider counter.
    /// </summary>
    public void TickSystemCounter(ushort fallingEdges)
    {
        if ((fallingEdges & MasterClockFallingEdgeMask) == 0)
        {
            return;
        }

        TickSerialMasterClock();
    }

    private void TickSerialMasterClock()
    {
        _masterClockHigh = !_masterClockHigh;

        if (_masterClockHigh || !_transferActive || (_control & InternalClockMask) == 0)
        {
            return;
        }

        ShiftDisconnectedInputBit();
    }

    private void ShiftDisconnectedInputBit()
    {
        TransferData = (byte)((TransferData << 1) | DisconnectedInputBit);
        _transferredBitCount++;

        if (_transferredBitCount == TransferBitCount)
        {
            CompleteTransfer();
        }
    }

    private void CompleteTransfer()
    {
        _control = (byte)(_control & ~TransferStartMask);
        _transferredBitCount = 0;
        _transferActive = false;
        interrupts.Request(InterruptSource.Serial);
        ByteTransferred?.Invoke(this, new SerialByteTransferredEventArgs(_outgoingTransferData));
    }
}
