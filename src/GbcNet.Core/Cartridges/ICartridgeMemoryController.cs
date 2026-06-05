namespace GbcNet.Core.Cartridges;

/// <summary>
/// Handles cartridge-local ROM, external RAM, and MBC control register accesses.
/// </summary>
internal interface ICartridgeMemoryController
{
    byte ReadRom(ushort address);

    void WriteRom(ushort address, byte value);

    byte ReadRamOffset(ushort offset);

    void WriteRamOffset(ushort offset, byte value);

    CartridgeRam ExternalRam { get; }
}
