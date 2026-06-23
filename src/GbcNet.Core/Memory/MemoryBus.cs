using GbcNet.Core.Apu;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Clock;
using GbcNet.Core.Dma;
using GbcNet.Core.Dma.Policies;
using GbcNet.Core.Hardware.Profiles;
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

    private readonly WorkRam _workRam;
    private readonly Cartridge _cartridge;
    private readonly ITransferPolicy _oamDmaTransferPolicy;
    private readonly Func<ushort, byte> _readByteForOamDma;
    private readonly Action<ushort, byte> _writeOamByteForDma;
    private readonly IoRegisters _ioRegisters;
    private readonly BootRom? _bootRom;

    /// <summary>
    /// Interrupt request and enable registers routed through FF0F and FFFF.
    /// </summary>
    public InterruptController Interrupts { get; }

    /// <summary>
    /// Shared clock controller routed through DIV and used by timer, serial, and APU frame clocks.
    /// </summary>
    public ClockController Clock { get; }

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
    /// LCD/PPU registers routed through the PPU controller.
    /// </summary>
    public PpuController Ppu { get; }

    /// <summary>
    /// OAM DMA register routed through FF46.
    /// </summary>
    public OamDmaController OamDma { get; }

    /// <summary>
    /// CGB VRAM DMA registers routed through FF51-FF55.
    /// </summary>
    public CgbVramDmaController VramDma { get; }

    /// <summary>
    /// Optional instrumentation sink for debugger/watchpoint tooling.
    /// </summary>
    internal ICpuMemoryWriteObserver? CpuMemoryWriteObserver { private get; set; }

    /// <summary>
    /// Creates the CPU-visible bus and model-specific DMA/PPU policies for a cartridge.
    /// </summary>
    public MemoryBus(Cartridge cartridge, IHardwareProfile hardwareProfile, BootRom? bootRom = null)
    {
        _cartridge = cartridge;
        _bootRom = bootRom;
        _workRam = new WorkRam(
            hardwareProfile.WorkRamBankCount,
            hardwareProfile.IsWorkRamBankRegisterEnabled
        );

        var cgbMiscRegisters = new CgbMiscRegisters(
            hardwareProfile.IsCgbHardwareMiscRegisterEnabled,
            hardwareProfile.IsCgbUndocumentedFf74RegisterEnabled
        );

        _oamDmaTransferPolicy = hardwareProfile.CreateOamDmaTransferPolicy();

        Interrupts = new InterruptController();
        Joypad = new JoypadController(Interrupts);

        Serial = new SerialController(Interrupts, hardwareProfile.IsSerialHighSpeedClockEnabled);

        Apu = new ApuController(hardwareProfile.CreateApuHardwareProfile());

        Clock = new ClockController(
            Interrupts,
            Serial,
            Apu,
            hardwareProfile.IsKey1RegisterEnabled,
            hardwareProfile.TicksTimerOnTacDisableWhenInputHigh,
            hardwareProfile.TicksTimerOnTacEnableWhenInputHigh
        );

        Timers = Clock.Timers;

        Ppu = new PpuController(
            Interrupts,
            hardwareProfile.CreatePpuEngine(),
            hardwareProfile.VideoRamBankCount,
            hardwareProfile.IsVideoRamBankRegisterEnabled,
            hardwareProfile.IsColorPaletteIndexRegisterEnabled,
            hardwareProfile.IsColorPaletteRamEnabled,
            hardwareProfile.IsObjectPriorityModeRegisterEnabled
        );

        OamDma = new OamDmaController();
        VramDma = new CgbVramDmaController(
            hardwareProfile.IsVideoRamDmaRegisterEnabled,
            () => Clock.CgbDoubleSpeed,
            ReadVramDmaSourceByte,
            Ppu.VideoRam.Write
        );

        _ioRegisters = new IoRegisters(
            Interrupts,
            Clock,
            Joypad,
            Serial,
            Apu,
            Ppu,
            _workRam,
            cgbMiscRegisters,
            OamDma,
            VramDma
        );

        _readByteForOamDma = ReadOamDmaSourceByte;
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
                _ioRegisters.SetState(address, value);
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

    /// <summary>
    /// Reads a CPU-visible byte, applying active OAM DMA conflicts and PPU bus blocking.
    /// </summary>
    public byte ReadByte(ushort address)
    {
        if (TryReadDmaConflictedByte(address, out var value))
        {
            return value;
        }

        return IsCpuVideoMemoryReadBlockedByPpu(address) ? (byte)0xFF : ReadMappedByte(address);
    }

    /// <summary>
    /// Writes a CPU-visible byte unless OAM DMA or PPU ownership blocks the address.
    /// </summary>
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
        OamDma.Tick(machineCycles, _readByteForOamDma, _writeOamByteForDma);
    }

    private bool IsCpuWriteBlocked(ushort address) =>
        IsCpuWriteBlockedByDma(address) || IsCpuVideoMemoryWriteBlockedByPpu(address);

    private bool IsCpuWriteBlockedByDma(ushort address) =>
        (IsObjectAttributeMemory(address) && OamDma.IsCpuOamBlocked)
        || (
            OamDma.TryGetCpuConflictSourceAddress(out var sourceAddress)
            && _oamDmaTransferPolicy.IsCpuAddressBlocked(address, sourceAddress)
        );

    private bool TryReadDmaConflictedByte(ushort address, out byte value)
    {
        if (IsObjectAttributeMemory(address) && OamDma.IsCpuOamBlocked)
        {
            value = 0xFF;
            return true;
        }

        if (!OamDma.TryGetCpuConflictSourceAddress(out var sourceAddress))
        {
            value = 0;
            return false;
        }

        if (_oamDmaTransferPolicy.IsCpuAddressBlocked(address, sourceAddress))
        {
            // During a DMA bus conflict, the CPU sees the byte currently driven by DMA source reads
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

    private byte ReadMappedByte(ushort address) =>
        address switch
        {
            <= AddressMap.RomEnd => ReadRomWindowByte(address),
            <= AddressMap.VideoRamEnd => Ppu.VideoRam.Read(address),
            <= AddressMap.ExternalRamEnd => _cartridge.ReadRam(address),
            <= AddressMap.WorkRamEnd => _workRam.Read(address),
            <= AddressMap.EchoRamEnd => _workRam.Read(address),
            <= AddressMap.ObjectAttributeMemoryEnd => Ppu.ObjectAttributeMemory.Read(address),
            <= AddressMap.NotUsableEnd => 0x00,
            <= AddressMap.IoRegistersEnd => _ioRegisters.Read(address),
            <= AddressMap.HighRamEnd => _highRam.Read(address),
            AddressMap.InterruptEnableRegister => Interrupts.InterruptEnable,
        };

    private byte ReadOamDmaSourceByte(ushort address)
    {
        var mappedAddress = _oamDmaTransferPolicy.MapSourceAddress(address);

        return mappedAddress switch
        {
            <= AddressMap.RomEnd => ReadRomWindowByte(mappedAddress),
            <= AddressMap.VideoRamEnd => Ppu.VideoRam.Read(mappedAddress),
            <= AddressMap.ExternalRamEnd => _cartridge.ReadRam(mappedAddress),
            <= AddressMap.WorkRamEnd => _workRam.Read(mappedAddress),
            _ => 0xFF,
        };
    }

    private byte ReadVramDmaSourceByte(ushort address) =>
        address switch
        {
            <= AddressMap.RomEnd => ReadRomWindowByte(address),
            >= AddressMap.ExternalRamStart and <= AddressMap.ExternalRamEnd => _cartridge.ReadRam(
                address
            ),
            >= AddressMap.WorkRamStart and <= AddressMap.WorkRamEnd => _workRam.Read(address),
            _ => 0xFF,
        };

    private byte ReadRomWindowByte(ushort address) =>
        _bootRom is not null && _bootRom.TryRead(address, out var value)
            ? value
            : _cartridge.ReadRom(address);

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
            case AddressMap.BootRomDisableRegister:
                _bootRom?.WriteDisableRegister(value);
                return;
            case <= AddressMap.IoRegistersEnd:
                _ioRegisters.WriteCpu(address, value);
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
