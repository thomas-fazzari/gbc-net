namespace GbcNet.Core.Cartridges;

/// <summary>
/// MBC1 cartridge controller for ROM banking and optional external RAM banking.
/// </summary>
internal sealed class Mbc1MemoryController(
    byte[] rom,
    CartridgeHeader header,
    bool hasBatteryBackedRam
) : ICartridgeMemoryController
{
    private const int RomBankSize = Cartridge.FixedRomBankSize;
    private const ushort RomBank0End = 0x3FFF;
    private const ushort RomBankNStart = 0x4000;

    private const byte RomBankLowMask = 0x1F;
    private const byte BankHighMask = 0x03;
    private const byte BankingModeMask = 0x01;

    private byte _romBankLow;
    private byte _bankHigh;
    private byte _bankingMode;
    private readonly CartridgeRamWindow _externalRam = new(
        header.RamSizeBytes,
        hasBatteryBackedRam
    );

    public CartridgeRam ExternalRam => _externalRam.Ram;

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
                _externalRam.WriteEnableRegister(value);
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

    public byte ReadRamOffset(ushort offset) => _externalRam.ReadOffset(offset, GetRamBank());

    public void WriteRamOffset(ushort offset, byte value)
    {
        _externalRam.WriteOffset(offset, value, GetRamBank());
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

    private int GetRamBank() => _bankingMode == 0 ? 0 : _bankHigh;
}
