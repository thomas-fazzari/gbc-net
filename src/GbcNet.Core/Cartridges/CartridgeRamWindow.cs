using GbcNet.Core.Memory;

namespace GbcNet.Core.Cartridges;

/// <summary>
/// External cartridge RAM window shared by banked MBC implementations.
/// </summary>
internal sealed class CartridgeRamWindow(int sizeBytes, bool hasBattery)
{
    private const int RamBankSize = AddressMap.ExternalRamWindowSize;
    private const byte RamEnableValue = 0x0A;
    private const byte RamEnableMask = 0x0F;

    private bool _enabled;

    public CartridgeRam Ram { get; } = new(sizeBytes, hasBattery);

    public void WriteEnableRegister(byte value)
    {
        _enabled = (value & RamEnableMask) == RamEnableValue;
    }

    public byte ReadOffset(ushort offset, int bank) =>
        !CanAccess ? (byte)0xFF : Ram.Read(GetEffectiveOffset(offset, bank));

    public void WriteOffset(ushort offset, byte value, int bank)
    {
        if (CanAccess)
        {
            Ram.Write(GetEffectiveOffset(offset, bank), value);
        }
    }

    private bool CanAccess => _enabled && Ram.Size != 0;

    private int GetEffectiveOffset(ushort offset, int bank)
    {
        int effectiveOffset = (bank * RamBankSize) + offset;
        return effectiveOffset % Ram.Size;
    }
}
