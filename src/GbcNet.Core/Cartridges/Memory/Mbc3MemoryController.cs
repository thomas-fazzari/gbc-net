// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Cartridges.Memory;

/// <summary>
/// MBC3 cartridge controller for ROM banking, optional RAM banking, and optional RTC mapping.
/// </summary>
internal sealed class Mbc3MemoryController : ICartridgeMemoryController
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
        SaveData = _realTimeClock is null
            ? _externalRam.Ram
            : new Mbc3SaveData(_externalRam.Ram, _realTimeClock);
    }

    public ICartridgeSaveData SaveData { get; }

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
