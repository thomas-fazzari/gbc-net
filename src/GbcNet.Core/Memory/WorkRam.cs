namespace GbcNet.Core.Memory;

/// <summary>
/// Stores banked work RAM and mirrors E000-FDFF onto C000-DDFF.
/// </summary>
internal sealed class WorkRam
{
    private const int BankSize = 4 * 1024;
    private const int MinimumBankCount = 2;
    private const int BankSelectMask = 0b111;

    private readonly byte[] _banks;
    private readonly int _bankCount;
    private int _selectedSwitchableBank = 1;

    public WorkRam(int bankCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bankCount, MinimumBankCount);

        _bankCount = bankCount;
        _banks = new byte[bankCount * BankSize];
    }

    public byte Read(ushort address) => _banks[GetOffset(address)];

    public void Write(ushort address, byte value)
    {
        _banks[GetOffset(address)] = value;
    }

    internal void SelectSwitchableBank(byte value)
    {
        int selectedBank = value & BankSelectMask;

        if (selectedBank == 0 || selectedBank >= _bankCount)
        {
            selectedBank = 1;
        }

        _selectedSwitchableBank = selectedBank;
    }

    private int GetOffset(ushort address)
    {
        ushort mappedAddress =
            address >= AddressMap.EchoRamStart ? (ushort)(address - 0x2000) : address;

        return mappedAddress <= AddressMap.WorkRamFixedBankEnd
            ? mappedAddress - AddressMap.WorkRamStart
            : (_selectedSwitchableBank * BankSize)
                + (mappedAddress - AddressMap.WorkRamSwitchableBankStart);
    }
}
