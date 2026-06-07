using System.Collections.Concurrent;
using System.Diagnostics;
using FluentResults;
using GbcNet.Core;
using GbcNet.Core.Apu;
using GbcNet.Core.Joypad;
using GbcNet.Core.Ppu;
using GbcNet.Gui.Audio;

namespace GbcNet.Gui.Emulation;

/// <summary>
/// Runs a Game Boy instance on a background loop.
/// </summary>
internal sealed class EmulationSession
{
    private const int MachineCyclesPerSecond = 1_048_576;
    private const int MachineCyclesPerThrottle = 4096;
    private const int MachineCyclesPerSaveFlush = 5 * MachineCyclesPerSecond;
    private const int AudioDrainSampleCapacity = 512;

    private readonly Func<Result> _flushBatterySave;
    private readonly Action<Exception> _handleFault;
    private readonly Action<FrameCompletedEventArgs> _handleFrame;
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

    public EmulationSession(
        GameBoy gameBoy,
        IAudioOutput audioOutput,
        Action<FrameCompletedEventArgs> handleFrame,
        Action<Exception> handleFault,
        Func<Result> flushBatterySave
    )
    {
        _gameBoy = gameBoy;
        _audioOutput = audioOutput;
        _audioOutput.Clear();
        _handleFrame = handleFrame;
        _handleFault = handleFault;
        _flushBatterySave = flushBatterySave;
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

        bool enabledChanged =
            Interlocked.Exchange(ref _isFastForwardEnabled, enabled ? 1 : 0) != (enabled ? 1 : 0);

        bool speedChanged = Interlocked.Exchange(ref _fastForwardSpeed, (int)speed) != (int)speed;

        if (!enabledChanged && !speedChanged)
        {
            return;
        }

        // Queued audio produced for the old pacing mode
        // Dropped instead of playing stale sound
        _audioOutput.Clear();
        Interlocked.Increment(ref _pacingRevision);
    }

    private async Task RunAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        long elapsedMachineCycles = 0;
        long nextThrottleMachineCycles = MachineCyclesPerThrottle;
        long nextSaveMachineCycles = MachineCyclesPerSaveFlush;
        long pacingBaseMachineCycles = 0;
        TimeSpan pacingBaseElapsed = TimeSpan.Zero;
        int pacingRevision = Volatile.Read(ref _pacingRevision);

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

                while (
                    _pendingButtonStates.TryDequeue(
                        out (JoypadButton Button, bool Pressed) buttonState
                    )
                )
                {
                    _gameBoy.SetButtonState(buttonState.Button, buttonState.Pressed);
                }

                elapsedMachineCycles += _gameBoy.Step();

                DrainAudioSamples(enqueueOutput: !ShouldMuteAudio());

                if (elapsedMachineCycles >= nextSaveMachineCycles)
                {
                    FlushBatterySave();
                    nextSaveMachineCycles += MachineCyclesPerSaveFlush;
                }

                if (elapsedMachineCycles < nextThrottleMachineCycles)
                {
                    continue;
                }

                int currentPacingRevision = Volatile.Read(ref _pacingRevision);
                if (currentPacingRevision != pacingRevision)
                {
                    // Timing baseline restarted when speed changes to avoid a catch-up delay
                    pacingRevision = currentPacingRevision;
                    pacingBaseMachineCycles = elapsedMachineCycles;
                    pacingBaseElapsed = stopwatch.Elapsed;
                    nextThrottleMachineCycles = elapsedMachineCycles + MachineCyclesPerThrottle;
                }

                // Throttle against total emulated time instead of per-chunk delay to avoid drift.
                TimeSpan expectedElapsed =
                    pacingBaseElapsed
                    + TimeSpan.FromSeconds(
                        (elapsedMachineCycles - pacingBaseMachineCycles)
                            / (MachineCyclesPerSecond * GetSpeedMultiplier())
                    );
                TimeSpan delay = expectedElapsed - stopwatch.Elapsed;

                if (delay > TimeSpan.Zero)
                {
                    await Task.WhenAny(
                            Task.Delay(delay, CancellationToken.None),
                            _stopRequested.Task
                        )
                        .ConfigureAwait(false);
                }

                nextThrottleMachineCycles += MachineCyclesPerThrottle;
            }
        }
        catch (Exception exception)
            when (exception is NotSupportedException or InvalidOperationException)
        {
            _handleFault(exception);
        }
        finally
        {
            FlushBatterySave();
            _audioOutput.Clear();
        }
    }

    private double GetSpeedMultiplier() =>
        Volatile.Read(ref _isFastForwardEnabled) != 0
            ? Volatile.Read(ref _fastForwardSpeed) / (double)(int)EmulationSpeed.Normal
            : 1.0;

    private bool ShouldMuteAudio() =>
        Volatile.Read(ref _isFastForwardEnabled) != 0
        && Volatile.Read(ref _fastForwardSpeed) > (int)EmulationSpeed.Normal;

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

    private void FlushBatterySave()
    {
        Result result = _flushBatterySave();

        if (result.IsFailed)
        {
            _handleFault(
                new InvalidOperationException(
                    string.Join(Environment.NewLine, result.Errors.Select(error => error.Message))
                )
            );
        }
    }

    private void OnFrameCompleted(object? sender, FrameCompletedEventArgs e)
    {
        _handleFrame(e);
    }
}
