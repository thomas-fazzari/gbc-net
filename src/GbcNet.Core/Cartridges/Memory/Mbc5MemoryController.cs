namespace GbcNet.Core.Cartridges.Memory;

/// <summary>
/// MBC5 cartridge controller for 9-bit ROM banking and optional external RAM banking.
/// </summary>
internal sealed class Mbc5MemoryController(
    byte[] rom,
    CartridgeHeader header,
    bool hasBatteryBackedRam
) : ICartridgeMemoryController
{
    private const int RomBankSize = Cartridge.FixedRomBankSize;
    private const ushort RomBank0End = 0x3FFF;
    private const ushort RomBankNStart = 0x4000;

    private const byte RomBankHighMask = 0x01;
    private const byte RamBankMask = 0x0F;

    private byte _romBankLow = 1;
    private byte _romBankHigh;
    private byte _ramBank;
    private readonly CartridgeRamWindow _externalRam = new(
        header.RamSizeBytes,
        hasBatteryBackedRam
    );

    public CartridgeRam CartridgeRam => _externalRam.Ram;

    public byte ReadRom(ushort address)
    {
        if (address <= RomBank0End)
        {
            return rom[address];
        }

        int bank = ((_romBankHigh << 8) | _romBankLow) % header.RomBankCount;
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
                return;
        }
    }

    public byte ReadRamOffset(ushort offset) => _externalRam.ReadOffset(offset, _ramBank);

    public void WriteRamOffset(ushort offset, byte value)
    {
        _externalRam.WriteOffset(offset, value, _ramBank);
    }
}
