using GbcNet.Core.Memory;

namespace GbcNet.Core.Ppu;

/// <summary>
/// Stores CPU-visible VRAM with optional CGB bank selection through VBK.
/// </summary>
internal sealed class VideoRam
{
    private const int BankSize = AddressMap.VideoRamEnd - AddressMap.VideoRamStart + 1;
    private const int MinimumBankCount = 1;
    private const int MaximumBankCount = 2;
    private const int BankSelectMask = 0b1;
    private const byte BankRegisterReadMask = 0xFE;

    private readonly byte[] _banks;
    private readonly int _bankCount;

    public VideoRam(int bankCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bankCount, MinimumBankCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bankCount, MaximumBankCount);

        _bankCount = bankCount;
        _banks = new byte[bankCount * BankSize];
    }

    public int SelectedBank { get; private set; }

    public byte Read(ushort address) => _banks[GetOffset(SelectedBank, address)];

    public void Write(ushort address, byte value)
    {
        _banks[GetOffset(SelectedBank, address)] = value;
    }

    public byte ReadBank(int bank, ushort address) => _banks[GetOffset(bank, address)];

    public byte ReadBankRegister() =>
        _bankCount > MinimumBankCount ? (byte)(BankRegisterReadMask | SelectedBank) : (byte)0xFF;

    public void WriteBankRegister(byte value)
    {
        if (_bankCount > MinimumBankCount)
        {
            SelectedBank = value & BankSelectMask;
        }
    }

    private static int GetOffset(int bank, ushort address) =>
        (bank * BankSize) + address - AddressMap.VideoRamStart;
}
