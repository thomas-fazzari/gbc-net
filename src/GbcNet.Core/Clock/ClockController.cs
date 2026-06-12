using GbcNet.Core.Apu;
using GbcNet.Core.Interrupts;
using GbcNet.Core.Serial;
using GbcNet.Core.Timers;

namespace GbcNet.Core.Clock;

/// <summary>
/// Owns the shared system counter and dispatches DIV falling-edge effects.
/// </summary>
internal sealed class ClockController
{
    private const byte Key1SwitchArmedMask = 0x01;
    private const byte Key1ReadMask = 0x7E;
    private const byte Key1CurrentSpeedMask = 0x80;
    private const byte DisabledRegisterValue = 0xFF;

    private readonly SystemCounter _systemCounter = new();
    private readonly SerialController _serial;
    private readonly ApuController _apu;

    private readonly bool _isKey1RegisterEnabled;
    private bool _speedSwitchArmed;

    public ClockController(
        InterruptController interrupts,
        SerialController serial,
        ApuController apu,
        bool isKey1RegisterEnabled = false
    )
    {
        _serial = serial;
        _apu = apu;
        _isKey1RegisterEnabled = isKey1RegisterEnabled;
        Timers = new TimerController(interrupts, _systemCounter);
    }

    /// <summary>
    /// Programmable timer registers clocked from the shared system counter.
    /// </summary>
    public TimerController Timers { get; }

    /// <summary>
    /// Indicates that CGB CPU double-speed mode is active.
    /// </summary>
    public bool CgbDoubleSpeed { get; private set; }

    /// <summary>
    /// Number of PPU/APU T-cycles elapsed per CPU machine cycle at the current speed.
    /// </summary>
    public int VideoAndAudioTCyclesPerMachineCycle =>
        CgbDoubleSpeed
            ? HardwareTiming.DoubleSpeedMachineCycleTCycles
            : HardwareTiming.MachineCycleTCycles;

    /// <summary>
    /// Reads the CPU-visible DIV register value.
    /// </summary>
    public byte ReadDivider() => _systemCounter.ReadDivider();

    /// <summary>
    /// Reads KEY1 with current speed, armed bit, and unused bits set.
    /// </summary>
    public byte ReadKey1()
    {
        if (!_isKey1RegisterEnabled)
        {
            return DisabledRegisterValue;
        }

        var value = Key1ReadMask;
        if (CgbDoubleSpeed)
        {
            value |= Key1CurrentSpeedMask;
        }

        if (_speedSwitchArmed)
        {
            value |= Key1SwitchArmedMask;
        }

        return value;
    }

    /// <summary>
    /// Writes KEY1; only bit 0 is CPU-writable.
    /// </summary>
    public void WriteKey1(byte value)
    {
        if (_isKey1RegisterEnabled)
        {
            _speedSwitchArmed = (value & Key1SwitchArmedMask) != 0;
        }
    }

    /// <summary>
    /// Seeds KEY1 without modeling a CPU write.
    /// </summary>
    internal void SetKey1State(byte value)
    {
        if (!_isKey1RegisterEnabled)
        {
            return;
        }

        CgbDoubleSpeed = (value & Key1CurrentSpeedMask) != 0;
        _speedSwitchArmed = (value & Key1SwitchArmedMask) != 0;
    }

    /// <summary>
    /// Applies a CGB speed switch requested by STOP, if armed.
    /// </summary>
    public bool TrySwitchSpeedOnStop()
    {
        if (!_isKey1RegisterEnabled || !_speedSwitchArmed)
        {
            return false;
        }

        ResetDivider();
        _speedSwitchArmed = false;
        CgbDoubleSpeed = !CgbDoubleSpeed;
        return true;
    }

    /// <summary>
    /// Advances the system counter one CPU machine cycle and dispatches falling-edge events.
    /// </summary>
    public void TickMachineCycle()
    {
        Timers.AdvanceReloadPipeline();
        DispatchSystemCounterFallingEdges(_systemCounter.AdvanceMachineCycle());
    }

    /// <summary>
    /// Clears DIV as a CPU write would, including falling-edge side effects.
    /// </summary>
    public void ResetDivider()
    {
        DispatchSystemCounterFallingEdges(_systemCounter.Reset());
    }

    /// <summary>
    /// Seeds the visible divider state without CPU write side effects.
    /// </summary>
    internal void SetDivider(byte value)
    {
        _systemCounter.SetDivider(value);
        _serial.SetMasterClockStateFromCounter(_systemCounter.Value);
    }

    /// <summary>
    /// Seeds the raw system counter state without CPU write side effects.
    /// </summary>
    internal void SetCounter(ushort value)
    {
        _systemCounter.SetCounter(value);
        _serial.SetMasterClockStateFromCounter(_systemCounter.Value);
    }

    private void DispatchSystemCounterFallingEdges(ushort fallingEdges)
    {
        Timers.TickSystemCounter(fallingEdges);
        _serial.TickSystemCounter(fallingEdges);
        _apu.TickSystemCounter(new ApuTickInputs(fallingEdges, CgbDoubleSpeed));
    }
}
