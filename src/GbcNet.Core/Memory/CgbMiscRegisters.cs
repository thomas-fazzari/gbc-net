namespace GbcNet.Core.Memory;

/// <summary>
/// CGB-only undocumented I/O registers at FF72-FF75.
/// </summary>
internal sealed class CgbMiscRegisters(bool isCgbHardwareRegisterEnabled, bool isFf74Enabled)
{
    private const byte Ff75WritableMask = 0x70;
    private const byte Ff75ReadMask = 0x8F;

    private byte _ff72;
    private byte _ff73;
    private byte _ff74;
    private byte _ff75;

    public static bool ContainsRegister(ushort address) =>
        address
            is AddressMap.CgbUndocumentedRegisterFf72
                or AddressMap.CgbUndocumentedRegisterFf73
                or AddressMap.CgbUndocumentedRegisterFf74
                or AddressMap.CgbUndocumentedRegisterFf75;

    public byte ReadRegister(ushort address) =>
        address switch
        {
            AddressMap.CgbUndocumentedRegisterFf72 => isCgbHardwareRegisterEnabled
                ? _ff72
                : (byte)0xFF,
            AddressMap.CgbUndocumentedRegisterFf73 => isCgbHardwareRegisterEnabled
                ? _ff73
                : (byte)0xFF,
            AddressMap.CgbUndocumentedRegisterFf74 => isFf74Enabled ? _ff74 : (byte)0xFF,
            AddressMap.CgbUndocumentedRegisterFf75 => isCgbHardwareRegisterEnabled
                ? (byte)(Ff75ReadMask | _ff75)
                : (byte)0xFF,
            _ => 0xFF,
        };

    public void WriteRegister(ushort address, byte value)
    {
        switch (address)
        {
            case AddressMap.CgbUndocumentedRegisterFf72:
                if (isCgbHardwareRegisterEnabled)
                {
                    _ff72 = value;
                }
                return;
            case AddressMap.CgbUndocumentedRegisterFf73:
                if (isCgbHardwareRegisterEnabled)
                {
                    _ff73 = value;
                }
                return;
            case AddressMap.CgbUndocumentedRegisterFf74:
                if (isFf74Enabled)
                {
                    _ff74 = value;
                }
                return;
            case AddressMap.CgbUndocumentedRegisterFf75:
                if (isCgbHardwareRegisterEnabled)
                {
                    _ff75 = (byte)(value & Ff75WritableMask);
                }
                return;
            default:
                return;
        }
    }
}
