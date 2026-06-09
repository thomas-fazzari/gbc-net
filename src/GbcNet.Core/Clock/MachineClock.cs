using System.Diagnostics.CodeAnalysis;
using GbcNet.Core.Memory;
using GbcNet.Core.Ppu;

namespace GbcNet.Core.Clock;

internal static class HardwareTiming
{
    /// <summary>
    /// Number of T-cycles in one CPU machine cycle.
    /// </summary>
    public const ushort MachineCycleTCycles = 4;
}

/// <summary>
/// Advances hardware components that run from CPU machine cycles.
/// </summary>
internal sealed class MachineClock(MemoryBus bus)
{
    private readonly Queue<LcdFrame> _completedFrames = new();

    /// <summary>
    /// Advances cycle-driven hardware once, preserving CPU M-cycle side-effect ordering.
    /// </summary>
    public void TickMachineCycle()
    {
        bus.Clock.TickMachineCycle();
        bus.Apu.Tick(HardwareTiming.MachineCycleTCycles);

        if (bus.Ppu.Tick(HardwareTiming.MachineCycleTCycles) is { } completedFrame)
        {
            _completedFrames.Enqueue(completedFrame);
        }

        bus.TickDma(1);
    }

    /// <summary>
    /// Removes the next frame completed during prior hardware ticks, if one is queued.
    /// </summary>
    public bool TryDequeueCompletedFrame([NotNullWhen(true)] out LcdFrame? frame) =>
        _completedFrames.TryDequeue(out frame);
}
