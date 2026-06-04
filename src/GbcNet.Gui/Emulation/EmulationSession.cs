using System.Diagnostics;
using GbcNet.Core;
using GbcNet.Core.Ppu;

namespace GbcNet.Gui.Emulation;

/// <summary>
/// Runs a Game Boy instance on a background loop.
/// </summary>
internal sealed class EmulationSession : IDisposable
{
    private const int MachineCyclesPerSecond = 1_048_576;
    private const int MachineCyclesPerThrottle = 4096;

    private readonly Action<Exception> _handleFault;
    private readonly Action<FrameCompletedEventArgs> _handleFrame;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly GameBoy _gameBoy;
    private readonly Task _runTask;

    public EmulationSession(
        GameBoy gameBoy,
        Action<FrameCompletedEventArgs> handleFrame,
        Action<Exception> handleFault
    )
    {
        _gameBoy = gameBoy;
        _handleFrame = handleFrame;
        _handleFault = handleFault;
        _gameBoy.FrameCompleted += OnFrameCompleted;
        _runTask = RunAsync(_cancellationTokenSource.Token);
    }

    public void Dispose()
    {
        _gameBoy.FrameCompleted -= OnFrameCompleted;
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();

        if (_runTask.IsCompleted)
        {
            _runTask.Dispose();
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        int elapsedMachineCycles = 0;
        int nextThrottleMachineCycles = MachineCyclesPerThrottle;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                elapsedMachineCycles += _gameBoy.Step();

                if (elapsedMachineCycles < nextThrottleMachineCycles)
                {
                    continue;
                }

                await ThrottleAsync(stopwatch, elapsedMachineCycles, cancellationToken)
                    .ConfigureAwait(false);
                nextThrottleMachineCycles += MachineCyclesPerThrottle;
            }
        }
        catch (Exception exception)
            when (exception is NotSupportedException or InvalidOperationException)
        {
            _handleFault(exception);
        }
    }

    private static async Task ThrottleAsync(
        Stopwatch stopwatch,
        int elapsedMachineCycles,
        CancellationToken cancellationToken
    )
    {
        var expectedElapsed = TimeSpan.FromSeconds(
            elapsedMachineCycles / (double)MachineCyclesPerSecond
        );

        TimeSpan delay = expectedElapsed - stopwatch.Elapsed;

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private void OnFrameCompleted(object? sender, FrameCompletedEventArgs e)
    {
        _handleFrame(e);
    }
}
