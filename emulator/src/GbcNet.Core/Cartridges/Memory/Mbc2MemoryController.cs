// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Cartridges.Memory;

/// <summary>
/// MBC2 cartridge controller with 4-bit built-in RAM and 4-bit ROM banking.
/// </summary>
internal sealed class Mbc2MemoryController(
    byte[] rom,
    CartridgeHeader header,
    bool hasBatteryBackedRam
) : ICartridgeMemoryController
{
    private const int RomBankSize = Cartridge.FixedRomBankSize;
    private const ushort RomBank0End = 0x3FFF;
    private const ushort RomBankNStart = 0x4000;
    private const ushort RegisterAddressBit8 = 0x0100;
    private const byte RomBankMask = 0x0F;
    private const ushort RamOffsetMask = 0x01FF;

    private readonly Mbc2Ram _ram = new(hasBatteryBackedRam);
    private byte _romBank = 1;
    private bool _ramEnabled;

    public ICartridgeSaveData SaveData => _ram;

    public byte ReadRom(ushort address)
    {
        if (address <= RomBank0End)
        {
            return rom[address];
        }

        var bank = _romBank % header.RomBankCount;
        return rom[(bank * RomBankSize) + (address - RomBankNStart)];
    }

    public void WriteRom(ushort address, byte value)
    {
        if (address > RomBank0End)
        {
            return;
        }

        if ((address & RegisterAddressBit8) == 0)
        {
            _ramEnabled = (value & RomBankMask) == 0x0A;
            return;
        }

        _romBank = (byte)(value & RomBankMask);
        if (_romBank == 0)
        {
            _romBank = 1;
        }
    }

    public byte ReadRamOffset(ushort offset) =>
        _ramEnabled ? _ram.Read(offset & RamOffsetMask) : (byte)0xFF;

    public void WriteRamOffset(ushort offset, byte value)
    {
        if (_ramEnabled)
        {
            _ram.Write(offset & RamOffsetMask, value);
        }
    }

    public ICartridgeMemoryControllerState CaptureState() =>
        new Mbc2MemoryControllerState(_ram.CaptureState(), _romBank, _ramEnabled);

    public void ValidateState(ICartridgeMemoryControllerState state)
    {
        if (state is not Mbc2MemoryControllerState mbc2State)
        {
            throw new ArgumentException(
                "Cartridge memory controller state is invalid.",
                nameof(state)
            );
        }

        _ram.ValidateState(mbc2State.Ram);

        if (mbc2State.RomBank is < 1 or > RomBankMask)
        {
            throw new ArgumentException("MBC2 ROM bank must be in the 1-15 range.", nameof(state));
        }
    }

    public void RestoreState(ICartridgeMemoryControllerState state)
    {
        ValidateState(state);
        var mbc2State = (Mbc2MemoryControllerState)state;
        _ram.RestoreState(mbc2State.Ram);
        _romBank = mbc2State.RomBank;
        _ramEnabled = mbc2State.RamEnabled;
    }
}

internal sealed record Mbc2MemoryControllerState(Mbc2RamState Ram, byte RomBank, bool RamEnabled)
    : ICartridgeMemoryControllerState;
