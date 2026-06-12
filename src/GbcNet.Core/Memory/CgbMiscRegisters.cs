namespace GbcNet.Core.Memory;

/// <summary>
/// CGB-only undocumented I/O registers at FF72-FF75.
/// </summary>
internal sealed class CgbMiscRegisters(bool isEnabled)
{
    private const byte Ff75WritableMask = 0x70;
    private const byte Ff75ReadMask = 0x8F;

    private byte _ff72;
    private byte _ff73;
    private byte _ff75;

    public static bool ContainsRegister(ushort address) =>
        address
            is AddressMap.CgbUndocumentedRegisterFf72
                or AddressMap.CgbUndocumentedRegisterFf73
                or AddressMap.CgbUndocumentedRegisterFf74
                or AddressMap.CgbUndocumentedRegisterFf75;

    public byte ReadRegister(ushort address)
    {
        if (!isEnabled)
        {
            return 0xFF;
        }

        return address switch
        {
            AddressMap.CgbUndocumentedRegisterFf72 => _ff72,
            AddressMap.CgbUndocumentedRegisterFf73 => _ff73,
            AddressMap.CgbUndocumentedRegisterFf75 => (byte)(Ff75ReadMask | _ff75),
            _ => 0xFF,
        };
    }

    public void WriteRegister(ushort address, byte value)
    {
        if (!isEnabled)
        {
            return;
        }

        switch (address)
        {
            case AddressMap.CgbUndocumentedRegisterFf72:
                _ff72 = value;
                return;
            case AddressMap.CgbUndocumentedRegisterFf73:
                _ff73 = value;
                return;
            case AddressMap.CgbUndocumentedRegisterFf75:
                _ff75 = (byte)(value & Ff75WritableMask);
                return;
            default:
                return;
        }
    }
}
