namespace GbcNet.Core.Cartridges.Memory;

/// <summary>
/// MBC2 cartridge controller with 4-bit built-in RAM and 4-bit ROM banking.
/// </summary>
internal sealed class Mbc2MemoryController(
    byte[] rom,
    CartridgeHeader header,
    bool hasBatteryBackedRam
) : ICartridgeMemoryController
{
    private const int RomBankSize = Cartridge.FixedRomBankSize;
    private const ushort RomBank0End = 0x3FFF;
    private const ushort RomBankNStart = 0x4000;
    private const ushort RegisterAddressBit8 = 0x0100;
    private const byte RomBankMask = 0x0F;
    private const ushort RamOffsetMask = 0x01FF;

    private readonly Mbc2Ram _ram = new(hasBatteryBackedRam);
    private byte _romBank = 1;
    private bool _ramEnabled;

    public ICartridgeRamStorage CartridgeRam => _ram;

    public byte ReadRom(ushort address)
    {
        if (address <= RomBank0End)
        {
            return rom[address];
        }

        int bank = _romBank % header.RomBankCount;
        return rom[(bank * RomBankSize) + (address - RomBankNStart)];
    }

    public void WriteRom(ushort address, byte value)
    {
        if (address > RomBank0End)
        {
            return;
        }

        if ((address & RegisterAddressBit8) == 0)
        {
            _ramEnabled = (value & RomBankMask) == 0x0A;
            return;
        }

        _romBank = (byte)(value & RomBankMask);
        if (_romBank == 0)
        {
            _romBank = 1;
        }
    }

    public byte ReadRamOffset(ushort offset) =>
        _ramEnabled ? _ram.Read(offset & RamOffsetMask) : (byte)0xFF;

    public void WriteRamOffset(ushort offset, byte value)
    {
        if (_ramEnabled)
        {
            _ram.Write(offset & RamOffsetMask, value);
        }
    }
}
