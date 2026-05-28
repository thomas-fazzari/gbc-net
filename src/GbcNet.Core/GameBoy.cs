using GbcNet.Core.Cartridges;
using GbcNet.Core.Joypad;
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
    public GameBoy(Cartridge cartridge, HardwareModel hardwareModel)
    {
        Bus = new MemoryBus(cartridge);
        _cpu = new Cpu(Bus);
        HardwareModel = hardwareModel;
        PostBootState.Apply(hardwareModel, cartridge, _cpu, Bus);
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
        int tCycles = machineCycles * TCyclesPerMachineCycle;
        Bus.Timers.Tick(tCycles);
        Bus.Ppu.Tick(tCycles);
        Bus.TickDma(machineCycles);
        return machineCycles;
    }

    /// <summary>
    /// Updates a joypad button state for the emulated machine.
    /// </summary>
    public void SetButtonState(JoypadButton button, bool pressed)
    {
        Bus.Joypad.SetButtonState(button, pressed);
    }

    /// <summary>
    /// Hardware model selected for this emulation instance.
    /// </summary>
    public HardwareModel HardwareModel { get; }

    internal MemoryBus Bus { get; }
}
