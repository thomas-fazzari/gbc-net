// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Cartridges.Memory;

/// <summary>
/// MBC5 cartridge controller for 9-bit ROM banking and optional external RAM banking.
/// </summary>
internal sealed class Mbc5MemoryController(
    byte[] rom,
    CartridgeHeader header,
    bool hasBatteryBackedRam,
    bool hasRumble
) : ICartridgeMemoryController
{
    private const int RomBankSize = Cartridge.FixedRomBankSize;
    private const ushort RomBank0End = 0x3FFF;
    private const ushort RomBankNStart = 0x4000;

    private const byte RomBankHighMask = 0x01;
    private const byte StandardRamBankMask = 0x0F;
    private const byte RumbleRamBankMask = 0x07;
    private const byte RumbleActiveMask = 0x08;

    private byte _romBankLow = 1;
    private byte _romBankHigh;
    private byte _ramBank;
    private readonly CartridgeRamWindow _externalRam = new(
        header.RamSizeBytes,
        hasBatteryBackedRam
    );

    public bool IsRumbleActive { get; private set; }

    public ICartridgeSaveData SaveData => _externalRam.Ram;

    private byte RamBankMask => hasRumble ? RumbleRamBankMask : StandardRamBankMask;

    public ICartridgeMemoryControllerState CaptureState() =>
        new Mbc5MemoryControllerState(
            _externalRam.CaptureState(),
            _romBankLow,
            _romBankHigh,
            _ramBank,
            IsRumbleActive
        );

    public void ValidateState(ICartridgeMemoryControllerState state)
    {
        var mbc5State = CartridgeStateValidator.ValidateControllerState<Mbc5MemoryControllerState>(
            state
        );

        _externalRam.ValidateState(mbc5State.ExternalRam);

        if (mbc5State.RomBankHigh > RomBankHighMask)
        {
            throw new ArgumentException("ROM bank high bit must be 0 or 1.", nameof(state));
        }

        if (mbc5State.RamBank > RamBankMask)
        {
            throw new ArgumentException(
                "RAM bank exceeds this MBC5 variant's range.",
                nameof(state)
            );
        }

        if (!hasRumble && mbc5State.IsRumbleActive)
        {
            throw new ArgumentException(
                "Rumble cannot be active on this MBC5 variant.",
                nameof(state)
            );
        }
    }

    public void RestoreState(ICartridgeMemoryControllerState state)
    {
        ValidateState(state);
        var mbc5State = (Mbc5MemoryControllerState)state;
        _externalRam.RestoreState(mbc5State.ExternalRam);
        _romBankLow = mbc5State.RomBankLow;
        _romBankHigh = mbc5State.RomBankHigh;
        _ramBank = mbc5State.RamBank;
        IsRumbleActive = mbc5State.IsRumbleActive;
    }

    public byte ReadRom(ushort address)
    {
        if (address <= RomBank0End)
        {
            return rom[address];
        }

        var bank = ((_romBankHigh << 8) | _romBankLow) % header.RomBankCount;
        return rom[(bank * RomBankSize) + (address - RomBankNStart)];
    }

    public void WriteRom(ushort address, byte value)
    {
        switch (address)
        {
            case <= 0x1FFF:
                _externalRam.WriteEnableRegister(value);
                return;
            case <= 0x2FFF:
                _romBankLow = value;
                return;
            case <= 0x3FFF:
                _romBankHigh = (byte)(value & RomBankHighMask);
                return;
            case <= 0x5FFF:
                _ramBank = (byte)(value & RamBankMask);
                IsRumbleActive = hasRumble && (value & RumbleActiveMask) != 0;
                return;
        }
    }

    public byte ReadRamOffset(ushort offset) => _externalRam.ReadOffset(offset, _ramBank);

    public void WriteRamOffset(ushort offset, byte value)
    {
        _externalRam.WriteOffset(offset, value, _ramBank);
    }
}

internal sealed record Mbc5MemoryControllerState(
    CartridgeRamWindowState ExternalRam,
    byte RomBankLow,
    byte RomBankHigh,
    byte RamBank,
    bool IsRumbleActive
) : ICartridgeMemoryControllerState;
