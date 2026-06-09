using GbcNet.Core.Apu;
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
        ArgumentNullException.ThrowIfNull(cartridge);

        IHardwareProfile hardwareProfile = HardwareProfileFactory.Create(
            hardwareModel,
            cartridge.Header
        );

        Bus = new MemoryBus(cartridge, hardwareProfile);
        Bus.Serial.ByteTransferred += OnSerialByteTransferred;
        _clock = new MachineClock(Bus);
        Cpu = new Cpu(Bus, _clock.TickMachineCycle);
        HardwareModel = hardwareProfile.Model;
        PostBootState.Apply(hardwareProfile, cartridge, Cpu, Bus);
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
    /// Enables host-visible LCD frame rendering while keeping LCD timing active either way.
    /// </summary>
    public bool VideoRenderingEnabled
    {
        get => Bus.Ppu.VideoRenderingEnabled;
        set => Bus.Ppu.VideoRenderingEnabled = value;
    }

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
    /// Drains signed PCM-friendly APU stereo samples after output conditioning.
    /// </summary>
    public int DrainAudioSamples(Span<ApuStereoSample> destination) =>
        Bus.Apu.DrainBufferedSamples(destination);

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
