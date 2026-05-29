using GbcNet.Core.Memory;

namespace GbcNet.Core;

/// <summary>
/// Advances hardware components that run from CPU machine cycles.
/// </summary>
internal sealed class MachineClock(MemoryBus bus)
{
    private const int TCyclesPerMachineCycle = 4;

    public void TickMachineCycle()
    {
        bus.Timers.TickMachineCycle();
        bus.Serial.Tick(TCyclesPerMachineCycle);
        bus.Ppu.Tick(TCyclesPerMachineCycle);
        bus.TickDma(1);
    }
}
