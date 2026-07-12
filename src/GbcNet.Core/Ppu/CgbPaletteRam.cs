// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Ppu;

internal readonly record struct CgbPaletteRamState(byte[] Bytes, byte Index);

/// <summary>
/// Stores one CGB color palette RAM, with index visibility separated from data access.
/// </summary>
internal sealed class CgbPaletteRam(bool isIndexRegisterEnabled, bool isDataRegisterEnabled)
{
    private const int PaletteRamSize = 64;
    private const byte IndexMask = 0x3F;
    private const byte IndexReadMask = 0x40;
    private const byte AutoIncrementMask = 0x80;
    private const byte DisabledRegisterValue = 0xFF;

    private readonly byte[] _bytes = new byte[PaletteRamSize];
    private byte _index;

    public byte ReadIndexRegister() =>
        isIndexRegisterEnabled ? (byte)(IndexReadMask | _index) : DisabledRegisterValue;

    public void WriteIndexRegister(byte value)
    {
        if (isIndexRegisterEnabled)
        {
            _index = (byte)(value & (AutoIncrementMask | IndexMask));
        }
    }

    public byte ReadDataRegister() =>
        isDataRegisterEnabled ? _bytes[_index & IndexMask] : DisabledRegisterValue;

    public ushort ReadRgb555Color(int paletteIndex, byte colorId)
    {
        var offset = GetColorOffset(paletteIndex, colorId);

        return (ushort)(_bytes[offset] | (_bytes[offset + 1] << 8));
    }

    internal void SetAllColorsRgb555(ushort color)
    {
        var lowByte = (byte)color;
        var highByte = (byte)(color >> 8);

        for (var offset = 0; offset < _bytes.Length; offset += 2)
        {
            _bytes[offset] = lowByte;
            _bytes[offset + 1] = highByte;
        }
    }

    internal void SetRgb555Color(int paletteIndex, byte colorId, ushort color)
    {
        var offset = GetColorOffset(paletteIndex, colorId);
        _bytes[offset] = (byte)color;
        _bytes[offset + 1] = (byte)(color >> 8);
    }

    internal CgbPaletteRamState CaptureState() => new((byte[])_bytes.Clone(), _index);

    internal void ValidateState(CgbPaletteRamState state)
    {
        var bytes = state.Bytes;
        if (bytes is null || bytes.Length != _bytes.Length)
        {
            throw new ArgumentException(
                "State bytes must match the CGB palette RAM length.",
                nameof(state)
            );
        }

        if ((state.Index & ~(AutoIncrementMask | IndexMask)) != 0)
        {
            throw new ArgumentException(
                "State palette index contains unsupported bits.",
                nameof(state)
            );
        }
    }

    internal void RestoreState(CgbPaletteRamState state)
    {
        ValidateState(state);
        state.Bytes.CopyTo(_bytes, 0);
        _index = state.Index;
    }

    private static int GetColorOffset(int paletteIndex, byte colorId) =>
        (((paletteIndex & 0x07) * 4) + (colorId & 0x03)) * 2;

    public void WriteDataRegister(byte value)
    {
        if (!isDataRegisterEnabled)
        {
            return;
        }

        _bytes[_index & IndexMask] = value;

        if ((_index & AutoIncrementMask) != 0)
        {
            _index = (byte)(AutoIncrementMask | ((_index + 1) & IndexMask));
        }
    }
}
