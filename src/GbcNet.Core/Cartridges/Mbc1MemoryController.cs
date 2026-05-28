using GbcNet.Core.Memory;

namespace GbcNet.Core.Cartridges;

/// <summary>
/// MBC1 cartridge controller for ROM banking and optional external RAM banking.
/// </summary>
internal sealed class Mbc1MemoryController(byte[] rom, CartridgeHeader header)
    : ICartridgeMemoryController
{
    private const int RomBankSize = Cartridge.FixedRomBankSize;
    private const int RamBankSize = 8 * 1024;

    private const ushort RomBank0End = 0x3FFF;
    private const ushort RomBankNStart = 0x4000;

    private const byte RamEnableValue = 0x0A;
    private const byte RamEnableMask = 0x0F;
    private const byte RomBankLowMask = 0x1F;
    private const byte BankHighMask = 0x03;
    private const byte BankingModeMask = 0x01;

    private readonly byte[] _ram = new byte[header.RamSizeBytes];

    private byte _romBankLow;
    private byte _bankHigh;
    private byte _bankingMode;
    private bool _ramEnabled;

    public byte ReadRom(ushort address)
    {
        int bank = address <= RomBank0End ? GetFixedAreaRomBank() : GetSwitchableRomBank();
        int bankAddress = address <= RomBank0End ? address : address - RomBankNStart;
        return rom[(bank * RomBankSize) + bankAddress];
    }

    public void WriteRom(ushort address, byte value)
    {
        switch (address)
        {
            case <= 0x1FFF:
                _ramEnabled = (value & RamEnableMask) == RamEnableValue;
                return;
            case <= 0x3FFF:
                _romBankLow = (byte)(value & RomBankLowMask);
                return;
            case <= 0x5FFF:
                _bankHigh = (byte)(value & BankHighMask);
                return;
            case <= 0x7FFF:
                _bankingMode = (byte)(value & BankingModeMask);
                return;
        }
    }

    public byte ReadRam(ushort address) =>
        !CanAccessRam() ? (byte)0xFF : _ram[GetRamOffset(address)];

    public void WriteRam(ushort address, byte value)
    {
        if (CanAccessRam())
        {
            _ram[GetRamOffset(address)] = value;
        }
    }

    private int GetFixedAreaRomBank() => _bankingMode == 0 ? 0 : WrapRomBank(_bankHigh << 5);

    private int GetSwitchableRomBank()
    {
        int bank = (_bankHigh << 5) | _romBankLow;
        if ((bank & RomBankLowMask) == 0)
        {
            bank++;
        }

        return WrapRomBank(bank);
    }

    private int WrapRomBank(int bank) => bank % header.RomBankCount;

    private bool CanAccessRam() => _ramEnabled && _ram.Length != 0;

    private int GetRamOffset(ushort address)
    {
        int bank = _bankingMode == 0 ? 0 : _bankHigh;
        int offset = (bank * RamBankSize) + (address - AddressMap.ExternalRamStart);
        return offset % _ram.Length;
    }
}
