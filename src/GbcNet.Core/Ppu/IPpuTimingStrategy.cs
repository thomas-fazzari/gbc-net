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

    PpuInterruptRequest Tick(int tCycles, byte lcdYCompare, byte statusInterruptSelect);

    PpuInterruptRequest EnableLcd(byte lcdYCompare, byte statusInterruptSelect);

    void DisableLcd();

    PpuInterruptRequest WriteStatusInterruptSelect(byte statusInterruptSelect, bool lcdEnabled);

    PpuInterruptRequest WriteLycCompare(
        byte lcdYCompare,
        byte statusInterruptSelect,
        bool lcdEnabled
    );

    void SetStatusState(byte value, byte statusInterruptSelect, bool lcdEnabled);

    void SetLcdYCoordinateState(
        byte value,
        byte lcdYCompare,
        byte statusInterruptSelect,
        bool lcdEnabled
    );

    void SetLycCompareState(byte lcdYCompare, byte statusInterruptSelect, bool lcdEnabled);
}
