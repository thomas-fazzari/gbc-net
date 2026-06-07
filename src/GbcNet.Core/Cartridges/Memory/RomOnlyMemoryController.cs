namespace GbcNet.Core.Cartridges.Memory;

/// <summary>
/// Plain 32 KiB ROM cartridge with no MBC registers or external RAM.
/// </summary>
internal sealed class RomOnlyMemoryController(byte[] rom) : ICartridgeMemoryController
{
    public byte ReadRom(ushort address) => rom[address];

    public void WriteRom(ushort address, byte value) { }

    public byte ReadRamOffset(ushort offset) => 0xFF;

    public void WriteRamOffset(ushort offset, byte value) { }

    public CartridgeRam CartridgeRam { get; } = new(sizeBytes: 0, hasBattery: false);
}
