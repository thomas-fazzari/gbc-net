using GbcNet.Core.Cartridges;
using GbcNet.Core.Memory;
using GbcNet.Core.Sm83;

namespace GbcNet.Core;

/// <summary>
/// Coordinates the emulated Game Boy hardware components for one execution step.
/// </summary>
public sealed class GameBoy
{
    /// <summary>
    /// One SM83 machine cycle is four T-cycles.
    /// </summary>
    private const int TCyclesPerMachineCycle = 4;

    private readonly Cpu _cpu;

    /// <summary>
    /// Creates a Game Boy instance using the supplied cartridge.
    /// </summary>
    public GameBoy(Cartridge cartridge)
    {
        Bus = new MemoryBus(cartridge);
        _cpu = new Cpu(Bus);
    }

    /// <summary>
    /// Executes one CPU step and advances hardware that runs from CPU cycles.
    /// </summary>
    /// <returns>
    /// Elapsed machine cycles.
    /// </returns>
    public int Step()
    {
        int machineCycles = _cpu.Step();
        Bus.Timers.Tick(machineCycles * TCyclesPerMachineCycle);
        return machineCycles;
    }

    internal MemoryBus Bus { get; }
}
