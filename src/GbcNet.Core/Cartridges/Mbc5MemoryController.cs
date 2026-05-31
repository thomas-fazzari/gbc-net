using GbcNet.Core.Memory;

namespace GbcNet.Core.Cartridges;

/// <summary>
/// MBC5 cartridge controller for 9-bit ROM banking and optional external RAM banking.
/// </summary>
internal sealed class Mbc5MemoryController(byte[] rom, CartridgeHeader header)
    : ICartridgeMemoryController
{
    private const int RomBankSize = Cartridge.FixedRomBankSize;
    private const int RamBankSize = AddressMap.ExternalRamWindowSize;

    private const ushort RomBank0End = 0x3FFF;
    private const ushort RomBankNStart = 0x4000;

    private const byte RamEnableValue = 0x0A;
    private const byte RamEnableMask = 0x0F;
    private const byte RomBankHighMask = 0x01;
    private const byte RamBankMask = 0x0F;

    private readonly byte[] _ram = new byte[header.RamSizeBytes];

    private byte _romBankLow = 1;
    private byte _romBankHigh;
    private byte _ramBank;
    private bool _ramEnabled;

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
                _ramEnabled = (value & RamEnableMask) == RamEnableValue;
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

    public byte ReadRamOffset(ushort offset) =>
        !CanAccessRam() ? (byte)0xFF : _ram[GetEffectiveRamOffset(offset)];

    public void WriteRamOffset(ushort offset, byte value)
    {
        if (CanAccessRam())
        {
            _ram[GetEffectiveRamOffset(offset)] = value;
        }
    }

    private bool CanAccessRam() => _ramEnabled && _ram.Length != 0;

    private int GetEffectiveRamOffset(ushort offset)
    {
        int effectiveOffset = (_ramBank * RamBankSize) + offset;
        return effectiveOffset % _ram.Length;
    }
}
