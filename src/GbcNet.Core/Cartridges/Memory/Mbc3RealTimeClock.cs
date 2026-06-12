using System.Buffers.Binary;

namespace GbcNet.Core.Cartridges.Memory;

/// <summary>
/// MBC3 real-time clock registers, latch behavior, and persistence state.
/// </summary>
internal sealed class Mbc3RealTimeClock(Func<long> getUnixTimeSeconds)
{
    public const byte SecondsRegister = 0x08;
    public const byte MinutesRegister = 0x09;
    public const byte HoursRegister = 0x0A;
    public const byte DayLowRegister = 0x0B;
    public const byte DayHighRegister = 0x0C;
    public const int SaveStateSize = 48;

    private const int RtcTimeStateSize = 20;
    private const int RealRtcStateOffset = 0;
    private const int LatchedRtcStateOffset = 20;
    private const int LastUnixTimeSecondsOffset = 40;
    private const int MaxDay = 0x1FF;
    private const byte SecondsMask = 0x3F;
    private const byte MinutesMask = 0x3F;
    private const byte HoursMask = 0x1F;
    private const byte DayHighDayBitMask = 0x01;
    private const byte DayHighHaltMask = 0x40;
    private const byte DayHighCarryMask = 0x80;

    private int _seconds;
    private int _minutes;
    private int _hours;
    private int _day;
    private bool _halted;
    private bool _carry;
    private int _latchedSeconds;
    private int _latchedMinutes;
    private int _latchedHours;
    private int _latchedDay;
    private bool _latchedHalted;
    private bool _latchedCarry;
    private long _lastUnixTimeSeconds = getUnixTimeSeconds();

    /// <summary>
    /// Indicates that RTC state changed since the last import or clear.
    /// </summary>
    public bool IsDirty { get; private set; }

    /// <summary>
    /// Catches live RTC counters up to the injected clock.
    /// </summary>
    public void RefreshFromClock()
    {
        UpdateToNow();
    }

    /// <summary>
    /// Copies the live RTC counters into the CPU-visible latched registers.
    /// </summary>
    public void Latch()
    {
        UpdateToNow();
        _latchedSeconds = _seconds;
        _latchedMinutes = _minutes;
        _latchedHours = _hours;
        _latchedDay = _day;
        _latchedHalted = _halted;
        _latchedCarry = _carry;
        IsDirty = true;
    }

    /// <summary>
    /// Reads a latched RTC register selected by MBC3 register values 08-0C.
    /// </summary>
    public byte ReadRegister(byte register) =>
        register switch
        {
            SecondsRegister => (byte)_latchedSeconds,
            MinutesRegister => (byte)_latchedMinutes,
            HoursRegister => (byte)_latchedHours,
            DayLowRegister => (byte)_latchedDay,
            DayHighRegister => GetDayHigh(_latchedDay, _latchedHalted, _latchedCarry),
            _ => 0xFF,
        };

    /// <summary>
    /// Writes a live RTC register selected by MBC3 register values 08-0C.
    /// </summary>
    public void WriteRegister(byte register, byte value)
    {
        UpdateToNow();

        switch (register)
        {
            case SecondsRegister:
                _seconds = value & SecondsMask;
                break;
            case MinutesRegister:
                _minutes = value & MinutesMask;
                break;
            case HoursRegister:
                _hours = value & HoursMask;
                break;
            case DayLowRegister:
                _day = (_day & 0x100) | value;
                break;
            case DayHighRegister:
                _day = (_day & 0xFF) | ((value & DayHighDayBitMask) << 8);
                _halted = (value & DayHighHaltMask) != 0;
                _carry = (value & DayHighCarryMask) != 0;
                break;
            default:
                return;
        }

        IsDirty = true;
    }

