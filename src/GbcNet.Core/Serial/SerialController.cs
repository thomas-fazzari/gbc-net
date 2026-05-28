using GbcNet.Core.Interrupts;

namespace GbcNet.Core.Serial;

/// <summary>
/// Emulates the DMG serial transfer data and control registers.
/// </summary>
internal sealed class SerialController(InterruptController interrupts)
{
    /// <summary>
    /// DMG internal serial clock period for one transferred bit.
    /// </summary>
    private const int InternalClockBitPeriodTCycles = 512;

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
    private int _elapsedTransferCycles;
    private int _transferredBitCount;
    private bool _transferActive;

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
        _control = (byte)(value & ControlWriteMask);
        _elapsedTransferCycles = 0;
        _transferredBitCount = 0;
        _transferActive = (_control & TransferStartMask) != 0;
    }

    /// <summary>
    /// Seeds SC without starting a serial transfer.
    /// </summary>
    internal void SetControlState(byte value)
    {
        _control = (byte)(value & ControlWriteMask);
        _elapsedTransferCycles = 0;
        _transferredBitCount = 0;
        _transferActive = false;
    }

    /// <summary>
    /// Advances an internal-clock serial transfer by elapsed T-cycles.
    /// </summary>
    public void Tick(int tCycles)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tCycles);

        if (!IsInternalClockTransferActive())
        {
            return;
        }

        _elapsedTransferCycles += tCycles;
        while (
            IsInternalClockTransferActive()
            && _elapsedTransferCycles >= InternalClockBitPeriodTCycles
        )
        {
            _elapsedTransferCycles -= InternalClockBitPeriodTCycles;
            ShiftDisconnectedInputBit();
        }
    }

    private bool IsInternalClockTransferActive() =>
        _transferActive && (_control & InternalClockMask) != 0;

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
        _elapsedTransferCycles = 0;
        _transferredBitCount = 0;
        _transferActive = false;
        interrupts.Request(InterruptSource.Serial);
    }
}
