// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Memory;

namespace GbcNet.Core.Ppu;

/// <summary>
/// Stores CPU-visible VRAM with optional CGB bank selection through VBK.
/// </summary>
internal sealed class VideoRam
{
    private const int BankSize = AddressMap.VideoRamEnd - AddressMap.VideoRamStart + 1;
    private const int MinimumBankCount = 1;
    private const int MaximumBankCount = 2;
    private const int BankSelectMask = 0b1;
    private const byte BankRegisterReadMask = 0xFE;

    private readonly byte[] _banks;
    private readonly int _bankCount;
    private readonly bool _isBankRegisterEnabled;

    public VideoRam(int bankCount, bool isBankRegisterEnabled)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bankCount, MinimumBankCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bankCount, MaximumBankCount);

        _bankCount = bankCount;
        _isBankRegisterEnabled = isBankRegisterEnabled;
        _banks = new byte[bankCount * BankSize];
    }

    public int SelectedBank { get; private set; }

    public byte Read(ushort address) => _banks[GetOffset(SelectedBank, address)];

    public void Write(ushort address, byte value)
    {
        _banks[GetOffset(SelectedBank, address)] = value;
    }

    public byte ReadBank(int bank, ushort address) => _banks[GetOffset(bank, address)];

    public void WriteBank(int bank, ushort address, byte value)
    {
        _banks[GetOffset(bank, address)] = value;
    }

    public byte ReadBankRegister() =>
        _isBankRegisterEnabled ? (byte)(BankRegisterReadMask | SelectedBank) : (byte)0xFF;

    public void WriteBankRegister(byte value)
    {
        if (_isBankRegisterEnabled && _bankCount > MinimumBankCount)
        {
            SelectedBank = value & BankSelectMask;
        }
    }

    internal VideoRamState CaptureState() => new((byte[])_banks.Clone(), SelectedBank);

    internal void ValidateState(VideoRamState state)
    {
        var banks = state.Banks;
        if (banks is null || banks.Length != _banks.Length)
        {
            throw new ArgumentException(
                "State banks must match the video RAM length.",
                nameof(state)
            );
        }

        if (state.SelectedBank < 0 || state.SelectedBank >= _bankCount)
        {
            throw new ArgumentException(
                "State selected bank must identify an available video RAM bank.",
                nameof(state)
            );
        }
    }

    internal void RestoreState(VideoRamState state)
    {
        ValidateState(state);
        state.Banks.CopyTo(_banks, 0);
        SelectedBank = state.SelectedBank;
    }

    private static int GetOffset(int bank, ushort address) =>
        (bank * BankSize) + address - AddressMap.VideoRamStart;
}

internal readonly record struct VideoRamState(byte[] Banks, int SelectedBank);
