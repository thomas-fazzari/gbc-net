using GbcNet.Core.Cartridges;
using GbcNet.Core.Dma;
using GbcNet.Core.Interrupts;
using GbcNet.Core.Joypad;
using GbcNet.Core.Ppu;
using GbcNet.Core.Serial;
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
    /// Serial transfer registers routed through FF01-FF02.
    /// </summary>
    public SerialController Serial { get; }

    /// <summary>
    /// LCD/PPU registers routed through FF40-FF45 and FF47-FF4B.
    /// </summary>
    public PpuController Ppu { get; }

    /// <summary>
    /// OAM DMA register routed through FF46.
    /// </summary>
    public DmaController Dma { get; }

    /// <summary>
    /// Optional instrumentation sink for debugger/watchpoint tooling.
    /// </summary>
    internal ICpuMemoryWriteObserver? CpuMemoryWriteObserver { private get; set; }

    public MemoryBus(Cartridge cartridge)
    {
        _cartridge = cartridge;
        Interrupts = new InterruptController();
        Timers = new TimerController(Interrupts);
        Joypad = new JoypadController(Interrupts);
        Serial = new SerialController(Interrupts);
        Ppu = new PpuController(Interrupts);
        Dma = new DmaController();
        _readByteForDma = ReadOamDmaSourceByte;
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
        IsCpuMemoryBlocked(address) ? (byte)0xFF : ReadMappedByte(address);

    public void WriteByte(ushort address, byte value)
    {
        if (!IsCpuMemoryBlocked(address))
        {
            WriteMappedByte(address, value);
            CpuMemoryWriteObserver?.OnCpuMemoryWritten(address, value);
        }
    }

    /// <summary>
    /// Advances OAM DMA transfers using CPU machine cycles.
    /// </summary>
    public void TickDma(int machineCycles)
    {
        Dma.Tick(machineCycles, _readByteForDma, _writeOamByteForDma);
    }

    private bool IsCpuMemoryBlocked(ushort address) =>
        IsCpuMemoryBlockedByDma(address) || IsCpuVideoMemoryBlockedByPpu(address);

    private bool IsCpuMemoryBlockedByDma(ushort address) =>
        Dma.IsActive && address <= AddressMap.ObjectAttributeMemoryEnd;

    private bool IsCpuVideoMemoryBlockedByPpu(ushort address) =>
        address switch
        {
            >= AddressMap.VideoRamStart and <= AddressMap.VideoRamEnd => !Ppu.CanCpuAccessVideoRam,
            >= AddressMap.ObjectAttributeMemoryStart and <= AddressMap.ObjectAttributeMemoryEnd =>
                !Ppu.CanCpuAccessObjectAttributeMemory,
            _ => false,
        };

    private byte ReadMappedByte(ushort address)
    {
        return address switch
        {
            <= AddressMap.RomEnd => _cartridge.ReadRom(address),
            <= AddressMap.VideoRamEnd => _videoRam.Read(address),
            <= AddressMap.ExternalRamEnd => _cartridge.ReadRam(address),
            <= AddressMap.WorkRamEnd => _workRam.Read(address),
            <= AddressMap.EchoRamEnd => _workRam.Read(address),
            <= AddressMap.ObjectAttributeMemoryEnd => _objectAttributeMemory.Read(address),
            <= AddressMap.NotUsableEnd => 0x00,
            <= AddressMap.IoRegistersEnd => ReadIoRegister(address),
            <= AddressMap.HighRamEnd => _highRam.Read(address),
            AddressMap.InterruptEnableRegister => Interrupts.InterruptEnable,
        };
    }

    private byte ReadOamDmaSourceByte(ushort address) =>
        address switch
        {
            <= AddressMap.RomEnd => _cartridge.ReadRom(address),
            <= AddressMap.VideoRamEnd => _videoRam.Read(address),
            _ => _cartridge.ReadRamOffset(GetOamDmaExternalRamOffset(address)),
        };

    private static ushort GetOamDmaExternalRamOffset(ushort address) =>
        (ushort)(address & AddressMap.ExternalRamOffsetMask);

    private void WriteMappedByte(ushort address, byte value)
    {
        switch (address)
        {
            case <= AddressMap.RomEnd:
                _cartridge.WriteRom(address, value);
                return;
            case <= AddressMap.VideoRamEnd:
                _videoRam.Write(address, value);
                return;
            case <= AddressMap.ExternalRamEnd:
                _cartridge.WriteRam(address, value);
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
            AddressMap.SerialTransferDataRegister => Serial.TransferData,
            AddressMap.SerialTransferControlRegister => Serial.ReadControl(),
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
            case AddressMap.SerialTransferDataRegister:
                Serial.TransferData = value;
                return;
            case AddressMap.SerialTransferControlRegister:
                Serial.WriteControl(value);
                return;
            case AddressMap.DividerRegister:
                Timers.ResetDivider();
                return;
            case AddressMap.TimerCounterRegister:
                Timers.WriteTimerCounter(value);
                return;
            case AddressMap.TimerModuloRegister:
                Timers.WriteTimerModulo(value);
                return;
            case AddressMap.TimerControlRegister:
                Timers.WriteTimerControl(value);
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
            case AddressMap.SerialTransferDataRegister:
                Serial.TransferData = value;
                return;
            case AddressMap.SerialTransferControlRegister:
                Serial.SetControlState(value);
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
                Timers.SetTimerControlState(value);
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
