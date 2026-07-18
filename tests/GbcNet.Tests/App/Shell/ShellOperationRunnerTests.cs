// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Shell;
using Microsoft.Extensions.Logging.Abstractions;

namespace GbcNet.Tests.App.Shell;

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
        var releaseFirstOperation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var firstOperationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var secondOperationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var activeCount = 0;
        var maxActiveCount = 0;
        ShellOperationRunner runner = new(
            exception => Assert.Fail($"Unexpected error: {exception}"),
            NullLogger<ShellOperationRunner>.Instance
        );

        var firstOperation = runner.RunAsync(async () =>
        {
            TrackOperationStart();
            firstOperationStarted.SetResult();
            await releaseFirstOperation.Task.ConfigureAwait(false);
            Interlocked.Decrement(ref activeCount);
        });

        await firstOperationStarted.Task;

        var secondOperation = runner.RunAsync(() =>
        {
            TrackOperationStart();
            secondOperationStarted.SetResult();
            Interlocked.Decrement(ref activeCount);
            return Task.CompletedTask;
        });

        Assert.False(secondOperationStarted.Task.IsCompleted);
        releaseFirstOperation.SetResult();

        await Task.WhenAll(firstOperation, secondOperation);

        Assert.Equal(1, maxActiveCount);
        Assert.True(secondOperationStarted.Task.IsCompletedSuccessfully);
        return;

        void TrackOperationStart()
        {
            var current = Interlocked.Increment(ref activeCount);
            maxActiveCount = Math.Max(maxActiveCount, current);
        }
    }

    [Fact]
    public async Task RunAsync_ReleasesGateAfterUnexpectedException()
    {
        var nextOperationRan = false;
        ShellOperationRunner runner = new(
            exception => Assert.Fail($"Unexpected handled error: {exception}"),
            NullLogger<ShellOperationRunner>.Instance
        );

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

    [Fact]
    public async Task Run_QueuesFireAndForgetOperationsAndReportsExpectedError()
    {
        var releaseFirstOperation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var firstOperationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var errorReported = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var thirdOperationCompleted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var events = new List<string>();
        ShellOperationRunner runner = new(
            exception =>
            {
                events.Add("error");
                errorReported.SetResult(exception.Message);
            },
            NullLogger<ShellOperationRunner>.Instance
        );

        // Intentionally exercise the synchronous fire-and-forget wrapper.
#pragma warning disable CA1849, S6966
        runner.Run(async () =>
        {
            events.Add("first-start");
            firstOperationStarted.SetResult();
            await releaseFirstOperation.Task;
            events.Add("first-end");
        });

        await firstOperationStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken
        );
        runner.Run(() => throw new IOException("no access"));
        runner.Run(() =>
        {
            events.Add("third");
            thirdOperationCompleted.SetResult();
            return Task.CompletedTask;
        });
#pragma warning restore CA1849, S6966

        Assert.False(errorReported.Task.IsCompleted);
        Assert.False(thirdOperationCompleted.Task.IsCompleted);
        releaseFirstOperation.SetResult();

        Assert.Equal(
            "no access",
            await errorReported.Task.WaitAsync(
                TimeSpan.FromSeconds(1),
                TestContext.Current.CancellationToken
            )
        );
        await thirdOperationCompleted.Task.WaitAsync(
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken
        );
        Assert.Equal(["first-start", "first-end", "error", "third"], events);
    }
}
