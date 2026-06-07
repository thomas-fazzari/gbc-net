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
    public CartridgeRam CartridgeRam { get; } = new(header.RamSizeBytes, hasBatteryBackedRam);

    public byte ReadRom(ushort address) => rom[address];

    public void WriteRom(ushort address, byte value) { }

    public byte ReadRamOffset(ushort offset) =>
        CartridgeRam.Size == 0 ? (byte)0xFF : CartridgeRam.Read(offset % CartridgeRam.Size);

    public void WriteRamOffset(ushort offset, byte value)
    {
        if (CartridgeRam.Size != 0)
        {
            CartridgeRam.Write(offset % CartridgeRam.Size, value);
        }
    }
}
