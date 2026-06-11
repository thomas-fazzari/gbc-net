namespace GbcNet.Core.Ppu;

/// <summary>
/// Stores one CGB color palette RAM and its index/data register pair.
/// </summary>
internal sealed class CgbPaletteRam(bool isEnabled)
{
    private const int PaletteRamSize = 64;
    private const byte IndexMask = 0x3F;
    private const byte IndexReadMask = 0x40;
    private const byte AutoIncrementMask = 0x80;
    private const byte DisabledRegisterValue = 0xFF;

    private readonly byte[] _bytes = new byte[PaletteRamSize];
    private byte _index;

    public byte ReadIndexRegister() =>
        isEnabled ? (byte)(IndexReadMask | _index) : DisabledRegisterValue;

    public void WriteIndexRegister(byte value)
    {
        if (isEnabled)
        {
            _index = (byte)(value & (AutoIncrementMask | IndexMask));
        }
    }

    public byte ReadDataRegister() =>
        isEnabled ? _bytes[_index & IndexMask] : DisabledRegisterValue;

    public void WriteDataRegister(byte value)
    {
        if (!isEnabled)
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
