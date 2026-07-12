// Copyright (C) 2026 thomas-fazzari
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
    private const int NormalSpeedBlockMachineCycles = 8;
    private const int DoubleSpeedBlockMachineCycles = 16;

    private byte _sourceHigh;
    private byte _sourceLow;
    private byte _destinationHigh;
    private byte _destinationLow;
    private byte _lengthModeReadValue = CompletedReadValue;

    private int _hblankBlocksRemaining;
    private int _cpuStallMachineCycles;

    private bool _isHblankDmaActive;
    private bool _cpuHalted;

    internal CgbVramDmaControllerState CaptureState() =>
        new(
            _sourceHigh,
            _sourceLow,
            _destinationHigh,
            _destinationLow,
            _lengthModeReadValue,
            _hblankBlocksRemaining,
            _cpuStallMachineCycles,
            _isHblankDmaActive,
            _cpuHalted
        );

    internal void RestoreState(CgbVramDmaControllerState state)
    {
        _sourceHigh = state.SourceHigh;
        _sourceLow = state.SourceLow;
        _destinationHigh = state.DestinationHigh;
        _destinationLow = state.DestinationLow;
        _lengthModeReadValue = state.LengthModeReadValue;
        _hblankBlocksRemaining = state.HblankBlocksRemaining;
        _cpuStallMachineCycles = state.CpuStallMachineCycles;
        _isHblankDmaActive = state.IsHblankDmaActive;
        _cpuHalted = state.CpuHalted;
    }

    /// <summary>
    /// Reads CPU-visible HDMA registers. HDMA1-HDMA4 are write-only.
    /// </summary>
    public byte ReadHdmaRegister(ushort address) =>
        isRegisterEnabled && address is AddressMap.VideoRamDmaLengthModeStartRegister
            ? _lengthModeReadValue
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
    /// Consumes one CPU-blocked VRAM DMA machine cycle, if a transfer stalled execution.
    /// </summary>
    public bool TryConsumeCpuStallMachineCycle()
    {
        if (_cpuStallMachineCycles == 0)
        {
            return false;
        }

        _cpuStallMachineCycles--;
        return true;
    }

    /// <summary>
    /// Transfers one active HBlank DMA block on a visible scanline Mode 0 entry.
    /// </summary>
    public void TransferHBlankBlock()
    {
        if (!isRegisterEnabled || !_isHblankDmaActive || _cpuHalted)
        {
            return;
        }

        var destinationAddress = GetDestinationAddress();
        if (destinationAddress > AddressMap.VideoRamEnd)
        {
            CompleteTransfer();
            return;
        }

        CopyBlock(GetSourceAddress(), destinationAddress);
        QueueCpuStall(blockCount: 1);
        AdvanceSourceAddress();
        var destinationWithinVram = TryAdvanceDestinationAddress();
        _hblankBlocksRemaining--;

        if (_hblankBlocksRemaining == 0 || !destinationWithinVram)
        {
            CompleteTransfer();
            return;
        }

        _lengthModeReadValue = (byte)(_hblankBlocksRemaining - 1);
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

        if (_isHblankDmaActive)
        {
            StopHBlankDma();
            return;
        }

        RunGeneralPurposeDma(value);
    }

    private void RunGeneralPurposeDma(byte value)
    {
        var blockCount = (value & LengthMask) + 1;
        var transferredBlocks = 0;

        for (var block = 0; block < blockCount; block++)
        {
            var destinationAddress = GetDestinationAddress();
            if (destinationAddress > AddressMap.VideoRamEnd)
            {
                break;
            }

            CopyBlock(GetSourceAddress(), destinationAddress);
            transferredBlocks++;
            AdvanceSourceAddress();

            if (!TryAdvanceDestinationAddress())
            {
                break;
            }
        }

        CompleteTransfer();
        QueueCpuStall(transferredBlocks);
    }

    private void StartHBlankDma(byte value)
    {
        _hblankBlocksRemaining = (value & LengthMask) + 1;
        _isHblankDmaActive = true;
        _lengthModeReadValue = (byte)(value & LengthMask);
    }

    private void QueueCpuStall(int blockCount)
    {
        _cpuStallMachineCycles +=
            blockCount
            * (isDoubleSpeed() ? DoubleSpeedBlockMachineCycles : NormalSpeedBlockMachineCycles);
    }

    private void CopyBlock(ushort sourceAddress, ushort destinationAddress)
    {
        for (var offset = 0; offset < BlockSize; offset++)
        {
            var currentDestinationAddress = destinationAddress + offset;

            if (currentDestinationAddress > AddressMap.VideoRamEnd)
            {
                return;
            }

            writeDestinationByte(
                (ushort)currentDestinationAddress,
                readSourceByte((ushort)(sourceAddress + offset))
            );
        }
    }

    private void CompleteTransfer()
    {
        _isHblankDmaActive = false;
        _hblankBlocksRemaining = 0;
        _lengthModeReadValue = CompletedReadValue;
    }

    private void StopHBlankDma()
    {
        _isHblankDmaActive = false;
        _lengthModeReadValue = (byte)(InactiveHBlankReadMask | (_hblankBlocksRemaining - 1));
    }

    private void SetLengthModeState(byte value)
    {
        _lengthModeReadValue = value;
        _isHblankDmaActive = (value & HBlankModeMask) == 0 && value != CompletedReadValue;
        _hblankBlocksRemaining = _isHblankDmaActive ? (value & LengthMask) + 1 : 0;
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
    byte LengthModeReadValue,
    int HblankBlocksRemaining,
    int CpuStallMachineCycles,
    bool IsHblankDmaActive,
    bool CpuHalted
);
