using GbcNet.App.Chrome;
using Microsoft.Extensions.Logging.Abstractions;

namespace GbcNet.Tests.App.Chrome;

public sealed class ShellOperationRunnerTests
{
    [Fact]
    public async Task RunAsync_ReportsExpectedUiException()
    {
        var reportedMessage = string.Empty;

        ShellOperationRunner runner = new(
            exception => reportedMessage = exception.Message,
            NullLogger<ShellOperationRunner>.Instance
        );

        await runner.RunAsync(() => throw new IOException("no access"));

        Assert.Equal("no access", reportedMessage);
    }

    [Fact]
    public async Task RunAsync_SerializesOperations()
    {
        var activeCount = 0;
        var maxActiveCount = 0;

        ShellOperationRunner runner = new(_ => { }, NullLogger<ShellOperationRunner>.Instance);

        await Task.WhenAll(
            runner.RunAsync(RunTrackedOperation),
            runner.RunAsync(RunTrackedOperation)
        );

        Assert.Equal(1, maxActiveCount);
        return;

        async Task RunTrackedOperation()
        {
            var current = Interlocked.Increment(ref activeCount);
            maxActiveCount = Math.Max(maxActiveCount, current);
            await Task.Delay(10, CancellationToken.None).ConfigureAwait(false);
            Interlocked.Decrement(ref activeCount);
        }
    }

    [Fact]
    public async Task RunAsync_ReleasesGateAfterUnexpectedException()
    {
        var nextOperationRan = false;
        ShellOperationRunner runner = new(_ => { }, NullLogger<ShellOperationRunner>.Instance);

        await Assert.ThrowsAsync<TimeoutException>(() =>
            runner.RunAsync(() => throw new TimeoutException("boom"))
        );
        await runner.RunAsync(() =>
        {
            nextOperationRan = true;
            return Task.CompletedTask;
        });

        Assert.True(nextOperationRan);
    }
}
