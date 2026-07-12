// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Memory;

namespace GbcNet.Core.Dma;

/// <summary>
/// Copies bytes from the FF46-selected source page into OAM over machine cycles.
/// </summary>
internal sealed class OamDmaController
{
    private const int SourceAddressShift = 8;
    private const int TransferLength = 0xA0;
    private const int StartupDelayMachineCycles = 2;

    private byte _registerSourceHighByte;
    private byte _activeSourceHighByte;
    private byte _pendingRestartSourceHighByte;

    private int _nextOffset;
    private int _startupDelayMachineCycles;
    private int _restartDelayMachineCycles;

    private bool _restartPending;

    /// <summary>
    /// Reads FF46 as the last OAM DMA source high byte written by CPU or boot state.
    /// </summary>
    public byte ReadRegister() => _registerSourceHighByte;

    /// <summary>
    /// Indicates that OAM DMA has been requested and has not completed yet.
    /// </summary>
    public bool IsActive { get; private set; }

    internal OamDmaControllerState CaptureState() =>
        new(
            _registerSourceHighByte,
            _activeSourceHighByte,
            _pendingRestartSourceHighByte,
            _nextOffset,
            _startupDelayMachineCycles,
            _restartDelayMachineCycles,
            _restartPending,
            IsActive
        );

    internal void RestoreState(OamDmaControllerState state)
    {
        _registerSourceHighByte = state.RegisterSourceHighByte;
        _activeSourceHighByte = state.ActiveSourceHighByte;
        _pendingRestartSourceHighByte = state.PendingRestartSourceHighByte;
        _nextOffset = state.NextOffset;
        _startupDelayMachineCycles = state.StartupDelayMachineCycles;
        _restartDelayMachineCycles = state.RestartDelayMachineCycles;
        _restartPending = state.RestartPending;
        IsActive = state.IsActive;
    }

    /// <summary>
    /// Indicates that CPU OAM reads return FF and CPU OAM writes are ignored during OAM DMA.
    /// </summary>
    public bool IsCpuOamBlocked => IsActive && _startupDelayMachineCycles == 0;

    /// <summary>
    /// Gets the most recently copied source address when OAM DMA can conflict with CPU bus access.
    /// </summary>
    public bool TryGetCpuConflictSourceAddress(out ushort sourceAddress)
    {
        if (!IsCpuOamBlocked || _nextOffset == 0)
        {
            sourceAddress = 0;
            return false;
        }

        sourceAddress = (ushort)((_activeSourceHighByte << SourceAddressShift) + _nextOffset - 1);
        return true;
    }

    /// <summary>
    /// Starts an OAM DMA transfer from sourceHighByte * 0x100.
    /// </summary>
    public void StartOamTransfer(byte sourceHighByte)
    {
        _registerSourceHighByte = sourceHighByte;

        if (IsActive)
        {
            _pendingRestartSourceHighByte = sourceHighByte;
            _restartDelayMachineCycles = StartupDelayMachineCycles;
            _restartPending = true;
            return;
        }

        StartTransfer(sourceHighByte, StartupDelayMachineCycles);
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

        if (machineCycles == 0 || (!IsActive && !_restartPending))
        {
            return;
        }

        for (var cycle = 0; cycle < machineCycles; cycle++)
        {
            TickActiveTransfer(readSourceByte, writeOamByte);
            TickPendingRestart();
        }
    }

    /// <summary>
    /// Seeds FF46 without starting OAM DMA.
    /// </summary>
    internal void SetRegisterState(byte value)
    {
        _registerSourceHighByte = value;
        _activeSourceHighByte = 0;
        _pendingRestartSourceHighByte = 0;
        _nextOffset = 0;
        IsActive = false;
        _startupDelayMachineCycles = 0;
        _restartDelayMachineCycles = 0;
        _restartPending = false;
    }

    private void StartTransfer(byte sourceHighByte, int startupDelayMachineCycles)
    {
        _activeSourceHighByte = sourceHighByte;
        _nextOffset = 0;
        _startupDelayMachineCycles = startupDelayMachineCycles;
        IsActive = true;
    }

    private void TickActiveTransfer(
        Func<ushort, byte> readSourceByte,
        Action<ushort, byte> writeOamByte
    )
    {
        if (!IsActive)
        {
            return;
        }

        if (_startupDelayMachineCycles != 0)
        {
            _startupDelayMachineCycles--;
            return;
        }

        CopyByte(readSourceByte, writeOamByte);
    }

    private void CopyByte(Func<ushort, byte> readSourceByte, Action<ushort, byte> writeOamByte)
    {
        var sourceAddress = (ushort)((_activeSourceHighByte << SourceAddressShift) + _nextOffset);
        var destinationAddress = (ushort)(AddressMap.ObjectAttributeMemoryStart + _nextOffset);

        writeOamByte(destinationAddress, readSourceByte(sourceAddress));
        _nextOffset++;

        if (_nextOffset != TransferLength)
        {
            return;
        }

        IsActive = false;
    }

    private void TickPendingRestart()
    {
        if (!_restartPending)
        {
            return;
        }

        _restartDelayMachineCycles--;

        if (_restartDelayMachineCycles != 0)
        {
            return;
        }

        _restartPending = false;
        StartTransfer(_pendingRestartSourceHighByte, startupDelayMachineCycles: 0);
    }
}

internal readonly record struct OamDmaControllerState(
    byte RegisterSourceHighByte,
    byte ActiveSourceHighByte,
    byte PendingRestartSourceHighByte,
    int NextOffset,
    int StartupDelayMachineCycles,
    int RestartDelayMachineCycles,
    bool RestartPending,
    bool IsActive
);
