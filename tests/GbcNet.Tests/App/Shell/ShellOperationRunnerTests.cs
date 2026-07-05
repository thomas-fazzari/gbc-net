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

        ShellOperationRunner runner = new(_ => { }, NullLogger<ShellOperationRunner>.Instance);

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
