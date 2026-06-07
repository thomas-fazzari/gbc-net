using GbcNet.Core.Apu.Components;

namespace GbcNet.Core.Apu.Channels;

/// <summary>
/// CH4 noise channel state, including envelope, length, and LFSR timing.
/// </summary>
internal sealed class NoiseChannel
{
    private const byte InitialLengthMask = 0x3F;
    private const byte LengthEnableMask = 0x40;
    private const byte TriggerMask = 0x80;
    private const byte DivisorCodeMask = 0x07;
    private const byte WidthModeMask = 0x08;
    private const int ClockShift = 4;
    private const int MaxLength = 64;
    private const int LfsrResetValue = 0;

    private readonly LengthCounter _length = new(MaxLength);
    private readonly VolumeEnvelope _envelope = new();

    private int _lfsr;
    private int _timer;
    private int _tCycleAccumulator;
    private byte _clockShift;
    private byte _divisorCode;
    private bool _widthMode;

    /// <summary>
    /// Whether CH4 generation is active and reported through NR52 bit 3.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Current envelope volume.
    /// </summary>
    public byte Volume => _envelope.Volume;

    /// <summary>
    /// Current CH4 digital output from the LFSR low bit and envelope volume.
    /// </summary>
    public byte DigitalOutput => IsActive && (_lfsr & 1) != 0 ? _envelope.Volume : (byte)0;

    /// <summary>
    /// Loads NR41 initial length.
    /// </summary>
    public void WriteLength(byte value)
    {
        _length.WriteInitialLength((byte)(value & InitialLengthMask));
    }

    /// <summary>
    /// Applies NR42 envelope and DAC-enable side effects.
    /// </summary>
    public void WriteEnvelope(byte value)
    {
        _envelope.WriteRegister(value);
        if (!_envelope.DacEnabled)
        {
            IsActive = false;
        }
    }

    /// <summary>
    /// Latches NR43 clock shift, width mode, and divisor code.
    /// </summary>
    public void WriteFrequency(byte value)
    {
        _clockShift = (byte)(value >> ClockShift);
        _widthMode = (value & WidthModeMask) != 0;
        _divisorCode = (byte)(value & DivisorCodeMask);
    }

    /// <summary>
    /// Applies NR44 length enable and trigger side effects.
    /// </summary>
    public void WriteControl(byte value, byte envelope)
    {
        _length.SetEnabled((value & LengthEnableMask) != 0);

        if ((value & TriggerMask) == 0)
        {
            return;
        }

        _length.TriggerReloadIfExpired();
        _envelope.Trigger(envelope);
        // Trigger resets the pseudo-random generator before the next clock.
        _lfsr = LfsrResetValue;
        _timer = GetPeriod();
        _tCycleAccumulator = 0;
        IsActive = _envelope.DacEnabled;
    }

    /// <summary>
    /// Advances CH4 frequency timing by elapsed T-cycles.
    /// </summary>
    public void Tick(int tCycles)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tCycles);
        // NR43 shifts 14/15 leave the LFSR unclocked
        if (!IsActive || _clockShift >= 14)
        {
            return;
        }

        _tCycleAccumulator += tCycles;

        while (_tCycleAccumulator >= _timer)
        {
            _tCycleAccumulator -= _timer;
            ClockLfsr();
        }
    }

    /// <summary>
    /// Clocks the length counter from the DIV-APU frame sequencer.
    /// </summary>
    public void ClockLength()
    {
        if (_length.Clock())
        {
            IsActive = false;
        }
    }

    /// <summary>
    /// Clocks the volume envelope from the DIV-APU frame sequencer.
    /// </summary>
    public void ClockEnvelope()
    {
        if (IsActive)
        {
            _envelope.Clock();
        }
    }

    /// <summary>
    /// Clears CH4 internal state on APU power-off.
    /// </summary>
    public void PowerOff()
    {
        _length.PowerOff();
        _envelope.PowerOff();
        _lfsr = 0;
        _timer = 0;
        _tCycleAccumulator = 0;
        _clockShift = 0;
        _divisorCode = 0;
        _widthMode = false;
        IsActive = false;
    }

    private int GetPeriod() =>
        _divisorCode == 0 ? 8 << _clockShift : (16 * _divisorCode) << _clockShift;

    private void ClockLfsr()
    {
        // Hardware feeds back bit0 XNOR bit1 into the top of the shift register
        int feedback = ~(_lfsr ^ (_lfsr >> 1)) & 1;
        _lfsr = (_lfsr >> 1) | (feedback << 14);

        if (_widthMode)
        {
            _lfsr = (_lfsr & ~0x40) | (feedback << 6);
        }
    }
}
