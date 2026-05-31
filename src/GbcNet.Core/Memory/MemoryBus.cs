using GbcNet.Core.Apu;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Dma;
using GbcNet.Core.Hardware;
using GbcNet.Core.Interrupts;
using GbcNet.Core.Joypad;
using GbcNet.Core.Ppu;
using GbcNet.Core.Serial;
using GbcNet.Core.Timers;

namespace GbcNet.Core.Memory;

/// <summary>
/// Routes CPU-visible 16-bit addresses to memory regions and hardware registers.
/// </summary>
internal sealed class MemoryBus
{
    /// <summary>
    /// Plain backing store for HRAM at FF80-FFFE.
    /// </summary>
    private readonly MappedMemory _highRam = new(AddressMap.HighRamStart, AddressMap.HighRamEnd);

    private readonly WorkRam _workRam = new();
    private readonly Cartridge _cartridge;
    private readonly IDmaTransferStrategy _dmaTransferStrategy;
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
    /// Audio registers routed through FF10-FF26.
    /// </summary>
    public ApuController Apu { get; }

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

    public MemoryBus(Cartridge cartridge, IHardwareStrategy hardwareStrategy)
    {
        _cartridge = cartridge;
        _dmaTransferStrategy = hardwareStrategy.CreateDmaTransferStrategy();
        Interrupts = new InterruptController();
        Timers = new TimerController(Interrupts);
        Joypad = new JoypadController(Interrupts);
        Serial = new SerialController(Interrupts);
        Apu = new ApuController();
        Ppu = new PpuController(Interrupts, hardwareStrategy.CreatePpuTimingStrategy());
        Dma = new DmaController();
        _readByteForDma = ReadOamDmaSourceByte;
        _writeOamByteForDma = Ppu.ObjectAttributeMemory.Write;
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

    public byte ReadByte(ushort address)
    {
        if (TryReadDmaConflictedByte(address, out byte value))
        {
            return value;
        }

        return IsCpuVideoMemoryReadBlockedByPpu(address) ? (byte)0xFF : ReadMappedByte(address);
    }

    public void WriteByte(ushort address, byte value)
    {
        if (IsCpuWriteBlocked(address))
        {
            return;
        }

        WriteMappedByte(address, value);
        CpuMemoryWriteObserver?.OnCpuMemoryWritten(address, value);
    }

    /// <summary>
    /// Advances OAM DMA transfers using CPU machine cycles.
    /// </summary>
    public void TickDma(int machineCycles)
    {
        Dma.Tick(machineCycles, _readByteForDma, _writeOamByteForDma);
    }

    private bool IsCpuWriteBlocked(ushort address) =>
        IsCpuWriteBlockedByDma(address) || IsCpuVideoMemoryWriteBlockedByPpu(address);

    private bool IsCpuWriteBlockedByDma(ushort address) =>
        (IsObjectAttributeMemory(address) && Dma.IsCpuOamBlocked)
        || (
            Dma.TryGetCpuConflictSourceAddress(out ushort sourceAddress)
            && _dmaTransferStrategy.IsCpuAddressBlocked(address, sourceAddress)
        );

    private bool TryReadDmaConflictedByte(ushort address, out byte value)
    {
        if (IsObjectAttributeMemory(address) && Dma.IsCpuOamBlocked)
        {
            value = 0xFF;
            return true;
        }

        if (!Dma.TryGetCpuConflictSourceAddress(out ushort sourceAddress))
        {
            value = 0;
            return false;
        }

        if (_dmaTransferStrategy.IsCpuAddressBlocked(address, sourceAddress))
        {
            value = ReadOamDmaSourceByte(sourceAddress);
            return true;
        }

        value = 0;
        return false;
    }

    private static bool IsObjectAttributeMemory(ushort address) =>
        address
            is >= AddressMap.ObjectAttributeMemoryStart
                and <= AddressMap.ObjectAttributeMemoryEnd;

    private bool IsCpuVideoMemoryReadBlockedByPpu(ushort address) =>
        address switch
        {
            >= AddressMap.VideoRamStart and <= AddressMap.VideoRamEnd =>
                Ppu.IsCpuVideoRamReadBlocked,
            >= AddressMap.ObjectAttributeMemoryStart and <= AddressMap.ObjectAttributeMemoryEnd =>
                Ppu.IsCpuObjectAttributeMemoryReadBlocked,
            _ => false,
        };

    private bool IsCpuVideoMemoryWriteBlockedByPpu(ushort address) =>
        address switch
        {
            >= AddressMap.VideoRamStart and <= AddressMap.VideoRamEnd =>
                Ppu.IsCpuVideoRamWriteBlocked,
            >= AddressMap.ObjectAttributeMemoryStart and <= AddressMap.ObjectAttributeMemoryEnd =>
                Ppu.IsCpuObjectAttributeMemoryWriteBlocked,
            _ => false,
        };

    private byte ReadMappedByte(ushort address)
    {
        return address switch
        {
            <= AddressMap.RomEnd => _cartridge.ReadRom(address),
            <= AddressMap.VideoRamEnd => Ppu.VideoRam.Read(address),
            <= AddressMap.ExternalRamEnd => _cartridge.ReadRam(address),
            <= AddressMap.WorkRamEnd => _workRam.Read(address),
            <= AddressMap.EchoRamEnd => _workRam.Read(address),
            <= AddressMap.ObjectAttributeMemoryEnd => Ppu.ObjectAttributeMemory.Read(address),
            <= AddressMap.NotUsableEnd => 0x00,
            <= AddressMap.IoRegistersEnd => ReadIoRegister(address),
            <= AddressMap.HighRamEnd => _highRam.Read(address),
            AddressMap.InterruptEnableRegister => Interrupts.InterruptEnable,
        };
    }

    private byte ReadOamDmaSourceByte(ushort address)
    {
        if (!_dmaTransferStrategy.TryMapSourceAddress(address, out ushort mappedAddress))
        {
            return 0xFF;
        }

        return mappedAddress switch
        {
            <= AddressMap.RomEnd => _cartridge.ReadRom(mappedAddress),
            <= AddressMap.VideoRamEnd => Ppu.VideoRam.Read(mappedAddress),
            <= AddressMap.ExternalRamEnd => _cartridge.ReadRam(mappedAddress),
            <= AddressMap.WorkRamEnd => _workRam.Read(mappedAddress),
            _ => 0xFF,
        };
    }

    private void WriteMappedByte(ushort address, byte value)
    {
        switch (address)
        {
            case <= AddressMap.RomEnd:
                _cartridge.WriteRom(address, value);
                return;
            case <= AddressMap.VideoRamEnd:
                Ppu.VideoRam.Write(address, value);
                return;
            case <= AddressMap.ExternalRamEnd:
                _cartridge.WriteRam(address, value);
                return;
            case <= AddressMap.WorkRamEnd:
            case <= AddressMap.EchoRamEnd:
                _workRam.Write(address, value);
                return;
            case <= AddressMap.ObjectAttributeMemoryEnd:
                Ppu.ObjectAttributeMemory.Write(address, value);
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

        if (ApuController.ContainsRegister(address))
        {
            return Apu.ReadRegister(address);
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
            _ => 0xFF,
        };
    }

    private void WriteIoRegister(ushort address, byte value)
    {
        if (PpuController.ContainsRegister(address))
        {
            Ppu.WriteRegister(address, value);
            return;
        }

        if (ApuController.ContainsRegister(address))
        {
            Apu.WriteRegister(address, value);
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

        if (ApuController.ContainsRegister(address))
        {
            Apu.WriteRegister(address, value);
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
                return;
        }
    }
}
