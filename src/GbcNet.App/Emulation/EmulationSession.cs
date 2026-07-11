// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.Concurrent;
using System.Diagnostics;
using GbcNet.App.Audio;
using GbcNet.App.Saves;
using GbcNet.Core;
using GbcNet.Core.Apu;
using GbcNet.Core.Hardware;
using GbcNet.Core.Joypad;
using GbcNet.Core.Ppu;

namespace GbcNet.App.Emulation;

/// <summary>
/// Runs a Game Boy instance on a background loop.
/// </summary>
internal sealed class EmulationSession
{
    private const int MachineCyclesPerSaveFlush = 5 * GameBoyTiming.NormalCpuHz;
    private const int AudioDrainSampleCapacity = 512;
    private static readonly long _fastForwardFrameIntervalTimestamp = Stopwatch.Frequency / 60;
    private static readonly TimeSpan _stoppedCpuSleepInterval = TimeSpan.FromMilliseconds(1);

    private readonly CartridgeBatterySaveWriter? _batterySaveWriter;
    private readonly Action<Exception> _handleFatalFault;
    private readonly Action<LcdFrame> _handleFrame;
    private readonly GameBoy _gameBoy;
    private readonly IAudioOutput _audioOutput;
    private readonly ApuStereoSample[] _audioSamples = new ApuStereoSample[
        AudioDrainSampleCapacity
    ];
    private readonly ConcurrentQueue<(JoypadButton Button, bool Pressed)> _pendingButtonStates =
        new();
    private readonly TaskCompletionSource _stopRequested = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    private readonly Task _runTask;
    private int _isPaused;
    private int _isStopped;
    private int _isFastForwardEnabled;
    private int _fastForwardSpeed = (int)EmulationSpeed.Two;
    private int _pacingRevision;
    private int _videoFrameRenderRequested = 1;
    private long _nextFastForwardFrameTimestamp;

    public bool IsPaused
    {
        get => Volatile.Read(ref _isPaused) != 0;
        set
        {
            Volatile.Write(ref _isPaused, value ? 1 : 0);

            if (value)
            {
                _audioOutput.Clear();
            }
        }
    }

    public HardwareModel HardwareModel => _gameBoy.HardwareModel;

    public EmulationSession(
        GameBoy gameBoy,
        IAudioOutput audioOutput,
        Action<LcdFrame> handleFrame,
        Action<Exception> handleFatalFault,
        CartridgeBatterySaveWriter? batterySaveWriter
    )
    {
        _gameBoy = gameBoy;
        _audioOutput = audioOutput;
        _audioOutput.Clear();
        _handleFrame = handleFrame;
        _handleFatalFault = handleFatalFault;
        _batterySaveWriter = batterySaveWriter;
        _gameBoy.FrameCompleted += OnFrameCompleted;
        _runTask = Task.Run(RunAsync, CancellationToken.None);
    }

    public async ValueTask StopAsync()
    {
        if (Interlocked.Exchange(ref _isStopped, 1) == 0)
        {
            _gameBoy.FrameCompleted -= OnFrameCompleted;
            _stopRequested.SetResult();
        }

        // Wait for the emulation loop to run its final save before the next session loads SRAM.
        await _runTask.ConfigureAwait(false);
    }

    public void SetButtonState(JoypadButton button, bool pressed)
    {
        if (Volatile.Read(ref _isStopped) == 0)
        {
            // Avalonia raises input on the UI thread while emulation runs on the session thread.
            _pendingButtonStates.Enqueue((button, pressed));
        }
    }

    public void SetFastForward(bool enabled, EmulationSpeed speed)
    {
        if (!Enum.IsDefined(speed))
        {
            throw new ArgumentOutOfRangeException(
                nameof(speed),
                speed,
                "Fast-forward speed must be one of the supported GUI multipliers."
            );
        }

        var enabledChanged =
            Interlocked.Exchange(ref _isFastForwardEnabled, enabled ? 1 : 0) != (enabled ? 1 : 0);

        var speedChanged = Interlocked.Exchange(ref _fastForwardSpeed, (int)speed) != (int)speed;

        if (!enabledChanged && !speedChanged)
        {
            return;
        }

        // Old pacing mode may have queued audio with obsolete timing.
        _audioOutput.Clear();
        Volatile.Write(ref _videoFrameRenderRequested, 1);
        Volatile.Write(ref _nextFastForwardFrameTimestamp, 0);
        Interlocked.Increment(ref _pacingRevision);
    }

