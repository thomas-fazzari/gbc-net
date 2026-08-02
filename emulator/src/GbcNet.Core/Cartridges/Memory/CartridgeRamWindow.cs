// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Memory;

namespace GbcNet.Core.Cartridges.Memory;

/// <summary>
/// External cartridge RAM window shared by banked MBC implementations.
/// </summary>
internal sealed class CartridgeRamWindow(int sizeBytes, bool hasBattery)
{
    private const int RamBankSize = AddressMap.ExternalRamWindowSize;
    private const byte RamEnableValue = 0x0A;
    private const byte RamEnableMask = 0x0F;

    private bool _enabled;

    /// <summary>
    /// Backing RAM behind the CPU-visible A000-BFFF window.
    /// </summary>
    public CartridgeRam Ram { get; } = new(sizeBytes, hasBattery);

    /// <summary>
    /// Updates the MBC RAM-enable latch using the low nibble of a ROM-area write.
    /// </summary>
    public void WriteEnableRegister(byte value)
    {
        _enabled = (value & RamEnableMask) == RamEnableValue;
    }

    /// <summary>
    /// Reads from the selected RAM bank, returning FF while RAM is disabled or absent.
    /// </summary>
    public byte ReadOffset(ushort offset, int bank) =>
        !CanAccess ? (byte)0xFF : Ram.Read(GetEffectiveOffset(offset, bank));

    /// <summary>
    /// Writes to the selected RAM bank when enabled; disabled or absent RAM ignores writes.
    /// </summary>
    public void WriteOffset(ushort offset, byte value, int bank)
    {
        if (CanAccess)
        {
            Ram.Write(GetEffectiveOffset(offset, bank), value);
        }
    }

    private bool CanAccess => _enabled && Ram.Size != 0;

    internal CartridgeRamWindowState CaptureState() => new(Ram.CaptureState(), _enabled);

    internal void ValidateState(CartridgeRamWindowState state)
    {
        Ram.ValidateState(state.Ram);
    }

    internal void RestoreState(CartridgeRamWindowState state)
    {
        ValidateState(state);
        Ram.RestoreState(state.Ram);
        _enabled = state.Enabled;
    }

    private int GetEffectiveOffset(ushort offset, int bank)
    {
        var effectiveOffset = (bank * RamBankSize) + offset;
        return effectiveOffset % Ram.Size;
    }
}

internal readonly record struct CartridgeRamWindowState(CartridgeRamState Ram, bool Enabled);
