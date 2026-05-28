using GbcNet.Core.Cartridges;
using GbcNet.Core.Interrupts;
using GbcNet.Core.Joypad;
using GbcNet.Core.Timers;

namespace GbcNet.Core.Memory;

/// <summary>
/// Routes CPU-visible 16-bit addresses to the currently modeled DMG memory regions.
/// </summary>
internal sealed class MemoryBus
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
    private readonly Cartridge _cartridge;

    /// <summary>
    /// Interrupt request and enable registers routed through FF0F and FFFF.
    /// </summary>
    public InterruptController Interrupts { get; }

    /// <summary>
    /// Divider and timer registers routed through FF04-FF07.
    /// </summary>
    public TimerController Timers { get; }

    /// <summary>
    /// Joypad matrix register routed through FF00.
    /// </summary>
    public JoypadController Joypad { get; }

    public MemoryBus(Cartridge cartridge)
    {
        _cartridge = cartridge;
        Interrupts = new InterruptController();
        Timers = new TimerController(Interrupts);
        Joypad = new JoypadController(Interrupts);
    }

    /// <summary>
    /// Seeds a CPU-visible hardware register without triggering CPU write side effects.
    /// </summary>
    internal void SetHardwareRegisterState(ushort address, byte value)
    {
        switch (address)
        {
            case AddressMap.JoypadRegister:
                Joypad.Write(value, requestInterruptOnTransition: false);
                return;
            case AddressMap.DividerRegister:
                Timers.SetDivider(value);
                return;
            case AddressMap.TimerCounterRegister:
                Timers.TimerCounter = value;
                return;
            case AddressMap.TimerModuloRegister:
                Timers.TimerModulo = value;
                return;
            case AddressMap.TimerControlRegister:
                Timers.SetTimerControl(value);
                return;
            case AddressMap.InterruptFlagRegister:
                Interrupts.SetInterruptFlag(value);
                return;
            case AddressMap.InterruptEnableRegister:
                Interrupts.InterruptEnable = value;
                return;
            case >= AddressMap.IoRegistersStart and <= AddressMap.IoRegistersEnd:
                _ioRegisters.Write(address, value);
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(address),
                    address,
                    "Address must target a CPU-visible hardware register."
                );
        }
    }

    public byte ReadByte(ushort address)
    {
        return address switch
        {
            <= AddressMap.RomEnd => _cartridge.ReadRom(address),
            <= AddressMap.VideoRamEnd => _videoRam.Read(address),
            <= AddressMap.ExternalRamEnd => 0xFF,
            <= AddressMap.WorkRamEnd => _workRam.Read(address),
            <= AddressMap.EchoRamEnd => _workRam.Read(address),
            <= AddressMap.ObjectAttributeMemoryEnd => _objectAttributeMemory.Read(address),
            <= AddressMap.NotUsableEnd => 0x00,
            AddressMap.JoypadRegister => Joypad.Read(),
            AddressMap.DividerRegister => Timers.ReadDivider(),
            AddressMap.TimerCounterRegister => Timers.TimerCounter,
            AddressMap.TimerModuloRegister => Timers.TimerModulo,
            AddressMap.TimerControlRegister => Timers.ReadTimerControl(),
            AddressMap.InterruptFlagRegister => Interrupts.ReadInterruptFlag(),
            <= AddressMap.IoRegistersEnd => _ioRegisters.Read(address),
            <= AddressMap.HighRamEnd => _highRam.Read(address),
            AddressMap.InterruptEnableRegister => Interrupts.InterruptEnable,
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
            case AddressMap.JoypadRegister:
                Joypad.Write(value, requestInterruptOnTransition: true);
                return;
            case AddressMap.DividerRegister:
                Timers.ResetDivider();
                return;
            case AddressMap.TimerCounterRegister:
                Timers.TimerCounter = value;
                return;
            case AddressMap.TimerModuloRegister:
                Timers.TimerModulo = value;
                return;
            case AddressMap.TimerControlRegister:
                Timers.SetTimerControl(value);
                return;
            case AddressMap.InterruptFlagRegister:
                Interrupts.SetInterruptFlag(value);
                return;
            case <= AddressMap.IoRegistersEnd:
                _ioRegisters.Write(address, value);
                return;
            case <= AddressMap.HighRamEnd:
                _highRam.Write(address, value);
                return;
            case AddressMap.InterruptEnableRegister:
                Interrupts.InterruptEnable = value;
                return;
        }
    }
}
