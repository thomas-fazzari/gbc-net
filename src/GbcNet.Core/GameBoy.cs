using GbcNet.Core.Cartridges;
using GbcNet.Core.Hardware;
using GbcNet.Core.Hardware.Profiles;
using GbcNet.Core.Joypad;
using GbcNet.Core.Memory;
using GbcNet.Core.Ppu;
using GbcNet.Core.Serial;
using GbcNet.Core.Sm83;

namespace GbcNet.Core;

/// <summary>
/// Coordinates the emulated Game Boy hardware components for one execution step.
/// </summary>
public sealed class GameBoy
{
    private readonly MachineClock _clock;

    /// <summary>
    /// Creates a Game Boy instance using the supplied cartridge.
    /// </summary>
    public GameBoy(Cartridge cartridge, HardwareModel hardwareModel)
    {
        IHardwareProfile hardwareProfile = hardwareModel switch
        {
            HardwareModel.Dmg => new DmgHardwareProfile(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(hardwareModel),
                hardwareModel,
                "Unsupported hardware model."
            ),
        };

        Bus = new MemoryBus(cartridge, hardwareProfile);
        Bus.Serial.ByteTransferred += OnSerialByteTransferred;
        _clock = new MachineClock(Bus);
        Cpu = new Cpu(Bus, _clock.TickMachineCycle);
        HardwareModel = hardwareModel;
        PostBootState.Apply(hardwareModel, cartridge, Cpu, Bus);
    }

    /// <summary>
    /// Raised when a serial transfer completes, carrying the byte latched at transfer start.
    /// </summary>
    public event EventHandler<SerialByteTransferredEventArgs>? SerialByteTransferred;

    /// <summary>
    /// Raised after a complete visible LCD frame is available at VBlank entry.
    /// </summary>
    public event EventHandler<FrameCompletedEventArgs>? FrameCompleted;

    /// <summary>
    /// Executes one CPU step and advances hardware that runs from CPU cycles.
    /// </summary>
    /// <returns>
    /// Elapsed machine cycles.
    /// </returns>
    public int Step()
    {
        int machineCycles = Cpu.Step();

        while (_clock.TryDequeueCompletedFrame(out LcdFrame? frame))
        {
            FrameCompleted?.Invoke(this, new FrameCompletedEventArgs(frame));
        }

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

    internal Cpu Cpu { get; }

    private void OnSerialByteTransferred(object? sender, SerialByteTransferredEventArgs e)
    {
        SerialByteTransferred?.Invoke(this, e);
    }
}
