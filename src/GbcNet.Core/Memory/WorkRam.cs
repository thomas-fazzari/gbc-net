namespace GbcNet.Core.Memory;

/// <summary>
/// Stores DMG work RAM and mirrors E000-FDFF onto C000-DDFF.
/// </summary>
internal sealed class WorkRam
{
    private const int Size = AddressMap.WorkRamEnd - AddressMap.WorkRamStart + 1;

    private readonly byte[] _bytes = new byte[Size];

    public byte Read(ushort address) => _bytes[GetOffset(address)];

    public void Write(ushort address, byte value)
    {
        _bytes[GetOffset(address)] = value;
    }

    private static int GetOffset(ushort address)
    {
        return address >= AddressMap.EchoRamStart
            ? address - AddressMap.EchoRamStart
            : address - AddressMap.WorkRamStart;
    }
}
