// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace GbcNet.Core.Cartridges.Memory;

/// <summary>
/// MBC3 cartridge controller for ROM banking, optional RAM banking, and optional RTC mapping.
/// </summary>
internal sealed class Mbc3MemoryController : ICartridgeMemoryController, ICartridgeSaveData
{
    private const int RomBankSize = Cartridge.FixedRomBankSize;
    private const ushort RomBank0End = 0x3FFF;
    private const ushort RomBankNStart = 0x4000;

    private const byte RomBankMask = 0x7F;
    private const byte LastRamBank = 0x07;
    private const byte FirstRtcRegister = Mbc3RealTimeClock.SecondsRegister;
    private const byte LastRtcRegister = Mbc3RealTimeClock.DayHighRegister;

    private readonly byte[] _rom;
    private readonly CartridgeHeader _header;
    private readonly CartridgeRamWindow _externalRam;
    private readonly Mbc3RealTimeClock? _realTimeClock;

    private byte _romBank = 1;
    private byte _ramBankOrRtcRegister;

    private bool _ramAndTimerEnabled;
    private bool _rtcLatchArmed;

    public Mbc3MemoryController(
        byte[] rom,
        CartridgeHeader header,
        bool hasBatteryBackedRam,
        bool hasRealTimeClock,
        Func<long> getUnixTimeSeconds
    )
    {
        _rom = rom;
        _header = header;
        _externalRam = new CartridgeRamWindow(header.RamSizeBytes, hasBatteryBackedRam);
        _realTimeClock = hasRealTimeClock ? new Mbc3RealTimeClock(getUnixTimeSeconds) : null;
    }

    public ICartridgeSaveData SaveData => this;

    public bool HasBatteryBackedSave =>
        _realTimeClock is not null || _externalRam.Ram.HasBatteryBackedSave;

    public int BatterySaveSize =>
        _realTimeClock is null
            ? _externalRam.Ram.BatterySaveSize
            : _externalRam.Ram.BatterySaveSize + Mbc3RealTimeClock.SaveStateSize;

    public bool IsBatterySaveDirty
    {
        get
        {
            if (_realTimeClock is null)
            {
                return _externalRam.Ram.IsBatterySaveDirty;
            }

            _realTimeClock.RefreshFromClock();
            return _externalRam.Ram.IsBatterySaveDirty || _realTimeClock.IsDirty;
        }
    }

    public byte[] ExportBatterySave()
    {
        if (_realTimeClock is null)
        {
            return _externalRam.Ram.ExportBatterySave();
        }

        var data = new byte[BatterySaveSize];
        var ramData = _externalRam.Ram.ExportBatterySave();
        ramData.CopyTo(data.AsSpan(0, ramData.Length));
        _realTimeClock.ExportState().CopyTo(data.AsSpan(ramData.Length));
        return data;
    }

    public bool TryImportBatterySave(
        ReadOnlySpan<byte> data,
        [NotNullWhen(false)] out string? errorMessage
    )
    {
        if (_realTimeClock is null)
        {
            return _externalRam.Ram.TryImportBatterySave(data, out errorMessage);
        }

        if (data.Length != BatterySaveSize)
        {
            errorMessage = string.Create(
                CultureInfo.InvariantCulture,
                $"Save data length is {data.Length} bytes, but cartridge expects {BatterySaveSize} bytes."
            );
            return false;
        }

        var ramSize = _externalRam.Ram.BatterySaveSize;
        if (!_externalRam.Ram.TryImportBatterySave(data[..ramSize], out errorMessage))
        {
            return false;
        }

        _realTimeClock.ImportState(data.Slice(ramSize, Mbc3RealTimeClock.SaveStateSize));
        errorMessage = null;
        return true;
    }

    public void ClearBatterySaveDirty()
    {
        _externalRam.Ram.ClearBatterySaveDirty();
        _realTimeClock?.ClearDirty();
    }

    public ICartridgeMemoryControllerState CaptureState() =>
        new Mbc3MemoryControllerState(
            _externalRam.CaptureState(),
            _realTimeClock?.CaptureState(),
            _romBank,
            _ramBankOrRtcRegister,
            _ramAndTimerEnabled,
            _rtcLatchArmed
        );

