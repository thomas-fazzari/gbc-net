namespace GbcNet.Core.Ppu;

/// <summary>
/// Models hardware-specific LCD timing and exposes the resulting CPU-visible signals.
/// </summary>
internal interface IPpuTimingStrategy
{
    byte LcdYCoordinate { get; }

    bool LycEqualsLy { get; }

    PpuMode StatusMode { get; }

    bool IsCpuVideoRamReadBlocked { get; }

    bool IsCpuVideoRamWriteBlocked { get; }

    bool IsCpuObjectAttributeMemoryReadBlocked { get; }

    bool IsCpuObjectAttributeMemoryWriteBlocked { get; }

    PpuInterruptRequest Tick(int tCycles, PpuTimingInputs inputs);

    PpuInterruptRequest EnableLcd(PpuTimingInputs inputs);

    void DisableLcd();

    PpuInterruptRequest WriteStatusInterruptSelect(PpuTimingInputs inputs, bool lcdEnabled);

    PpuInterruptRequest WriteLycCompare(PpuTimingInputs inputs, bool lcdEnabled);

    void SetStatusState(byte value, PpuTimingInputs inputs, bool lcdEnabled);

    void SetLcdYCoordinateState(byte value, PpuTimingInputs inputs, bool lcdEnabled);

    void SetLycCompareState(PpuTimingInputs inputs, bool lcdEnabled);
}
