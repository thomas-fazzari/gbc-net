using GbcNet.Core.Cartridges;

namespace GbcNet.Core.Memory;

/// <summary>
/// Routes CPU-visible 16-bit addresses to the currently modeled DMG memory regions.
/// </summary>
internal sealed class MemoryBus(Cartridge cartridge)
{
    /// <summary>
    /// Plain backing store for HRAM at FF80-FFFE.
    /// </summary>
    private readonly MappedMemory _highRam = new(AddressMap.HighRamStart, AddressMap.HighRamEnd);

    /// <summary>
    /// Temporary backing store for I/O registers until each register gets its hardware behavior.
    /// </summary>
    private readonly MappedMemory _ioRegisters = new(
        AddressMap.IoRegistersStart,
        AddressMap.IoRegistersEnd
    );

    /// <summary>
    /// Plain backing store for OAM at FE00-FE9F.
    /// </summary>
    private readonly MappedMemory _objectAttributeMemory = new(
        AddressMap.ObjectAttributeMemoryStart,
        AddressMap.ObjectAttributeMemoryEnd
    );

    /// <summary>
    /// Plain backing store for DMG VRAM bank 0 at 8000-9FFF.
    /// </summary>
    private readonly MappedMemory _videoRam = new(AddressMap.VideoRamStart, AddressMap.VideoRamEnd);

    private readonly WorkRam _workRam = new();

    private byte _interruptEnable;

    public byte ReadByte(ushort address)
    {
        return address switch
        {
            >= AddressMap.RomStart and <= AddressMap.RomEnd => cartridge.ReadRom(address),
            >= AddressMap.VideoRamStart and <= AddressMap.VideoRamEnd => _videoRam.Read(address),
            >= AddressMap.ExternalRamStart and <= AddressMap.ExternalRamEnd => 0xFF,
            >= AddressMap.WorkRamStart and <= AddressMap.WorkRamEnd => _workRam.Read(address),
            >= AddressMap.EchoRamStart and <= AddressMap.EchoRamEnd => _workRam.Read(address),
            >= AddressMap.ObjectAttributeMemoryStart and <= AddressMap.ObjectAttributeMemoryEnd =>
                _objectAttributeMemory.Read(address),
            >= AddressMap.NotUsableStart and <= AddressMap.NotUsableEnd => 0x00,
            >= AddressMap.IoRegistersStart and <= AddressMap.IoRegistersEnd => _ioRegisters.Read(
                address
            ),
            >= AddressMap.HighRamStart and <= AddressMap.HighRamEnd => _highRam.Read(address),
            AddressMap.InterruptEnableRegister => _interruptEnable,
        };
    }

    public void WriteByte(ushort address, byte value)
    {
        switch (address)
        {
            case >= AddressMap.RomStart and <= AddressMap.RomEnd:
            case >= AddressMap.ExternalRamStart and <= AddressMap.ExternalRamEnd:
            case >= AddressMap.NotUsableStart and <= AddressMap.NotUsableEnd:
                return;
            case >= AddressMap.VideoRamStart and <= AddressMap.VideoRamEnd:
                _videoRam.Write(address, value);
                return;
            case >= AddressMap.WorkRamStart and <= AddressMap.WorkRamEnd:
            case >= AddressMap.EchoRamStart and <= AddressMap.EchoRamEnd:
                _workRam.Write(address, value);
                return;
            case >= AddressMap.ObjectAttributeMemoryStart
            and <= AddressMap.ObjectAttributeMemoryEnd:
                _objectAttributeMemory.Write(address, value);
                return;
            case >= AddressMap.IoRegistersStart and <= AddressMap.IoRegistersEnd:
                _ioRegisters.Write(address, value);
                return;
            case >= AddressMap.HighRamStart and <= AddressMap.HighRamEnd:
                _highRam.Write(address, value);
                return;
            case AddressMap.InterruptEnableRegister:
                _interruptEnable = value;
                return;
        }
    }
}
