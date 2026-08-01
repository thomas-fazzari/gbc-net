// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

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

    private RtcRegisterSet _live;
    private RtcRegisterSet _latched;
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
        _latched = _live;
        IsDirty = true;
    }

    /// <summary>
    /// Reads a latched RTC register selected by MBC3 register values 08-0C.
    /// </summary>
    public byte ReadRegister(byte register) =>
        register switch
        {
            SecondsRegister => (byte)_latched.Seconds,
            MinutesRegister => (byte)_latched.Minutes,
            HoursRegister => (byte)_latched.Hours,
            DayLowRegister => (byte)_latched.Day,
            DayHighRegister => GetDayHigh(_latched),
            _ => 0xFF,
        };

    /// <summary>
    /// Writes a live RTC register selected by MBC3 register values 08-0C.
    /// </summary>
    public void WriteRegister(byte register, byte value)
    {
        UpdateToNow();

        _live = register switch
        {
            SecondsRegister => _live with { Seconds = value & SecondsMask },
            MinutesRegister => _live with { Minutes = value & MinutesMask },
            HoursRegister => _live with { Hours = value & HoursMask },
            DayLowRegister => _live with { Day = (_live.Day & 0x100) | value },
            DayHighRegister => _live with
            {
                Day = (_live.Day & 0xFF) | ((value & DayHighDayBitMask) << 8),
                Halted = (value & DayHighHaltMask) != 0,
                Carry = (value & DayHighCarryMask) != 0,
            },
            _ => _live,
        };

        if (register is < SecondsRegister or > DayHighRegister)
        {
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

        WriteRegisters(data.AsSpan(RealRtcStateOffset, RtcTimeStateSize), _live);
        WriteRegisters(data.AsSpan(LatchedRtcStateOffset, RtcTimeStateSize), _latched);
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

        _live = ReadRegisters(data.Slice(RealRtcStateOffset, RtcTimeStateSize));
        _latched = ReadRegisters(data.Slice(LatchedRtcStateOffset, RtcTimeStateSize));
        _lastUnixTimeSeconds = BinaryPrimitives.ReadInt64LittleEndian(
            data.Slice(LastUnixTimeSecondsOffset, sizeof(long))
        );
        IsDirty = false;
        UpdateToNow();
    }

    /// <summary>
    /// Captures the RTC without changing its clock anchor or persistence state.
    /// </summary>
    internal Mbc3RealTimeClockState CaptureState()
    {
        var now = getUnixTimeSeconds();
        var live = _live;
        var advanced = now > _lastUnixTimeSeconds && !live.Halted;

        if (advanced)
        {
            Advance(now - _lastUnixTimeSeconds, ref live);
        }

        return new(live, _latched, IsDirty || advanced);
    }

    /// <summary>
    /// Validates an RTC save state without observing the injected clock.
    /// </summary>
    internal static void ValidateState(Mbc3RealTimeClockState state)
    {
        if (!IsValid(state.Live) || !IsValid(state.Latched))
        {
            throw new ArgumentException("RTC register value is out of range.", nameof(state));
        }
    }

    /// <summary>
    /// Restores the RTC at the destination clock's current time without catch-up.
    /// </summary>
    internal void RestoreState(Mbc3RealTimeClockState state)
    {
        ValidateState(state);
        var now = getUnixTimeSeconds();

        _live = state.Live;
        _latched = state.Latched;
        _lastUnixTimeSeconds = now;
        IsDirty = state.IsDirty;
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

        if (_live.Halted)
        {
            return;
        }

        Advance(elapsedSeconds, ref _live);
        IsDirty = true;
    }

    private static void Advance(long elapsedSeconds, ref RtcRegisterSet registers)
    {
        var totalSeconds = registers.Seconds + elapsedSeconds;
        var seconds = (int)(totalSeconds % 60);

        var totalMinutes = registers.Minutes + (totalSeconds / 60);
        var minutes = (int)(totalMinutes % 60);

        var totalHours = registers.Hours + (totalMinutes / 60);
        var hours = (int)(totalHours % 24);

        var day = registers.Day;
        var carry = registers.Carry;
        AddDays(totalHours / 24, ref day, ref carry);
        registers = registers with
        {
            Seconds = seconds,
            Minutes = minutes,
            Hours = hours,
            Day = day,
            Carry = carry,
        };
    }

    private static void AddDays(long days, ref int day, ref bool carry)
    {
        if (days == 0)
        {
            return;
        }

        var totalDays = day + days;
        if (totalDays > MaxDay)
        {
            carry = true;
        }

        day = (int)(totalDays & MaxDay);
    }

    private static bool IsValid(RtcRegisterSet registers) =>
        (uint)registers.Seconds <= SecondsMask
        && (uint)registers.Minutes <= MinutesMask
        && (uint)registers.Hours <= HoursMask
        && (uint)registers.Day <= MaxDay;

    private static byte GetDayHigh(RtcRegisterSet registers) =>
        (byte)(
            ((registers.Day >> 8) & DayHighDayBitMask)
            | (registers.Halted ? DayHighHaltMask : 0)
            | (registers.Carry ? DayHighCarryMask : 0)
        );

    private static void WriteRegisters(Span<byte> destination, RtcRegisterSet registers)
    {
        destination[0] = (byte)registers.Seconds;
        destination[4] = (byte)registers.Minutes;
        destination[8] = (byte)registers.Hours;
        destination[12] = (byte)registers.Day;
        destination[16] = GetDayHigh(registers);
    }

    private static RtcRegisterSet ReadRegisters(ReadOnlySpan<byte> source) =>
        new(
            source[0] & SecondsMask,
            source[4] & MinutesMask,
            source[8] & HoursMask,
            source[12] | ((source[16] & DayHighDayBitMask) << 8),
            (source[16] & DayHighHaltMask) != 0,
            (source[16] & DayHighCarryMask) != 0
        );
}

internal readonly record struct RtcRegisterSet(
    int Seconds,
    int Minutes,
    int Hours,
    int Day,
    bool Halted,
    bool Carry
);

internal readonly record struct Mbc3RealTimeClockState(
    RtcRegisterSet Live,
    RtcRegisterSet Latched,
    bool IsDirty
);
