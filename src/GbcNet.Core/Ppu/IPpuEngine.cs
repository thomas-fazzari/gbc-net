namespace GbcNet.Core.Ppu;

/// <summary>
/// Models hardware-specific LCD behavior and exposes the resulting CPU-visible signals.
/// </summary>
internal interface IPpuEngine
{
    byte LcdYCoordinate { get; }

    bool LycEqualsLy { get; }

    PpuMode StatusMode { get; }

    bool IsCpuVideoRamReadBlocked { get; }

    bool IsCpuVideoRamWriteBlocked { get; }

    bool IsCpuObjectAttributeMemoryReadBlocked { get; }

    bool IsCpuObjectAttributeMemoryWriteBlocked { get; }

    PpuEngineTickResult Tick(int tCycles, PpuEngineInputs inputs);

    PpuInterruptRequest EnableLcd(PpuEngineInputs inputs);

    void DisableLcd();

    PpuInterruptRequest WriteStatusInterruptSelect(PpuEngineInputs inputs, bool lcdEnabled);

    PpuInterruptRequest WriteLycCompare(PpuEngineInputs inputs, bool lcdEnabled);

    void SetStatusState(byte value, PpuEngineInputs inputs, bool lcdEnabled);

    void SetLcdYCoordinateState(byte value, PpuEngineInputs inputs, bool lcdEnabled);

    void SetLycCompareState(PpuEngineInputs inputs, bool lcdEnabled);
}
