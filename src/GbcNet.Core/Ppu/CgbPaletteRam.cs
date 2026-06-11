namespace GbcNet.Core.Ppu;

/// <summary>
/// Stores one CGB color palette RAM; CPU index/data registers can be disabled in non-CGB mode.
/// </summary>
internal sealed class CgbPaletteRam(bool isCpuRegisterEnabled)
{
    private const int PaletteRamSize = 64;
    private const byte IndexMask = 0x3F;
    private const byte IndexReadMask = 0x40;
    private const byte AutoIncrementMask = 0x80;
    private const byte DisabledRegisterValue = 0xFF;

    private readonly byte[] _bytes = new byte[PaletteRamSize];
    private byte _index;

    public byte ReadIndexRegister() =>
        isCpuRegisterEnabled ? (byte)(IndexReadMask | _index) : DisabledRegisterValue;

    public void WriteIndexRegister(byte value)
    {
        if (isCpuRegisterEnabled)
        {
            _index = (byte)(value & (AutoIncrementMask | IndexMask));
        }
    }

    public byte ReadDataRegister() =>
        isCpuRegisterEnabled ? _bytes[_index & IndexMask] : DisabledRegisterValue;

    public ushort ReadRgb555Color(int paletteIndex, byte colorId)
    {
        int offset = GetColorOffset(paletteIndex, colorId);

        return (ushort)(_bytes[offset] | (_bytes[offset + 1] << 8));
    }

    internal void SetAllColorsRgb555(ushort color)
    {
        byte lowByte = (byte)color;
        byte highByte = (byte)(color >> 8);

        for (int offset = 0; offset < _bytes.Length; offset += 2)
        {
            _bytes[offset] = lowByte;
            _bytes[offset + 1] = highByte;
        }
    }

    internal void SetRgb555Color(int paletteIndex, byte colorId, ushort color)
    {
        int offset = GetColorOffset(paletteIndex, colorId);
        _bytes[offset] = (byte)color;
        _bytes[offset + 1] = (byte)(color >> 8);
    }

    private static int GetColorOffset(int paletteIndex, byte colorId) =>
        (((paletteIndex & 0x07) * 4) + (colorId & 0x03)) * 2;

    public void WriteDataRegister(byte value)
    {
        if (!isCpuRegisterEnabled)
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