    private async Task RunAsync()
    {
        var timestamp = Stopwatch.GetTimestamp();
        long elapsedMachineCycles = 0;
        long nextSaveMachineCycles = MachineCyclesPerSaveFlush;
        EmulationPacingState pacing = new(
            timestamp,
            elapsedMachineCycles,
            GetSpeedMultiplier(),
            _gameBoy.CpuMachineCyclesPerSecond,
            Volatile.Read(ref _pacingRevision)
        );

        Exception? fatalException = null;
        try
        {
            while (!_stopRequested.Task.IsCompleted)
            {
                if (IsPaused)
                {
                    await Task.WhenAny(
                            Task.Delay(TimeSpan.FromMilliseconds(16), CancellationToken.None),
                            _stopRequested.Task
                        )
                        .ConfigureAwait(false);
                    continue;
                }

                while (_pendingButtonStates.TryDequeue(out var buttonState))
                {
                    _gameBoy.SetButtonState(buttonState.Button, buttonState.Pressed);
                }

                _gameBoy.VideoRenderingEnabled = ShouldRenderVideoFrame();
                var stepMachineCycles = _gameBoy.Step();
                if (stepMachineCycles == 0)
                {
                    await Task.WhenAny(
                            Task.Delay(_stoppedCpuSleepInterval, CancellationToken.None),
                            _stopRequested.Task
                        )
                        .ConfigureAwait(false);
                    continue;
                }

                elapsedMachineCycles += stepMachineCycles;

                DrainAudioSamples(enqueueOutput: !ShouldMuteAudio());

                if (elapsedMachineCycles >= nextSaveMachineCycles)
                {
                    _batterySaveWriter?.QueueSave();
                    nextSaveMachineCycles += MachineCyclesPerSaveFlush;
                }

                if (!pacing.ShouldThrottle(elapsedMachineCycles))
                {
                    continue;
                }

                timestamp = Stopwatch.GetTimestamp();
                if (
                    pacing.ResetIfChanged(
                        timestamp,
                        elapsedMachineCycles,
                        GetSpeedMultiplier(),
                        _gameBoy.CpuMachineCyclesPerSecond,
                        Volatile.Read(ref _pacingRevision)
                    )
                )
                {
                    RequestFastForwardFrameIfDue(timestamp);
                    continue;
                }

                var delayTimestamp = pacing.GetDelayTimestamp(timestamp, elapsedMachineCycles);

                if (delayTimestamp > 0)
                {
                    await Task.WhenAny(
                            Task.Delay(
                                TimeSpan.FromTicks(
                                    delayTimestamp * TimeSpan.TicksPerSecond / Stopwatch.Frequency
                                ),
                                CancellationToken.None
                            ),
                            _stopRequested.Task
                        )
                        .ConfigureAwait(false);
                    timestamp = Stopwatch.GetTimestamp();
                }

                RequestFastForwardFrameIfDue(timestamp);
                pacing.ScheduleNextThrottle(elapsedMachineCycles);
            }
        }
        catch (Exception exception)
            when (exception is NotSupportedException or InvalidOperationException)
        {
            fatalException = exception;
        }
        finally
        {
            if (_batterySaveWriter is not null)
            {
                await _batterySaveWriter.FlushAsync().ConfigureAwait(false);
            }

            _audioOutput.Clear();
        }

        if (fatalException is not null)
        {
            _handleFatalFault(fatalException);
        }
    }

    private double GetSpeedMultiplier()
    {
        // Do not let fast-forward collapse the boot animation into its final white frame.
        if (_gameBoy.IsBootRomMapped || Volatile.Read(ref _isFastForwardEnabled) == 0)
        {
            return 1.0;
        }

        return Volatile.Read(ref _fastForwardSpeed) / (double)(int)EmulationSpeed.Normal;
    }

    private bool ShouldMuteAudio()
    {
        // Current audio output is real-time; fast-forward audio needs separate resampling.
        return !_gameBoy.IsBootRomMapped
            && Volatile.Read(ref _isFastForwardEnabled) != 0
            && Volatile.Read(ref _fastForwardSpeed) > (int)EmulationSpeed.Normal;
    }

    private void DrainAudioSamples(bool enqueueOutput)
    {
        int drained;

        do
        {
            drained = _gameBoy.DrainAudioSamples(_audioSamples);

            if (enqueueOutput && drained > 0)
            {
                _audioOutput.EnqueueSamples(_audioSamples.AsSpan(0, drained));
            }
        } while (drained == _audioSamples.Length);
    }

    private void OnFrameCompleted(LcdFrame frame)
    {
        if (Volatile.Read(ref _isFastForwardEnabled) != 0)
        {
            Volatile.Write(
                ref _nextFastForwardFrameTimestamp,
                Stopwatch.GetTimestamp() + _fastForwardFrameIntervalTimestamp
            );
        }

        Volatile.Write(ref _videoFrameRenderRequested, 0);
        _handleFrame(frame);
    }

    private void RequestFastForwardFrameIfDue(long timestamp)
    {
        if (
            Volatile.Read(ref _isFastForwardEnabled) == 0
            || Volatile.Read(ref _fastForwardSpeed) <= (int)EmulationSpeed.Normal
            || Volatile.Read(ref _videoFrameRenderRequested) != 0
        )
        {
            return;
        }

        if (timestamp >= Volatile.Read(ref _nextFastForwardFrameTimestamp))
        {
            Volatile.Write(ref _videoFrameRenderRequested, 1);
        }
    }

    private bool ShouldRenderVideoFrame() =>
        _gameBoy.IsBootRomMapped
        || Volatile.Read(ref _isFastForwardEnabled) == 0
        || Volatile.Read(ref _fastForwardSpeed) <= (int)EmulationSpeed.Normal
        || Volatile.Read(ref _videoFrameRenderRequested) != 0;
}
