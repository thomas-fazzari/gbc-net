// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Memory;

namespace GbcNet.Core.Dma;

/// <summary>
/// Stores CGB HDMA registers and transfers General Purpose DMA / visible-HBlank blocks into the selected VRAM bank.
/// </summary>
internal sealed class CgbVramDmaController(
    bool isRegisterEnabled,
    Func<bool> isDoubleSpeed,
    Func<ushort, byte> readSourceByte,
    Action<ushort, byte> writeDestinationByte
)
{
    private const byte HBlankModeMask = 0x80;
    private const byte LengthMask = 0x7F;
    private const byte SourceLowMask = 0xF0;
    private const byte DestinationHighMask = 0x1F;
    private const byte DestinationLowMask = 0xF0;
    private const byte CompletedReadValue = 0xFF;
    private const byte InactiveHBlankReadMask = 0x80;
    private const int BlockSize = 0x10;
    private const int NormalSpeedBytesPerMachineCycle = 2;
    private const int DoubleSpeedBytesPerMachineCycle = 1;

    private byte _sourceHigh;
    private byte _sourceLow;
    private byte _destinationHigh;
    private byte _destinationLow;
    private int _blocksRemaining;
    private int _bytesRemainingInCurrentBlock;
    private VramDmaTransferMode _transferMode;
    private bool _transferStartPending;
    private bool _cpuHalted;

    internal CgbVramDmaControllerState CaptureState() =>
        new(
            _sourceHigh,
            _sourceLow,
            _destinationHigh,
            _destinationLow,
            _transferMode,
            _blocksRemaining,
            _bytesRemainingInCurrentBlock,
            _transferStartPending,
            _cpuHalted
        );

    internal void ValidateState(CgbVramDmaControllerState state)
    {
        if ((state.SourceLow & ~SourceLowMask) != 0)
        {
            throw new ArgumentException(
                "State VRAM DMA source low register contains unsupported bits.",
                nameof(state)
            );
        }

        if (
            (state.DestinationHigh & ~DestinationHighMask) != 0
            || (state.DestinationLow & ~DestinationLowMask) != 0
        )
        {
            throw new ArgumentException(
                "State VRAM DMA destination registers contain unsupported bits.",
                nameof(state)
            );
        }

        if (!Enum.IsDefined(state.TransferMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                state.TransferMode,
                "State VRAM DMA transfer mode is invalid."
            );
        }

        if (state.BlocksRemaining is < 0 or > LengthMask + 1)
        {
            throw new ArgumentException(
                "State VRAM DMA block count is out of range.",
                nameof(state)
            );
        }

        if (state.BytesRemainingInCurrentBlock is < 0 or > BlockSize)
        {
            throw new ArgumentException(
                "State VRAM DMA current block byte count is out of range.",
                nameof(state)
            );
        }

        if (
            (state.TransferMode is not VramDmaTransferMode.Inactive && state.BlocksRemaining == 0)
            || (
                state.TransferMode is VramDmaTransferMode.General
                && state.BytesRemainingInCurrentBlock == 0
            )
            || (
                state.TransferMode is VramDmaTransferMode.Inactive
                && (state.BytesRemainingInCurrentBlock != 0 || state.TransferStartPending)
            )
            || state
                is { TransferStartPending: true, BytesRemainingInCurrentBlock: not BlockSize }
                    or { CpuHalted: true, BytesRemainingInCurrentBlock: > 0 }
        )
        {
            throw new ArgumentException(
                "State VRAM DMA transfer progress is invalid.",
                nameof(state)
            );
        }

        if (
            !isRegisterEnabled
            && state
                is not {
                    SourceHigh: 0,
                    SourceLow: 0,
                    DestinationHigh: 0,
                    DestinationLow: 0,
                    TransferMode: VramDmaTransferMode.Inactive,
                    BlocksRemaining: 0,
                    BytesRemainingInCurrentBlock: 0,
                    TransferStartPending: false,
                }
        )
        {
            throw new ArgumentException(
                "State VRAM DMA must be inert when its registers are disabled.",
                nameof(state)
            );
        }
    }

    internal void RestoreState(CgbVramDmaControllerState state)
    {
        ValidateState(state);
        _sourceHigh = state.SourceHigh;
        _sourceLow = state.SourceLow;
        _destinationHigh = state.DestinationHigh;
        _destinationLow = state.DestinationLow;
        _transferMode = state.TransferMode;
        _blocksRemaining = state.BlocksRemaining;
        _bytesRemainingInCurrentBlock = state.BytesRemainingInCurrentBlock;
        _transferStartPending = state.TransferStartPending;
        _cpuHalted = state.CpuHalted;
    }

    /// <summary>
    /// Reads CPU-visible HDMA registers. HDMA1-HDMA4 are write-only.
    /// </summary>
    public byte ReadHdmaRegister(ushort address) =>
        isRegisterEnabled && address is AddressMap.VideoRamDmaLengthModeStartRegister
            ? GetLengthModeReadValue()
            : CompletedReadValue;

    /// <summary>
    /// Writes a CPU-visible HDMA register and starts General Purpose DMA through HDMA5.
    /// </summary>
    public void WriteHdmaRegister(ushort address, byte value)
    {
        if (!isRegisterEnabled)
        {
            return;
        }

        WriteRegisterState(address, value, startTransfer: true);
    }

    /// <summary>
    /// Seeds an HDMA register without starting a transfer.
    /// </summary>
    public void SetHdmaRegisterState(ushort address, byte value)
    {
        if (!isRegisterEnabled)
        {
            return;
        }

        WriteRegisterState(address, value, startTransfer: false);
    }

    /// <summary>
    /// Records whether CPU HALT currently pauses HBlank DMA block transfers.
    /// </summary>
    public void SetCpuHalted(bool value)
    {
        _cpuHalted = value;
    }

    /// <summary>
    /// Indicates that the CPU is blocked while one VRAM DMA block is transferring.
    /// </summary>
    public bool IsCpuStalled => _bytesRemainingInCurrentBlock > 0;

    /// <summary>
    /// Starts one active HBlank DMA block on a visible scanline Mode 0 entry.
    /// </summary>
    public void BeginHBlankBlock()
    {
        if (
            !isRegisterEnabled
            || _transferMode is not VramDmaTransferMode.HBlank
            || _bytesRemainingInCurrentBlock > 0
            || _cpuHalted
        )
        {
            return;
        }

        _bytesRemainingInCurrentBlock = BlockSize;
        _transferStartPending = true;
    }

    /// <summary>
    /// Advances an active transfer by one elapsed CPU machine cycle.
    /// </summary>
    public void TickMachineCycle()
    {
        if (_bytesRemainingInCurrentBlock == 0)
        {
            return;
        }

        if (_transferStartPending)
        {
            _transferStartPending = false;
            return;
        }

        if (_transferMode is VramDmaTransferMode.HBlank && _cpuHalted)
        {
            return;
        }

        var bytesToTransfer = Math.Min(
            _bytesRemainingInCurrentBlock,
            isDoubleSpeed() ? DoubleSpeedBytesPerMachineCycle : NormalSpeedBytesPerMachineCycle
        );
        var blockOffset = BlockSize - _bytesRemainingInCurrentBlock;
        var sourceAddress = GetSourceAddress();
        var destinationAddress = GetDestinationAddress();

        for (var offset = 0; offset < bytesToTransfer; offset++)
        {
            var byteOffset = blockOffset + offset;
            writeDestinationByte(
                (ushort)(destinationAddress + byteOffset),
                readSourceByte((ushort)(sourceAddress + byteOffset))
            );
        }

        _bytesRemainingInCurrentBlock -= bytesToTransfer;
        if (_bytesRemainingInCurrentBlock == 0)
        {
            CompleteBlock();
        }
    }

    private void WriteRegisterState(ushort address, byte value, bool startTransfer)
    {
        switch (address)
        {
            case AddressMap.VideoRamDmaSourceHighRegister:
                _sourceHigh = value;
                return;

            case AddressMap.VideoRamDmaSourceLowRegister:
                _sourceLow = (byte)(value & SourceLowMask);
                return;

            case AddressMap.VideoRamDmaDestinationHighRegister:
                _destinationHigh = (byte)(value & DestinationHighMask);
                return;

            case AddressMap.VideoRamDmaDestinationLowRegister:
                _destinationLow = (byte)(value & DestinationLowMask);
                return;

            case AddressMap.VideoRamDmaLengthModeStartRegister:
                if (startTransfer)
                {
                    WriteLengthMode(value);
                }
                else
                {
                    SetLengthModeState(value);
                }

                return;
        }
    }

    private void WriteLengthMode(byte value)
    {
        if ((value & HBlankModeMask) != 0)
        {
            StartHBlankDma(value);
            return;
        }

        if (_transferMode is VramDmaTransferMode.HBlank)
        {
            StopHBlankDma();
            return;
        }

        StartGeneralPurposeDma(value);
    }

    private void StartGeneralPurposeDma(byte value)
    {
        _blocksRemaining = (value & LengthMask) + 1;
        _bytesRemainingInCurrentBlock = BlockSize;
        _transferMode = VramDmaTransferMode.General;
        _transferStartPending = true;
    }

    private void StartHBlankDma(byte value)
    {
        _blocksRemaining = (value & LengthMask) + 1;
        _bytesRemainingInCurrentBlock = 0;
        _transferMode = VramDmaTransferMode.HBlank;
        _transferStartPending = false;
    }

    private void CompleteBlock()
    {
        AdvanceSourceAddress();
        var destinationWithinVram = TryAdvanceDestinationAddress();
        _blocksRemaining--;

        if (_blocksRemaining == 0 || !destinationWithinVram)
        {
            CompleteTransfer();
            return;
        }

        _bytesRemainingInCurrentBlock =
            _transferMode is VramDmaTransferMode.General ? BlockSize : 0;
    }

    private void CompleteTransfer()
    {
        _transferMode = VramDmaTransferMode.Inactive;
        _blocksRemaining = 0;
        _bytesRemainingInCurrentBlock = 0;
        _transferStartPending = false;
    }

    private void StopHBlankDma()
    {
        _transferMode = VramDmaTransferMode.Inactive;
        _bytesRemainingInCurrentBlock = 0;
        _transferStartPending = false;
    }

    private void SetLengthModeState(byte value)
    {
        _bytesRemainingInCurrentBlock = 0;
        _transferStartPending = false;

        if (value == CompletedReadValue)
        {
            _transferMode = VramDmaTransferMode.Inactive;
            _blocksRemaining = 0;
            return;
        }

        _blocksRemaining = (value & LengthMask) + 1;
        _transferMode =
            (value & HBlankModeMask) == 0
                ? VramDmaTransferMode.HBlank
                : VramDmaTransferMode.Inactive;
    }

    private byte GetLengthModeReadValue()
    {
        if (_transferMode is not VramDmaTransferMode.Inactive)
        {
            return (byte)(_blocksRemaining - 1);
        }

        return _blocksRemaining > 0
            ? (byte)(InactiveHBlankReadMask | (_blocksRemaining - 1))
            : CompletedReadValue;
    }

    private ushort GetSourceAddress() => (ushort)((_sourceHigh << 8) | _sourceLow);

    private void AdvanceSourceAddress()
    {
        var address = (ushort)(GetSourceAddress() + BlockSize);
        _sourceHigh = (byte)(address >> 8);
        _sourceLow = (byte)(address & SourceLowMask);
    }

    private ushort GetDestinationAddress() =>
        (ushort)(AddressMap.VideoRamStart | (_destinationHigh << 8) | _destinationLow);

    private bool TryAdvanceDestinationAddress()
    {
        var address = GetDestinationAddress() + BlockSize;
        if (address > AddressMap.VideoRamEnd)
        {
            return false;
        }

        var offset = address - AddressMap.VideoRamStart;
        _destinationHigh = (byte)((offset >> 8) & DestinationHighMask);
        _destinationLow = (byte)(offset & DestinationLowMask);
        return true;
    }
}

internal readonly record struct CgbVramDmaControllerState(
    byte SourceHigh,
    byte SourceLow,
    byte DestinationHigh,
    byte DestinationLow,
    VramDmaTransferMode TransferMode,
    int BlocksRemaining,
    int BytesRemainingInCurrentBlock,
    bool TransferStartPending,
    bool CpuHalted
);

internal enum VramDmaTransferMode
{
    Inactive = 0,
    General = 1,
    HBlank = 2,
}
