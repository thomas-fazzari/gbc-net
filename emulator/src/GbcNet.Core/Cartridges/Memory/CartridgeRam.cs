// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace GbcNet.Core.Cartridges.Memory;

/// <summary>
/// Cartridge RAM storage, including battery-backed persistence state.
/// </summary>
internal sealed class CartridgeRam(int sizeBytes, bool hasBattery) : ICartridgeSaveData
{
    private readonly byte[] _bytes = new byte[sizeBytes];
    private bool _dirty;

    /// <summary>
    /// Total cartridge RAM capacity exposed by the cartridge hardware.
    /// </summary>
    public int Size => _bytes.Length;

    /// <summary>
    /// Indicates that this RAM should be persisted to a save file.
    /// </summary>
    public bool HasBatteryBackedSave => hasBattery && _bytes.Length != 0;

    /// <summary>
    /// Number of bytes exported for battery-backed save data.
    /// </summary>
    public int BatterySaveSize => HasBatteryBackedSave ? _bytes.Length : 0;

    /// <summary>
    /// Indicates that battery-backed RAM changed since the last import or clear.
    /// </summary>
    public bool IsBatterySaveDirty => HasBatteryBackedSave && _dirty;

    /// <summary>
    /// Reads a byte from an already resolved cartridge RAM offset.
    /// </summary>
    public byte Read(int offset) => _bytes[offset];

    /// <summary>
    /// Writes a byte to an already resolved cartridge RAM offset and marks save data dirty.
    /// </summary>
    public void Write(int offset, byte value)
    {
        _bytes[offset] = value;
        _dirty |= HasBatteryBackedSave;
    }

    internal CartridgeRamState CaptureState() => new((byte[])_bytes.Clone(), _dirty);

    internal void ValidateState(CartridgeRamState state)
    {
        var bytes = state.Bytes;
        if (bytes is null || bytes.Length != _bytes.Length)
        {
            throw new ArgumentException(
                "State bytes must match the cartridge RAM length.",
                nameof(state)
            );
        }

        if (state.IsDirty && !HasBatteryBackedSave)
        {
            throw new ArgumentException(
                "Non-battery-backed cartridge RAM cannot be dirty.",
                nameof(state)
            );
        }
    }

    internal void RestoreState(CartridgeRamState state)
    {
        ValidateState(state);
        state.Bytes.CopyTo(_bytes, 0);
        _dirty = state.IsDirty;
    }

    /// <summary>
    /// Exports a defensive copy of battery-backed RAM, or an empty array when unavailable.
    /// </summary>
    public byte[] ExportBatterySave() => HasBatteryBackedSave ? (byte[])_bytes.Clone() : [];

    /// <summary>
    /// Imports battery-backed RAM and validates that the save length matches cartridge RAM size.
    /// </summary>
    public bool TryImportBatterySave(
        ReadOnlySpan<byte> data,
        [NotNullWhen(false)] out string? errorMessage
    )
    {
        if (!HasBatteryBackedSave)
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

        data.CopyTo(_bytes);
        _dirty = false;
        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Marks battery-backed RAM as clean after save data has been persisted.
    /// </summary>
    public void ClearBatterySaveDirty()
    {
        _dirty = false;
    }
}

internal readonly record struct CartridgeRamState(byte[] Bytes, bool IsDirty);
