using GbcNet.Core.Hardware.Profiles;
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
    int videoRamBankCount,
    bool isCgbHardware,
    bool isColorPaletteRamEnabled,
    bool isObjectPriorityModeRegisterEnabled
)
{
    private const byte ObjectPriorityModeReadMask = 0xFE;

    private const ushort WhiteRgb555 = 0x7FFF;

    /// <summary>
    /// VRAM at 8000-9FFF, banked by VBK when the active hardware mode exposes it.
    /// </summary>
    internal VideoRam VideoRam { get; } = new(videoRamBankCount, isCgbHardware);

    /// <summary>
    /// CGB background color palette RAM accessed through BGPI/BGPD.
    /// </summary>
    private CgbPaletteRam BgPaletteRam { get; } = new(isCgbHardware, isColorPaletteRamEnabled);

    /// <summary>
    /// CGB object color palette RAM accessed through OBPI/OBPD.
    /// </summary>
    private CgbPaletteRam ObjectPaletteRam { get; } = new(isCgbHardware, isColorPaletteRamEnabled);

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
    private ObjectPriorityMode _objectPriorityMode;

    /// <summary>
    /// Returns whether an address is owned by the LCD/PPU register block.
    /// </summary>
    internal static bool ContainsRegister(ushort address) =>
        address
            is >= AddressMap.LcdControlRegister
                and <= AddressMap.WindowXRegister
                and not AddressMap.DmaRegister;

    /// <summary>
    /// Reads an LCD/PPU register at FF40-FF45, FF47-FF4B, FF4F, FF68-FF6C.
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
            AddressMap.BackgroundPaletteIndexRegister => BgPaletteRam.ReadIndexRegister(),
            AddressMap.BackgroundPaletteDataRegister => BgPaletteRam.ReadDataRegister(),
            AddressMap.ObjectPaletteIndexRegister => ObjectPaletteRam.ReadIndexRegister(),
            AddressMap.ObjectPaletteDataRegister => ObjectPaletteRam.ReadDataRegister(),
            AddressMap.ObjectPriorityModeRegister => ReadObjectPriorityModeRegister(),
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
            _objectPriorityMode,
            VideoRam,
            BgPaletteRam,
            ObjectPaletteRam,
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
    public PpuTickResult Tick(int tCycles)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tCycles);

        if (!IsLcdEnabled || tCycles == 0)
        {
            return new PpuTickResult(CompletedFrame: null, EnteredVisibleHBlank: false);
        }

        var result = engine.Tick(tCycles, EngineInputs, VideoRenderingEnabled);
        RequestInterrupts(result.Interrupts);
        return new PpuTickResult(result.CompletedFrame, result.EnteredVisibleHBlank);
    }

    /// <summary>
    /// Seeds CGB background color palette RAM to boot-ROM white without changing palette index registers.
    /// </summary>
    internal void SetBackgroundColorPaletteRamToWhite()
    {
        BgPaletteRam.SetAllColorsRgb555(WhiteRgb555);
    }

    /// <summary>
    /// Seeds the CGB color palettes used to render DMG software in compatibility mode.
    /// </summary>
    internal void SetDmgCompatibilityColorPaletteRam(CgbCompatibilityPalettes palettes)
    {
        var background = palettes.Background;
        SetPalette(
            BgPaletteRam,
            0,
            background.Color0,
            background.Color1,
            background.Color2,
            background.Color3
        );

        var objectPalette0 = palettes.ObjectPalette0;
        SetPalette(
            ObjectPaletteRam,
            0,
            objectPalette0.Color0,
            objectPalette0.Color1,
            objectPalette0.Color2,
            objectPalette0.Color3
        );

        var objectPalette1 = palettes.ObjectPalette1;
        SetPalette(
            ObjectPaletteRam,
            1,
            objectPalette1.Color0,
            objectPalette1.Color1,
            objectPalette1.Color2,
            objectPalette1.Color3
        );
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

    private static void SetPalette(
        CgbPaletteRam paletteRam,
        int paletteIndex,
        ushort color0,
        ushort color1,
        ushort color2,
        ushort color3
    )
    {
        paletteRam.SetRgb555Color(paletteIndex, 0, color0);
        paletteRam.SetRgb555Color(paletteIndex, 1, color1);
        paletteRam.SetRgb555Color(paletteIndex, 2, color2);
        paletteRam.SetRgb555Color(paletteIndex, 3, color3);
    }

    private byte ReadObjectPriorityModeRegister() =>
        isObjectPriorityModeRegisterEnabled
            ? (byte)(ObjectPriorityModeReadMask | (byte)_objectPriorityMode)
            : (byte)0xFF;

    private byte ReadStatus()
    {
        var lycEqualsLy = engine.LycEqualsLy ? PpuStatusRegister.LycEqualsLyMask : (byte)0;
        var mode = IsLcdEnabled ? (byte)engine.StatusMode : (byte)PpuMode.HBlank;

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
            case AddressMap.BackgroundPaletteIndexRegister:
                BgPaletteRam.WriteIndexRegister(value);
                return;
            case AddressMap.BackgroundPaletteDataRegister:
                BgPaletteRam.WriteDataRegister(value);
                return;
            case AddressMap.ObjectPaletteIndexRegister:
                ObjectPaletteRam.WriteIndexRegister(value);
                return;
            case AddressMap.ObjectPaletteDataRegister:
                ObjectPaletteRam.WriteDataRegister(value);
                return;
            case AddressMap.ObjectPriorityModeRegister:
                if (isObjectPriorityModeRegisterEnabled)
                {
                    _objectPriorityMode =
                        (value & 0x01) == 0
                            ? ObjectPriorityMode.OamOrder
                            : ObjectPriorityMode.XCoordinate;
                }

                return;
            default:
                throw CreateUnsupportedRegisterException(address);
        }
    }

    private void WriteLcdControl(byte value)
    {
        var wasEnabled = IsLcdEnabled;
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