    /// <summary>
    /// Exports live and latched RTC state with the timestamp used for future catch-up.
    /// </summary>
    public byte[] ExportState()
    {
        UpdateToNow();

        var data = new byte[SaveStateSize];

        WriteRegisters(
            data.AsSpan(RealRtcStateOffset, RtcTimeStateSize),
            _seconds,
            _minutes,
            _hours,
            _day,
            _halted,
            _carry
        );
        WriteRegisters(
            data.AsSpan(LatchedRtcStateOffset, RtcTimeStateSize),
            _latchedSeconds,
            _latchedMinutes,
            _latchedHours,
            _latchedDay,
            _latchedHalted,
            _latchedCarry
        );
        BinaryPrimitives.WriteInt64LittleEndian(
            data.AsSpan(LastUnixTimeSecondsOffset, sizeof(long)),
            _lastUnixTimeSeconds
        );

        return data;
    }

    /// <summary>
    /// Imports live and latched RTC state, then catches up to the injected clock.
    /// </summary>
    public void ImportState(ReadOnlySpan<byte> data)
    {
        if (data.Length != SaveStateSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(data),
                data.Length,
                "Invalid RTC state size."
            );
        }

        ReadRegisters(
            data.Slice(RealRtcStateOffset, RtcTimeStateSize),
            out _seconds,
            out _minutes,
            out _hours,
            out _day,
            out _halted,
            out _carry
        );
        ReadRegisters(
            data.Slice(LatchedRtcStateOffset, RtcTimeStateSize),
            out _latchedSeconds,
            out _latchedMinutes,
            out _latchedHours,
            out _latchedDay,
            out _latchedHalted,
            out _latchedCarry
        );
        _lastUnixTimeSeconds = BinaryPrimitives.ReadInt64LittleEndian(
            data.Slice(LastUnixTimeSecondsOffset, sizeof(long))
        );
        IsDirty = false;
        UpdateToNow();
    }

    /// <summary>
    /// Marks RTC persistence state clean after save data has been written.
    /// </summary>
    public void ClearDirty()
    {
        IsDirty = false;
    }

    private void UpdateToNow()
    {
        var now = getUnixTimeSeconds();
        if (now <= _lastUnixTimeSeconds)
        {
            if (now < _lastUnixTimeSeconds)
            {
                _lastUnixTimeSeconds = now;
            }

            return;
        }

        var elapsedSeconds = now - _lastUnixTimeSeconds;
        _lastUnixTimeSeconds = now;

        if (_halted)
        {
            return;
        }

        Advance(elapsedSeconds);
        IsDirty = true;
    }

    private void Advance(long elapsedSeconds)
    {
        var totalSeconds = _seconds + elapsedSeconds;
        _seconds = (int)(totalSeconds % 60);

        var totalMinutes = _minutes + (totalSeconds / 60);
        _minutes = (int)(totalMinutes % 60);

        var totalHours = _hours + (totalMinutes / 60);
        _hours = (int)(totalHours % 24);

        AddDays(totalHours / 24);
    }

    private void AddDays(long days)
    {
        if (days == 0)
        {
            return;
        }

        var totalDays = _day + days;
        if (totalDays > MaxDay)
        {
            _carry = true;
        }

        _day = (int)(totalDays & MaxDay);
    }

    private static byte GetDayHigh(int day, bool halted, bool carry) =>
        (byte)(
            ((day >> 8) & DayHighDayBitMask)
            | (halted ? DayHighHaltMask : 0)
            | (carry ? DayHighCarryMask : 0)
        );

    private static void WriteRegisters(
        Span<byte> destination,
        int seconds,
        int minutes,
        int hours,
        int day,
        bool halted,
        bool carry
    )
    {
        destination[0] = (byte)seconds;
        destination[4] = (byte)minutes;
        destination[8] = (byte)hours;
        destination[12] = (byte)day;
        destination[16] = GetDayHigh(day, halted, carry);
    }

    private static void ReadRegisters(
        ReadOnlySpan<byte> source,
        out int seconds,
        out int minutes,
        out int hours,
        out int day,
        out bool halted,
        out bool carry
    )
    {
        seconds = source[0] & SecondsMask;
        minutes = source[4] & MinutesMask;
        hours = source[8] & HoursMask;
        day = source[12] | ((source[16] & DayHighDayBitMask) << 8);
        halted = (source[16] & DayHighHaltMask) != 0;
        carry = (source[16] & DayHighCarryMask) != 0;
    }
}
