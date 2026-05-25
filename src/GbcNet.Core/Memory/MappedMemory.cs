namespace GbcNet.Core.Memory;

/// <summary>
/// Stores a plain contiguous address window with no hardware side effects or read/write masks.
/// </summary>
internal sealed class MappedMemory(ushort startAddress, ushort endAddress)
{
    private readonly byte[] _bytes = new byte[endAddress - startAddress + 1];

    public byte Read(ushort address) => _bytes[address - startAddress];

    public void Write(ushort address, byte value)
    {
        _bytes[address - startAddress] = value;
    }
}
