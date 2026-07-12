// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace GbcNet.Core.Cartridges.Memory;

/// <summary>
/// MBC2 built-in 512 x 4-bit RAM with optional battery-backed persistence.
/// </summary>
internal sealed class Mbc2Ram(bool hasBattery) : ICartridgeSaveData
{
    private const int RamSize = 512;
    private const byte StoredNibbleMask = 0x0F;
    private const byte ReadHighNibbleMask = 0xF0;

    private readonly byte[] _bytes = new byte[RamSize];
    private bool _dirty;

    public bool HasBatteryBackedSave => hasBattery;

    public int BatterySaveSize => hasBattery ? _bytes.Length : 0;

    public bool IsBatterySaveDirty => hasBattery && _dirty;

    /// <summary>
    /// Reads a stored nibble as an MBC2 RAM byte with high bits set.
    /// </summary>
    public byte Read(int offset) => (byte)(_bytes[offset] | ReadHighNibbleMask);

    /// <summary>
    /// Stores the low nibble and marks battery-backed RAM dirty.
    /// </summary>
    public void Write(int offset, byte value)
    {
        _bytes[offset] = (byte)(value & StoredNibbleMask);
        _dirty |= hasBattery;
    }

    public byte[] ExportBatterySave() => hasBattery ? (byte[])_bytes.Clone() : [];

    public bool TryImportBatterySave(
        ReadOnlySpan<byte> data,
        [NotNullWhen(false)] out string? errorMessage
    )
    {
        if (!hasBattery)
        {
            errorMessage = data.IsEmpty ? null : "Cartridge has no battery-backed RAM.";
            return data.IsEmpty;
        }

        if (data.Length != _bytes.Length)
        {
            errorMessage = string.Format(
                CultureInfo.InvariantCulture,
                "Save RAM length is {0} bytes, but cartridge expects {1} bytes.",
                data.Length,
                _bytes.Length
            );
            return false;
        }

        for (var index = 0; index < data.Length; index++)
        {
            _bytes[index] = (byte)(data[index] & StoredNibbleMask);
        }

        _dirty = false;
        errorMessage = null;
        return true;
    }

    public void ClearBatterySaveDirty()
    {
        _dirty = false;
    }

    internal Mbc2RamState CaptureState() => new((byte[])_bytes.Clone(), _dirty);

    internal void RestoreState(Mbc2RamState state)
    {
        ValidateState(state);
        state.Bytes.CopyTo(_bytes, 0);
        _dirty = state.IsDirty;
    }

    internal void ValidateState(Mbc2RamState state)
    {
        if (state.Bytes is null)
        {
            throw new ArgumentException("MBC2 RAM state bytes must not be null.", nameof(state));
        }

        if (state.Bytes.Length != RamSize)
        {
            throw new ArgumentException(
                $"MBC2 RAM state must contain exactly {RamSize} bytes.",
                nameof(state)
            );
        }

        foreach (var value in state.Bytes)
        {
            if ((value & ~StoredNibbleMask) != 0)
            {
                throw new ArgumentException(
                    "MBC2 RAM state bytes must contain only low nibbles.",
                    nameof(state)
                );
            }
        }

        if (state.IsDirty && !hasBattery)
        {
            throw new ArgumentException(
                "MBC2 RAM state cannot be dirty without battery-backed RAM.",
                nameof(state)
            );
        }
    }
}

internal readonly record struct Mbc2RamState(byte[] Bytes, bool IsDirty);
