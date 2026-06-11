using GbcNet.Core.Interrupts;
using GbcNet.Core.Memory;
using GbcNet.Core.Ppu.Engines;

namespace GbcNet.Core.Ppu;

/// <summary>
/// Stores CPU-visible Liquid Crystal Display/Pixel Processing Unit registers and delegates model-specific behavior.
/// </summary>
internal sealed class PpuController(
    InterruptController interrupts,
    IPpuEngine engine,
    int videoRamBankCount
)
{
    /// <summary>
    /// VRAM at 8000-9FFF, banked by VBK when the active hardware mode exposes it.
    /// </summary>
    internal VideoRam VideoRam { get; } = new(videoRamBankCount);

    /// <summary>
    /// Sprite attribute table at FE00-FE9F.
    /// </summary>
    internal MappedMemory ObjectAttributeMemory { get; } =
        new(AddressMap.ObjectAttributeMemoryStart, AddressMap.ObjectAttributeMemoryEnd);

    private byte _control;
    private byte _statusInterruptSelect;
    private byte _scrollY;
    private byte _scrollX;
    private byte _lcdYCompare;
    private byte _backgroundPalette;
    private byte _objectPalette0;
    private byte _objectPalette1;
    private byte _windowY;
    private byte _windowX;

    /// <summary>
    /// Returns whether an address is owned by the LCD/PPU register block.
    /// </summary>
    internal static bool ContainsRegister(ushort address) =>
        address
            is >= AddressMap.LcdControlRegister
                and <= AddressMap.WindowXRegister
                and not AddressMap.DmaRegister;

    /// <summary>
    /// Reads an LCD/PPU register at FF40-FF45, FF47-FF4B, or FF4F.
    /// </summary>
    public byte ReadRegister(ushort address) =>
        address switch
        {
            AddressMap.LcdControlRegister => _control,
            AddressMap.LcdStatusRegister => ReadStatus(),
            AddressMap.ScrollYRegister => _scrollY,
            AddressMap.ScrollXRegister => _scrollX,
            AddressMap.LcdYCoordinateRegister => engine.LcdYCoordinate,
            AddressMap.LcdYCompareRegister => _lcdYCompare,
            AddressMap.BackgroundPaletteRegister => _backgroundPalette,
            AddressMap.ObjectPalette0Register => _objectPalette0,
            AddressMap.ObjectPalette1Register => _objectPalette1,
            AddressMap.WindowYRegister => _windowY,
            AddressMap.WindowXRegister => _windowX,
            AddressMap.VideoRamBankRegister => VideoRam.ReadBankRegister(),
            _ => throw CreateUnsupportedRegisterException(address),
        };

    /// <summary>
    /// Indicates whether LCD/PPU timing is running.
    /// </summary>
    public bool IsLcdEnabled => (_control & PpuLcdControlRegister.LcdEnableMask) != 0;

    /// <summary>
    /// Controls whether the PPU materializes host-visible frames.
    /// </summary>
    public bool VideoRenderingEnabled { get; set; } = true;

    /// <summary>
    /// Indicates whether the PPU blocks CPU reads from VRAM at 8000-9FFF.
    /// </summary>
    public bool IsCpuVideoRamReadBlocked => IsLcdEnabled && engine.IsCpuVideoRamReadBlocked;

    /// <summary>
    /// Indicates whether the PPU blocks CPU writes to VRAM at 8000-9FFF.
    /// </summary>
    public bool IsCpuVideoRamWriteBlocked => IsLcdEnabled && engine.IsCpuVideoRamWriteBlocked;

    /// <summary>
    /// Indicates whether the PPU blocks CPU reads from OAM at FE00-FE9F.
    /// </summary>
    public bool IsCpuObjectAttributeMemoryReadBlocked =>
        IsLcdEnabled && engine.IsCpuObjectAttributeMemoryReadBlocked;

    /// <summary>
    /// Indicates whether the PPU blocks CPU writes to OAM at FE00-FE9F.
    /// </summary>
    public bool IsCpuObjectAttributeMemoryWriteBlocked =>
        IsLcdEnabled && engine.IsCpuObjectAttributeMemoryWriteBlocked;

    private PpuEngineInputs EngineInputs =>
        new(
            _control,
            _lcdYCompare,
            _statusInterruptSelect,
            _scrollY,
            _scrollX,
            _windowY,
            _windowX,
            _backgroundPalette,
            _objectPalette0,
            _objectPalette1,
            VideoRam,
            ObjectAttributeMemory
        );

    /// <summary>
    /// Writes an LCD/PPU register as the CPU sees it.
    /// </summary>
    public void WriteRegister(ushort address, byte value)
    {
        switch (address)
        {
            case AddressMap.LcdControlRegister:
                WriteLcdControl(value);
                return;
            case AddressMap.LcdStatusRegister:
                _statusInterruptSelect = (byte)(value & PpuStatusRegister.InterruptSelectMask);
                RequestInterrupts(engine.WriteStatusInterruptSelect(EngineInputs, IsLcdEnabled));
                return;
            case AddressMap.LcdYCoordinateRegister:
                return;
            case AddressMap.LcdYCompareRegister:
                _lcdYCompare = value;
                RequestInterrupts(engine.WriteLycCompare(EngineInputs, IsLcdEnabled));
                return;
            default:
                SetReadWriteRegister(address, value);
                return;
        }
    }

    /// <summary>
    /// Advances the LCD/PPU engine by elapsed dots.
    /// </summary>
    public LcdFrame? Tick(int tCycles)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tCycles);

        if (!IsLcdEnabled || tCycles == 0)
        {
            return null;
        }

        PpuEngineTickResult result = engine.Tick(tCycles, EngineInputs, VideoRenderingEnabled);
        RequestInterrupts(result.Interrupts);
        return result.CompletedFrame;
    }

    /// <summary>
    /// Seeds an LCD/PPU register without modeling a CPU bus write.
    /// </summary>
    internal void SetRegisterState(ushort address, byte value)
    {
        switch (address)
        {
            case AddressMap.LcdControlRegister:
                _control = value;
                return;
            case AddressMap.LcdStatusRegister:
                _statusInterruptSelect = (byte)(value & PpuStatusRegister.InterruptSelectMask);
                engine.SetStatusState(value, EngineInputs, IsLcdEnabled);
                return;
            case AddressMap.LcdYCoordinateRegister:
                engine.SetLcdYCoordinateState(value, EngineInputs, IsLcdEnabled);
                return;
            case AddressMap.LcdYCompareRegister:
                _lcdYCompare = value;
                engine.SetLycCompareState(EngineInputs, IsLcdEnabled);
                return;
            default:
                SetReadWriteRegister(address, value);
                return;
        }
    }

    private byte ReadStatus()
    {
        byte lycEqualsLy = engine.LycEqualsLy ? PpuStatusRegister.LycEqualsLyMask : (byte)0;
        byte mode = IsLcdEnabled ? (byte)engine.StatusMode : (byte)PpuMode.HBlank;

        return (byte)(PpuStatusRegister.ReadMask | _statusInterruptSelect | lycEqualsLy | mode);
    }

    private void SetReadWriteRegister(ushort address, byte value)
    {
        switch (address)
        {
            case AddressMap.ScrollYRegister:
                _scrollY = value;
                return;
            case AddressMap.ScrollXRegister:
                _scrollX = value;
                return;
            case AddressMap.BackgroundPaletteRegister:
                _backgroundPalette = value;
                return;
            case AddressMap.ObjectPalette0Register:
                _objectPalette0 = value;
                return;
            case AddressMap.ObjectPalette1Register:
                _objectPalette1 = value;
                return;
            case AddressMap.WindowYRegister:
                _windowY = value;
                return;
            case AddressMap.WindowXRegister:
                _windowX = value;
                return;
            case AddressMap.VideoRamBankRegister:
                VideoRam.WriteBankRegister(value);
                return;
            default:
                throw CreateUnsupportedRegisterException(address);
        }
    }

    private void WriteLcdControl(byte value)
    {
        bool wasEnabled = IsLcdEnabled;
        _control = value;

        if (wasEnabled && !IsLcdEnabled)
        {
            engine.DisableLcd();
            return;
        }

        if (!wasEnabled && IsLcdEnabled)
        {
            RequestInterrupts(engine.EnableLcd(EngineInputs, VideoRenderingEnabled));
        }
    }

    private void RequestInterrupts(PpuInterruptRequest requests)
    {
        if ((requests & PpuInterruptRequest.VBlank) is not PpuInterruptRequest.None)
        {
            interrupts.Request(InterruptSource.VBlank);
        }

        if ((requests & PpuInterruptRequest.Lcd) is not PpuInterruptRequest.None)
        {
            interrupts.Request(InterruptSource.Lcd);
        }
    }

    private static ArgumentOutOfRangeException CreateUnsupportedRegisterException(ushort address) =>
        new(nameof(address), address, "Address must target an LCD/PPU register.");
}
