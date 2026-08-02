// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Shell;
using Microsoft.Extensions.Logging.Abstractions;

namespace GbcNet.Tests.Unit.App.Shell;

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

        reportedMessage.Should().Be("no access");
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
            exception => exception.Should().BeNull($"Unexpected error: {exception}"),
            NullLogger<ShellOperationRunner>.Instance
        );

        var firstOperation = runner.RunAsync(async () =>
        {
            TrackOperationStart();
            firstOperationStarted.SetResult();
            await releaseFirstOperation.Task;
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

        secondOperationStarted.Task.IsCompleted.Should().BeFalse();
        releaseFirstOperation.SetResult();

        await Task.WhenAll(firstOperation, secondOperation);

        maxActiveCount.Should().Be(1);
        secondOperationStarted.Task.IsCompletedSuccessfully.Should().BeTrue();
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
            exception => exception.Should().BeNull($"Unexpected handled error: {exception}"),
            NullLogger<ShellOperationRunner>.Instance
        );

        await FluentActions
            .Awaiting(() => runner.RunAsync(() => throw new TimeoutException("boom")))
            .Should()
            .ThrowExactlyAsync<TimeoutException>();
        await runner.RunAsync(() =>
        {
            nextOperationRan = true;
            return Task.CompletedTask;
        });

        nextOperationRan.Should().BeTrue();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "ReSharper",
        "MethodHasAsyncOverload",
        Justification = "Exercises the synchronous fire-and-forget wrapper."
    )]
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

        errorReported.Task.IsCompleted.Should().BeFalse();
        thirdOperationCompleted.Task.IsCompleted.Should().BeFalse();
        releaseFirstOperation.SetResult();

        (
            await errorReported.Task.WaitAsync(
                TimeSpan.FromSeconds(1),
                TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be("no access");
        await thirdOperationCompleted.Task.WaitAsync(
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken
        );
        events.Should().Equal("first-start", "first-end", "error", "third");
    }
}
