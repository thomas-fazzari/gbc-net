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
            <= AddressMap.RomEnd => cartridge.ReadRom(address),
            <= AddressMap.VideoRamEnd => _videoRam.Read(address),
            <= AddressMap.ExternalRamEnd => 0xFF,
            <= AddressMap.WorkRamEnd => _workRam.Read(address),
            <= AddressMap.EchoRamEnd => _workRam.Read(address),
            <= AddressMap.ObjectAttributeMemoryEnd => _objectAttributeMemory.Read(address),
            <= AddressMap.NotUsableEnd => 0x00,
            <= AddressMap.IoRegistersEnd => _ioRegisters.Read(address),
            <= AddressMap.HighRamEnd => _highRam.Read(address),
            AddressMap.InterruptEnableRegister => _interruptEnable,
        };
    }

    public void WriteByte(ushort address, byte value)
    {
        switch (address)
        {
            case <= AddressMap.RomEnd:
                return;
            case <= AddressMap.VideoRamEnd:
                _videoRam.Write(address, value);
                return;
            case <= AddressMap.ExternalRamEnd:
                return;
            case <= AddressMap.WorkRamEnd:
            case <= AddressMap.EchoRamEnd:
                _workRam.Write(address, value);
                return;
            case <= AddressMap.ObjectAttributeMemoryEnd:
                _objectAttributeMemory.Write(address, value);
                return;
            case <= AddressMap.NotUsableEnd:
                return;
            case <= AddressMap.IoRegistersEnd:
                _ioRegisters.Write(address, value);
                return;
            case <= AddressMap.HighRamEnd:
                _highRam.Write(address, value);
                return;
            case AddressMap.InterruptEnableRegister:
                _interruptEnable = value;
                return;
        }
    }
}
