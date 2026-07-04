// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;

namespace GbcNet.App.Emulation;

/// <summary>
/// Tracks host pacing against elapsed emulated CPU machine cycles.
/// </summary>
internal sealed class EmulationPacingState
{
    private const int ThrottleIntervalMilliseconds = 8;
    private const double ThrottleIntervalSeconds = ThrottleIntervalMilliseconds / 1000.0;

    public EmulationPacingState(
        long timestamp,
        long elapsedMachineCycles,
        double speedMultiplier,
        int cpuHz,
        int revision
    )
    {
        Reset(timestamp, elapsedMachineCycles, speedMultiplier, cpuHz, revision);
    }

    public double SpeedMultiplier { get; private set; }

    public long NextThrottleMachineCycles { get; private set; }

    private long BaseTimestamp { get; set; }

    private long BaseMachineCycles { get; set; }

    private int CpuHz { get; set; }

    private int Revision { get; set; }

    public bool ShouldThrottle(long elapsedMachineCycles) =>
        elapsedMachineCycles >= NextThrottleMachineCycles;

    public bool ResetIfChanged(
        long timestamp,
        long elapsedMachineCycles,
        double speedMultiplier,
        int cpuHz,
        int revision
    )
    {
        if (revision == Revision && cpuHz == CpuHz && speedMultiplier.Equals(SpeedMultiplier))
        {
            return false;
        }

        Reset(timestamp, elapsedMachineCycles, speedMultiplier, cpuHz, revision);
        return true;
    }

    public long GetDelayTimestamp(long timestamp, long elapsedMachineCycles) =>
        GetExpectedTimestamp(elapsedMachineCycles) - timestamp;

    public void ScheduleNextThrottle(long elapsedMachineCycles)
    {
        NextThrottleMachineCycles =
            elapsedMachineCycles + GetThrottleMachineCycles(CpuHz, SpeedMultiplier);
    }

    private void Reset(
        long timestamp,
        long elapsedMachineCycles,
        double speedMultiplier,
        int cpuHz,
        int revision
    )
    {
        BaseTimestamp = timestamp;
        BaseMachineCycles = elapsedMachineCycles;
        SpeedMultiplier = speedMultiplier;
        CpuHz = cpuHz;
        Revision = revision;
        ScheduleNextThrottle(elapsedMachineCycles);
    }

    private long GetExpectedTimestamp(long elapsedMachineCycles) =>
        BaseTimestamp
        + (long)
            Math.Round(
                (elapsedMachineCycles - BaseMachineCycles)
                    * (Stopwatch.Frequency / (CpuHz * SpeedMultiplier)),
                MidpointRounding.ToEven
            );

    private static long GetThrottleMachineCycles(int cpuHz, double speedMultiplier) =>
        Math.Max(
            1,
            (long)
                Math.Round(
                    cpuHz * speedMultiplier * ThrottleIntervalSeconds,
                    MidpointRounding.ToEven
                )
        );
}
