using System.Collections.Concurrent;
using System.Diagnostics;
using FluentResults;
using GbcNet.Core;
using GbcNet.Core.Joypad;
using GbcNet.Core.Ppu;

namespace GbcNet.Gui.Emulation;

/// <summary>
/// Runs a Game Boy instance on a background loop.
/// </summary>
internal sealed class EmulationSession : IDisposable
{
    private const int MachineCyclesPerSecond = 1_048_576;
    private const int MachineCyclesPerThrottle = 4096;
    private const int MachineCyclesPerSaveFlush = 5 * MachineCyclesPerSecond;

    private readonly Func<Result> _flushBatterySave;
    private readonly Action<Exception> _handleFault;
    private readonly Action<FrameCompletedEventArgs> _handleFrame;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly GameBoy _gameBoy;
    private readonly ConcurrentQueue<(JoypadButton Button, bool Pressed)> _pendingButtonStates =
        new();
    private readonly Task _runTask;
    private int _isPaused;
    private int _isDisposed;

    public bool IsPaused
    {
        get => Volatile.Read(ref _isPaused) != 0;
        set => Volatile.Write(ref _isPaused, value ? 1 : 0);
    }

    public EmulationSession(
        GameBoy gameBoy,
        Action<FrameCompletedEventArgs> handleFrame,
        Action<Exception> handleFault,
        Func<Result> flushBatterySave
    )
    {
        _gameBoy = gameBoy;
        _handleFrame = handleFrame;
        _handleFault = handleFault;
        _flushBatterySave = flushBatterySave;
        _gameBoy.FrameCompleted += OnFrameCompleted;
        _runTask = RunAsync(_cancellationTokenSource.Token);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        _gameBoy.FrameCompleted -= OnFrameCompleted;
        _cancellationTokenSource.Cancel();

        // Wait for the emulation loop to run its final save before the next session loads SRAM.
        _runTask.GetAwaiter().GetResult();
        _cancellationTokenSource.Dispose();
    }

    public void SetButtonState(JoypadButton button, bool pressed)
    {
        if (Volatile.Read(ref _isDisposed) == 0)
        {
            // Avalonia raises input on the UI thread while emulation runs on the session thread.
            _pendingButtonStates.Enqueue((button, pressed));
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        long elapsedMachineCycles = 0;
        long nextThrottleMachineCycles = MachineCyclesPerThrottle;
        long nextSaveMachineCycles = MachineCyclesPerSaveFlush;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (IsPaused)
                {
                    await Task.Delay(16, CancellationToken.None).ConfigureAwait(false);
                    continue;
                }

                ApplyPendingButtonStates();
                elapsedMachineCycles += _gameBoy.Step();

                if (elapsedMachineCycles >= nextSaveMachineCycles)
                {
                    FlushBatterySave();
                    nextSaveMachineCycles += MachineCyclesPerSaveFlush;
                }

                if (elapsedMachineCycles < nextThrottleMachineCycles)
                {
                    continue;
                }

                await ThrottleAsync(stopwatch, elapsedMachineCycles).ConfigureAwait(false);
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
        }
    }

    private void ApplyPendingButtonStates()
    {
        while (_pendingButtonStates.TryDequeue(out (JoypadButton Button, bool Pressed) buttonState))
        {
            _gameBoy.SetButtonState(buttonState.Button, buttonState.Pressed);
        }
    }

    private static async Task ThrottleAsync(Stopwatch stopwatch, long elapsedMachineCycles)
    {
        // Throttle against total emulated time instead of per-chunk delay to avoid drift.
        var expectedElapsed = TimeSpan.FromSeconds(
            elapsedMachineCycles / (double)MachineCyclesPerSecond
        );

        TimeSpan delay = expectedElapsed - stopwatch.Elapsed;

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, CancellationToken.None).ConfigureAwait(false);
        }
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
