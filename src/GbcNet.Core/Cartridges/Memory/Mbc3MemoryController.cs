namespace GbcNet.Core.Cartridges.Memory;

/// <summary>
/// MBC3 cartridge controller for ROM banking, optional external RAM banking, and ignored RTC registers.
/// </summary>
internal sealed class Mbc3MemoryController(
    byte[] rom,
    CartridgeHeader header,
    bool hasBatteryBackedRam
) : ICartridgeMemoryController
{
    private const int RomBankSize = Cartridge.FixedRomBankSize;
    private const ushort RomBank0End = 0x3FFF;
    private const ushort RomBankNStart = 0x4000;

    private const byte RomBankMask = 0x7F;
    private const byte LastRamBank = 0x07;

    private byte _romBank = 1;
    private byte _ramBankOrRtcRegister;
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

        int bank = (_romBank == 0 ? 1 : _romBank) % header.RomBankCount;
        return rom[(bank * RomBankSize) + (address - RomBankNStart)];
    }

    public void WriteRom(ushort address, byte value)
    {
        switch (address)
        {
            case <= 0x1FFF:
                _externalRam.WriteEnableRegister(value);
                return;
            case <= 0x3FFF:
                _romBank = (byte)(value & RomBankMask);
                return;
            case <= 0x5FFF:
                _ramBankOrRtcRegister = value;
                return;
        }
    }

    public byte ReadRamOffset(ushort offset) =>
        IsRamBankSelected ? _externalRam.ReadOffset(offset, _ramBankOrRtcRegister) : (byte)0xFF;

    public void WriteRamOffset(ushort offset, byte value)
    {
        if (IsRamBankSelected)
        {
            _externalRam.WriteOffset(offset, value, _ramBankOrRtcRegister);
        }
    }

    private bool IsRamBankSelected => _ramBankOrRtcRegister <= LastRamBank;
}
