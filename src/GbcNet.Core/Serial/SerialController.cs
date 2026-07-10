// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Interrupts;

namespace GbcNet.Core.Serial;

/// <summary>
/// Emulates the serial transfer data and control registers.
/// </summary>
internal sealed class SerialController(
    InterruptController interrupts,
    bool isHighSpeedClockEnabled = false
)
{
    private const ushort NormalMasterClockFallingEdgeMask = 1 << 7;
    private const ushort HighSpeedMasterClockFallingEdgeMask = 1 << 2;

    /// <summary>
    /// Serial transfers complete after shifting eight bits.
    /// </summary>
    private const int TransferBitCount = 8;

    /// <summary>
    /// SC bit 7 starts or marks an active serial transfer.
    /// </summary>
    private const byte TransferStartMask = 0x80;

    /// <summary>
    /// SC bit 1 selects the CGB high-speed internal clock when set.
    /// </summary>
    private const byte HighSpeedClockMask = 0x02;

    /// <summary>
    /// SC bit 0 selects internal clock when set and external clock when clear.
    /// </summary>
    private const byte InternalClockMask = 0x01;

    // Unused SC bits read back as set.
    private const byte DmgControlReadMask = 0x7E;
    private const byte CgbControlReadMask = 0x7C;

    /// <summary>
    /// SC stores transfer start, clock select, and in CGB mode clock speed.
    /// </summary>
    private const byte DmgControlWriteMask = TransferStartMask | InternalClockMask;
    private const byte CgbControlWriteMask = DmgControlWriteMask | HighSpeedClockMask;

    /// <summary>
    /// Disconnected internal-clock serial input is pulled high.
    /// </summary>
    private const byte DisconnectedInputBit = 0x01;

    private byte _control;
    private byte _outgoingTransferData;
    private int _transferredBitCount;
    private bool _normalMasterClockHigh;
    private bool _highSpeedMasterClockHigh;
    private bool _transferActive;

    // The raw payload avoids allocating an EventArgs wrapper for every completed transfer.
#pragma warning disable MA0046
    /// <summary>
    /// Raised when a serial transfer completes, carrying the byte latched at transfer start.
    /// </summary>
    internal event Action<byte>? ByteTransferred;
#pragma warning restore MA0046

    /// <summary>
    /// SB register at FF01, holding outgoing and incoming serial data.
    /// </summary>
    public byte TransferData { get; set; }

    /// <summary>
    /// Reads SC with unused bits set.
    /// </summary>
    public byte ReadControl() =>
        (byte)(_control | (isHighSpeedClockEnabled ? CgbControlReadMask : DmgControlReadMask));

    /// <summary>
    /// Writes SC as the CPU sees it, starting or cancelling transfer state.
    /// </summary>
    public void WriteControl(byte value)
    {
        var control = (byte)(
            value & (isHighSpeedClockEnabled ? CgbControlWriteMask : DmgControlWriteMask)
        );

        _transferredBitCount = 0;

        LowerSelectedMasterClock(control);

        _control = control;
        _transferActive = (_control & TransferStartMask) != 0;
        _outgoingTransferData = _transferActive ? TransferData : (byte)0;
    }

    /// <summary>
    /// Seeds SC without starting a serial transfer.
    /// </summary>
    internal void SetControlState(byte value)
    {
        _control = (byte)(
            value & (isHighSpeedClockEnabled ? CgbControlWriteMask : DmgControlWriteMask)
        );

        _transferredBitCount = 0;
        _transferActive = false;
        _outgoingTransferData = 0;
    }

    /// <summary>
    /// Seeds the serial master clock phase from the shared divider counter.
    /// </summary>
    internal void SetMasterClockStateFromCounter(ushort counter)
    {
        _normalMasterClockHigh = ((counter >> 8) & 1) != 0;
        _highSpeedMasterClockHigh = ((counter >> 3) & 1) != 0;
    }

    /// <summary>
    /// Applies falling edges produced by the shared divider counter.
    /// </summary>
    public void TickSystemCounter(ushort fallingEdges)
    {
        var highSpeedClockSelected = IsHighSpeedClockSelected(_control);

        if ((fallingEdges & NormalMasterClockFallingEdgeMask) != 0)
        {
            _normalMasterClockHigh = !_normalMasterClockHigh;
            if (!highSpeedClockSelected)
            {
                TickSerialMasterClock(_normalMasterClockHigh);
            }
        }

        if (!isHighSpeedClockEnabled || (fallingEdges & HighSpeedMasterClockFallingEdgeMask) == 0)
        {
            return;
        }

        _highSpeedMasterClockHigh = !_highSpeedMasterClockHigh;
        if (highSpeedClockSelected)
        {
            TickSerialMasterClock(_highSpeedMasterClockHigh);
        }
    }

    private void LowerSelectedMasterClock(byte control)
    {
        if (IsHighSpeedClockSelected(control))
        {
            _highSpeedMasterClockHigh = false;
            return;
        }

        _normalMasterClockHigh = false;
    }

    private void TickSerialMasterClock(bool masterClockHigh)
    {
        if (masterClockHigh || !_transferActive || (_control & InternalClockMask) == 0)
        {
            return;
        }

        ShiftDisconnectedInputBit();
    }

    private bool IsHighSpeedClockSelected(byte control) =>
        isHighSpeedClockEnabled && (control & HighSpeedClockMask) != 0;

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
        ByteTransferred?.Invoke(_outgoingTransferData);
    }
}
