// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Memory;

/// <summary>
/// Stores banked work RAM and mirrors E000-FDFF onto C000-DDFF.
/// </summary>
internal sealed class WorkRam
{
    private const int BankSize = 4 * 1024;
    private const int MinimumBankCount = 2;
    private const int BankSelectMask = 0b111;
    private const byte BankRegisterReadMask = 0xF8;
    private const byte DisabledBankRegisterValue = 0xFF;

    private readonly byte[] _banks;
    private readonly int _bankCount;
    private readonly bool _isBankRegisterEnabled;
    private byte _bankRegister = 1;
    private int _selectedSwitchableBank = 1;

    public WorkRam(int bankCount, bool isBankRegisterEnabled = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bankCount, MinimumBankCount);

        _bankCount = bankCount;
        _isBankRegisterEnabled = isBankRegisterEnabled;
        _banks = new byte[bankCount * BankSize];
    }

    public byte Read(ushort address) => _banks[GetOffset(address)];

    public void Write(ushort address, byte value)
    {
        _banks[GetOffset(address)] = value;
    }

    public byte ReadBankRegister() =>
        _isBankRegisterEnabled
            ? (byte)(BankRegisterReadMask | _bankRegister)
            : DisabledBankRegisterValue;

    public void WriteBankRegister(byte value)
    {
        if (_isBankRegisterEnabled)
        {
            SelectSwitchableBank(value);
        }
    }

    internal WorkRamState CaptureState() => new((byte[])_banks.Clone(), _bankRegister);

    internal void RestoreState(WorkRamState state)
    {
        var banks = state.Banks;
        if (banks is null || banks.Length != _banks.Length)
        {
            throw new ArgumentException(
                "State banks must match the work RAM length.",
                nameof(state)
            );
        }

        banks.CopyTo(_banks, 0);

        _bankRegister = state.BankRegister;
        _selectedSwitchableBank = GetSelectedSwitchableBank(_bankRegister);
    }

    internal void SelectSwitchableBank(byte value)
    {
        _bankRegister = (byte)(value & BankSelectMask);

        _selectedSwitchableBank = GetSelectedSwitchableBank(_bankRegister);
    }

    private int GetSelectedSwitchableBank(byte bankRegister)
    {
        int selectedBank = bankRegister;
        if (selectedBank == 0 || selectedBank >= _bankCount)
        {
            selectedBank = 1;
        }

        return selectedBank;
    }

    private int GetOffset(ushort address)
    {
        var mappedAddress =
            address >= AddressMap.EchoRamStart ? (ushort)(address - 0x2000) : address;

        return mappedAddress <= AddressMap.WorkRamFixedBankEnd
            ? mappedAddress - AddressMap.WorkRamStart
            : (_selectedSwitchableBank * BankSize)
                + (mappedAddress - AddressMap.WorkRamSwitchableBankStart);
    }
}

internal readonly record struct WorkRamState(byte[] Banks, byte BankRegister);
