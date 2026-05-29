using GbcNet.Core.Cartridges;
using GbcNet.Core.Joypad;
using GbcNet.Core.Memory;
using GbcNet.Core.Serial;
using GbcNet.Core.Sm83;

namespace GbcNet.Core;

/// <summary>
/// Coordinates the emulated Game Boy hardware components for one execution step.
/// </summary>
public sealed class GameBoy
{
    /// <summary>
    /// Creates a Game Boy instance using the supplied cartridge.
    /// </summary>
    public GameBoy(Cartridge cartridge, HardwareModel hardwareModel)
    {
        Bus = new MemoryBus(cartridge);
        Bus.Serial.ByteTransferred += OnSerialByteTransferred;
        var clock = new MachineClock(Bus);
        Cpu = new Cpu(Bus, clock.TickMachineCycle);
        HardwareModel = hardwareModel;
        PostBootState.Apply(hardwareModel, cartridge, Cpu, Bus);
    }

    /// <summary>
    /// Raised when a serial transfer completes, carrying the byte latched at transfer start.
    /// </summary>
    public event EventHandler<SerialByteTransferredEventArgs>? SerialByteTransferred;

    /// <summary>
    /// Executes one CPU step and advances hardware that runs from CPU cycles.
    /// </summary>
    /// <returns>
    /// Elapsed machine cycles.
    /// </returns>
    public int Step() => Cpu.Step();

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

    internal Cpu Cpu { get; }

    private void OnSerialByteTransferred(object? sender, SerialByteTransferredEventArgs e)
    {
        SerialByteTransferred?.Invoke(this, e);
    }
}
