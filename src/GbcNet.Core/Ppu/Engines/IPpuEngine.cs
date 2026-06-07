namespace GbcNet.Core.Ppu.Engines;

/// <summary>
/// Models hardware-specific LCD behavior and exposes the resulting CPU-visible signals.
/// </summary>
internal interface IPpuEngine
{
    /// <summary>
    /// Current LY register value exposed at FF44.
    /// </summary>
    byte LcdYCoordinate { get; }

    /// <summary>
    /// Current LYC=LY comparison result exposed through STAT bit 2.
    /// </summary>
    bool LycEqualsLy { get; }

    /// <summary>
    /// Current STAT mode bits exposed through STAT bits 1-0.
    /// </summary>
    PpuMode StatusMode { get; }

    /// <summary>
    /// Indicates that CPU reads from VRAM return FF because the LCD engine owns the bus.
    /// </summary>
    bool IsCpuVideoRamReadBlocked { get; }

    /// <summary>
    /// Indicates that CPU writes to VRAM are ignored because the LCD engine owns the bus.
    /// </summary>
    bool IsCpuVideoRamWriteBlocked { get; }

    /// <summary>
    /// Indicates that CPU reads from OAM return FF because the LCD engine owns the bus.
    /// </summary>
    bool IsCpuObjectAttributeMemoryReadBlocked { get; }

    /// <summary>
    /// Indicates that CPU writes to OAM are ignored because the LCD engine owns the bus.
    /// </summary>
    bool IsCpuObjectAttributeMemoryWriteBlocked { get; }

    /// <summary>
    /// Advances LCD timing by elapsed T-cycles and returns interrupt requests or a completed frame.
    /// </summary>
    PpuEngineTickResult Tick(int tCycles, PpuEngineInputs inputs);

    /// <summary>
    /// Applies the model-specific LCD-enable transition and returns any STAT interrupt request.
    /// </summary>
    PpuInterruptRequest EnableLcd(PpuEngineInputs inputs);

    /// <summary>
    /// Applies the model-specific LCD-disable transition and clears transient rendering state.
    /// </summary>
    void DisableLcd();

    /// <summary>
    /// Recomputes the STAT interrupt line after CPU-visible STAT interrupt select bits change.
    /// </summary>
    PpuInterruptRequest WriteStatusInterruptSelect(PpuEngineInputs inputs, bool lcdEnabled);

    /// <summary>
    /// Recomputes the LYC=LY comparison and STAT interrupt line after LYC changes.
    /// </summary>
    PpuInterruptRequest WriteLycCompare(PpuEngineInputs inputs, bool lcdEnabled);

    /// <summary>
    /// Seeds STAT mode and LYC=LY state without CPU write side effects.
    /// </summary>
    void SetStatusState(byte value, PpuEngineInputs inputs, bool lcdEnabled);

    /// <summary>
    /// Seeds LY state without CPU write side effects.
    /// </summary>
    void SetLcdYCoordinateState(byte value, PpuEngineInputs inputs, bool lcdEnabled);

    /// <summary>
    /// Seeds LYC comparison state without CPU write side effects.
    /// </summary>
    void SetLycCompareState(PpuEngineInputs inputs, bool lcdEnabled);
}
