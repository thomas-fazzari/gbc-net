// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Cartridges.Memory;

/// <summary>
/// MBC1 cartridge controller for ROM banking and optional external RAM banking.
/// </summary>
internal sealed class Mbc1MemoryController(
    byte[] rom,
    CartridgeHeader header,
    bool hasBatteryBackedRam
) : ICartridgeMemoryController
{
    private const int RomBankSize = Cartridge.FixedRomBankSize;
    private const ushort RomBank0End = 0x3FFF;
    private const ushort RomBankNStart = 0x4000;

    private const byte RomBankLowMask = 0x1F;
    private const byte BankHighMask = 0x03;
    private const byte BankingModeMask = 0x01;

    private byte _romBankLow;
    private byte _bankHigh;
    private byte _bankingMode;
    private readonly CartridgeRamWindow _externalRam = new(
        header.RamSizeBytes,
        hasBatteryBackedRam
    );

    public ICartridgeSaveData SaveData => _externalRam.Ram;

    public ICartridgeMemoryControllerState CaptureState() =>
        new Mbc1MemoryControllerState(
            _externalRam.CaptureState(),
            _romBankLow,
            _bankHigh,
            _bankingMode
        );

    public void RestoreState(ICartridgeMemoryControllerState state)
    {
        if (state is not Mbc1MemoryControllerState mbc1State)
        {
            throw new ArgumentException(
                "Cartridge memory controller state is invalid.",
                nameof(state)
            );
        }

        _externalRam.ValidateState(mbc1State.ExternalRam);
        if (
            mbc1State.RomBankLow > RomBankLowMask
            || mbc1State.BankHigh > BankHighMask
            || mbc1State.BankingMode > BankingModeMask
        )
        {
            throw new ArgumentException(
                "State contains an invalid MBC1 register value.",
                nameof(state)
            );
        }

        _externalRam.RestoreState(mbc1State.ExternalRam);
        _romBankLow = mbc1State.RomBankLow;
        _bankHigh = mbc1State.BankHigh;
        _bankingMode = mbc1State.BankingMode;
    }

    public byte ReadRom(ushort address)
    {
        var bank = address <= RomBank0End ? GetFixedAreaRomBank() : GetSwitchableRomBank();
        var bankAddress = address <= RomBank0End ? address : address - RomBankNStart;
        return rom[(bank * RomBankSize) + bankAddress];
    }

    public void WriteRom(ushort address, byte value)
    {
        switch (address)
        {
            case <= 0x1FFF:
                _externalRam.WriteEnableRegister(value);
                return;
            case <= 0x3FFF:
                _romBankLow = (byte)(value & RomBankLowMask);
                return;
            case <= 0x5FFF:
                _bankHigh = (byte)(value & BankHighMask);
                return;
            case <= 0x7FFF:
                _bankingMode = (byte)(value & BankingModeMask);
                return;
        }
    }

    public byte ReadRamOffset(ushort offset) => _externalRam.ReadOffset(offset, GetRamBank());

    public void WriteRamOffset(ushort offset, byte value)
    {
        _externalRam.WriteOffset(offset, value, GetRamBank());
    }

    private int GetFixedAreaRomBank() => _bankingMode == 0 ? 0 : WrapRomBank(_bankHigh << 5);

    private int GetSwitchableRomBank()
    {
        var bank = (_bankHigh << 5) | _romBankLow;
        // MBC1 cannot select bank 00, 20, 40, or 60 in the switchable ROM window
        if ((bank & RomBankLowMask) == 0)
        {
            bank++;
        }

        return WrapRomBank(bank);
    }

    private int WrapRomBank(int bank) => bank % header.RomBankCount;

    private int GetRamBank() => _bankingMode == 0 ? 0 : _bankHigh;
}

internal sealed record Mbc1MemoryControllerState(
    CartridgeRamWindowState ExternalRam,
    byte RomBankLow,
    byte BankHigh,
    byte BankingMode
) : ICartridgeMemoryControllerState;