    public void RestoreState(ICartridgeMemoryControllerState state)
    {
        if (state is not Mbc3MemoryControllerState mbc3State)
        {
            throw new ArgumentException(
                "Cartridge memory controller state is invalid.",
                nameof(state)
            );
        }

        ValidateState(mbc3State);

        if (mbc3State.RealTimeClock is { } realTimeClockState)
        {
            _realTimeClock!.RestoreState(realTimeClockState);
        }

        _externalRam.RestoreState(mbc3State.ExternalRam);
        _romBank = mbc3State.RomBank;
        _ramBankOrRtcRegister = mbc3State.RamBankOrRtcRegister;
        _ramAndTimerEnabled = mbc3State.RamAndTimerEnabled;
        _rtcLatchArmed = mbc3State.RtcLatchArmed;
    }

    private void ValidateState(Mbc3MemoryControllerState state)
    {
        if (state.RomBank > RomBankMask)
        {
            throw new ArgumentException("MBC3 ROM bank must be in the 00-7F range.", nameof(state));
        }

        if (state.ExternalRam.Enabled != state.RamAndTimerEnabled)
        {
            throw new ArgumentException(
                "MBC3 external RAM enable state must match the RAM and timer enable latch.",
                nameof(state)
            );
        }

        if (state.RealTimeClock.HasValue != (_realTimeClock is not null))
        {
            throw new ArgumentException(
                "MBC3 real-time clock state does not match the cartridge hardware.",
                nameof(state)
            );
        }

        _externalRam.ValidateState(state.ExternalRam);

        if (state.RealTimeClock is { } realTimeClockState)
        {
            Mbc3RealTimeClock.ValidateState(realTimeClockState);
        }
    }

    public byte ReadRom(ushort address)
    {
        if (address <= RomBank0End)
        {
            return _rom[address];
        }

        var bank = (_romBank == 0 ? 1 : _romBank) % _header.RomBankCount;
        return _rom[(bank * RomBankSize) + (address - RomBankNStart)];
    }

    public void WriteRom(ushort address, byte value)
    {
        switch (address)
        {
            case <= 0x1FFF:
                _ramAndTimerEnabled = (value & 0x0F) == 0x0A;
                _externalRam.WriteEnableRegister(value);
                return;
            case <= 0x3FFF:
                _romBank = (byte)(value & RomBankMask);
                return;
            case <= 0x5FFF:
                _ramBankOrRtcRegister = value;
                return;
            case <= 0x7FFF:
                WriteLatchRegister(value);
                return;
        }
    }

    public byte ReadRamOffset(ushort offset)
    {
        if (IsRamBankSelected)
        {
            return _externalRam.ReadOffset(offset, _ramBankOrRtcRegister);
        }

        return IsRtcRegisterSelected && _ramAndTimerEnabled && _realTimeClock is not null
            ? _realTimeClock.ReadRegister(_ramBankOrRtcRegister)
            : (byte)0xFF;
    }

    public void WriteRamOffset(ushort offset, byte value)
    {
        if (IsRamBankSelected)
        {
            _externalRam.WriteOffset(offset, value, _ramBankOrRtcRegister);
            return;
        }

        if (IsRtcRegisterSelected && _ramAndTimerEnabled)
        {
            _realTimeClock?.WriteRegister(_ramBankOrRtcRegister, value);
        }
    }

    private void WriteLatchRegister(byte value)
    {
        switch (value)
        {
            case 0:
                _rtcLatchArmed = true;
                return;
            case 1 when _rtcLatchArmed:
                _realTimeClock?.Latch();
                break;
        }

        _rtcLatchArmed = false;
    }

    private bool IsRamBankSelected => _ramBankOrRtcRegister <= LastRamBank;

    private bool IsRtcRegisterSelected =>
        _ramBankOrRtcRegister is >= FirstRtcRegister and <= LastRtcRegister;
}

internal sealed record Mbc3MemoryControllerState(
    CartridgeRamWindowState ExternalRam,
    Mbc3RealTimeClockState? RealTimeClock,
    byte RomBank,
    byte RamBankOrRtcRegister,
    bool RamAndTimerEnabled,
    bool RtcLatchArmed
) : ICartridgeMemoryControllerState;
