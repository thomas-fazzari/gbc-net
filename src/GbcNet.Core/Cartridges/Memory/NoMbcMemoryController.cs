namespace GbcNet.Core.Cartridges.Memory;

/// <summary>
/// No-MBC cartridge controller with direct ROM mapping and optional fixed cartridge RAM.
/// </summary>
internal sealed class NoMbcMemoryController(
    byte[] rom,
    CartridgeHeader header,
    bool hasBatteryBackedRam
) : ICartridgeMemoryController
{
    private readonly CartridgeRam _cartridgeRam = new(header.RamSizeBytes, hasBatteryBackedRam);

    public ICartridgeSaveData SaveData => _cartridgeRam;

    public byte ReadRom(ushort address) => rom[address];

    public void WriteRom(ushort address, byte value) { }

    public byte ReadRamOffset(ushort offset) =>
        _cartridgeRam.Size == 0 ? (byte)0xFF : _cartridgeRam.Read(offset % _cartridgeRam.Size);

    public void WriteRamOffset(ushort offset, byte value)
    {
        if (_cartridgeRam.Size != 0)
        {
            _cartridgeRam.Write(offset % _cartridgeRam.Size, value);
        }
    }
}
