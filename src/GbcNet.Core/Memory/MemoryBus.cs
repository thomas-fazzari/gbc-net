using GbcNet.Core.Cartridges;
using GbcNet.Core.Dma;
using GbcNet.Core.Interrupts;
using GbcNet.Core.Joypad;
using GbcNet.Core.Ppu;
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
    private readonly Func<ushort, byte> _readByteForDma;
    private readonly Action<ushort, byte> _writeOamByteForDma;

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

    /// <summary>
    /// LCD/PPU registers routed through FF40-FF45 and FF47-FF4B.
    /// </summary>
    public PpuController Ppu { get; }

    /// <summary>
    /// OAM DMA register routed through FF46.
    /// </summary>
    public DmaController Dma { get; }

    public MemoryBus(Cartridge cartridge)
    {
        _cartridge = cartridge;
        Interrupts = new InterruptController();
        Timers = new TimerController(Interrupts);
        Joypad = new JoypadController(Interrupts);
        Ppu = new PpuController();
        Dma = new DmaController();
        _readByteForDma = ReadByteBypassingDma;
        _writeOamByteForDma = WriteOamByteForDma;
    }

    /// <summary>
    /// Seeds a CPU-visible hardware register without triggering CPU write side effects.
    /// </summary>
    internal void SetHardwareRegisterState(ushort address, byte value)
    {
        switch (address)
        {
            case >= AddressMap.IoRegistersStart and <= AddressMap.IoRegistersEnd:
                SetIoRegisterState(address, value);
                return;
            case AddressMap.InterruptEnableRegister:
                Interrupts.InterruptEnable = value;
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(address),
                    address,
                    "Address must target a CPU-visible hardware register."
                );
        }
    }

    public byte ReadByte(ushort address) =>
        IsCpuMemoryBlockedByDma(address) ? (byte)0xFF : ReadByteBypassingDma(address);

    public void WriteByte(ushort address, byte value)
    {
        if (!IsCpuMemoryBlockedByDma(address))
        {
            WriteByteBypassingDma(address, value);
        }
    }

    /// <summary>
    /// Advances OAM DMA transfers using CPU machine cycles.
    /// </summary>
    public void TickDma(int machineCycles)
    {
        Dma.Tick(machineCycles, _readByteForDma, _writeOamByteForDma);
    }

    private bool IsCpuMemoryBlockedByDma(ushort address) =>
        Dma.IsActive && address <= AddressMap.ObjectAttributeMemoryEnd;

    private byte ReadByteBypassingDma(ushort address)
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
            <= AddressMap.IoRegistersEnd => ReadIoRegister(address),
            <= AddressMap.HighRamEnd => _highRam.Read(address),
            AddressMap.InterruptEnableRegister => Interrupts.InterruptEnable,
        };
    }

    private void WriteByteBypassingDma(ushort address, byte value)
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
                WriteIoRegister(address, value);
                return;
            case <= AddressMap.HighRamEnd:
                _highRam.Write(address, value);
                return;
            case AddressMap.InterruptEnableRegister:
                Interrupts.InterruptEnable = value;
                return;
        }
    }

    private byte ReadIoRegister(ushort address)
    {
        if (PpuController.ContainsRegister(address))
        {
            return Ppu.ReadRegister(address);
        }

        return address switch
        {
            AddressMap.JoypadRegister => Joypad.Read(),
            AddressMap.DividerRegister => Timers.ReadDivider(),
            AddressMap.TimerCounterRegister => Timers.TimerCounter,
            AddressMap.TimerModuloRegister => Timers.TimerModulo,
            AddressMap.TimerControlRegister => Timers.ReadTimerControl(),
            AddressMap.InterruptFlagRegister => Interrupts.ReadInterruptFlag(),
            AddressMap.DmaRegister => Dma.ReadRegister(),
            _ => _ioRegisters.Read(address),
        };
    }

    private void WriteIoRegister(ushort address, byte value)
    {
        if (PpuController.ContainsRegister(address))
        {
            Ppu.WriteRegister(address, value);
            return;
        }

        switch (address)
        {
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
            case AddressMap.DmaRegister:
                Dma.StartOamTransfer(value);
                return;
            default:
                _ioRegisters.Write(address, value);
                return;
        }
    }

    private void SetIoRegisterState(ushort address, byte value)
    {
        if (PpuController.ContainsRegister(address))
        {
            Ppu.SetRegisterState(address, value);
            return;
        }

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
            case AddressMap.DmaRegister:
                Dma.SetRegisterState(value);
                return;
            default:
                _ioRegisters.Write(address, value);
                return;
        }
    }

    private void WriteOamByteForDma(ushort address, byte value)
    {
        _objectAttributeMemory.Write(address, value);
    }
}
