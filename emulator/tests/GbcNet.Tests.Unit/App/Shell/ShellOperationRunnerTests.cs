// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Shell;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GbcNet.Tests.Unit.App.Shell;

public sealed class ShellOperationRunnerTests
{
    [Fact]
    public async Task RunAsync_ReportsExpectedUiException()
    {
        var expectedException = new IOException("Synthetic I/O failure.");
        Exception? reportedException = null;
        var logger = new RecordingLogger();
        ShellOperationRunner runner = new(exception => reportedException = exception, logger);

        await runner.RunAsync(() => throw expectedException);

        reportedException.Should().BeSameAs(expectedException);
        var logEntry = logger.Entries.Should().ContainSingle().Which;
        logEntry.Level.Should().Be(LogLevel.Warning);
        logEntry.Exception.Should().BeSameAs(expectedException);
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
        var expectedException = CreateArgumentException("invalid");
        var logger = new RecordingLogger();
        ShellOperationRunner runner = new(
            exception => exception.Should().BeNull($"Unexpected handled error: {exception}"),
            logger
        );

        var exception = (
            await FluentActions
                .Awaiting(() => runner.RunAsync(() => throw expectedException))
                .Should()
                .ThrowExactlyAsync<ArgumentException>()
        ).Which;
        exception.Should().BeSameAs(expectedException);
        var logEntry = logger.Entries.Should().ContainSingle().Which;
        logEntry.Level.Should().Be(LogLevel.Error);
        logEntry.Exception.Should().BeSameAs(expectedException);
        await runner.RunAsync(() =>
        {
            nextOperationRan = true;
            return Task.CompletedTask;
        });

        nextOperationRan.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_DoesNotHandleOrLogCancellation()
    {
        var cancellation = new OperationCanceledException();
        var logger = new RecordingLogger();
        ShellOperationRunner runner = new(
            exception => exception.Should().BeNull($"Unexpected handled error: {exception}"),
            logger
        );

        var exception = (
            await FluentActions
                .Awaiting(() => runner.RunAsync(() => throw cancellation))
                .Should()
                .ThrowExactlyAsync<OperationCanceledException>()
        ).Which;

        exception.Should().BeSameAs(cancellation);
        logger.Entries.Should().BeEmpty();
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
        var expectedException = new IOException("Synthetic I/O failure.");
        var errorReported = new TaskCompletionSource<Exception>(
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
                errorReported.SetResult(exception);
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
        runner.Run(() => throw expectedException);
        runner.Run(() =>
        {
            events.Add("third");
            thirdOperationCompleted.SetResult();
        });
#pragma warning restore CA1849, S6966

        errorReported.Task.IsCompleted.Should().BeFalse();
        thirdOperationCompleted.Task.IsCompleted.Should().BeFalse();
        releaseFirstOperation.SetResult();

        var reportedException = await errorReported.Task.WaitAsync(
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken
        );
        reportedException.Should().BeSameAs(expectedException);
        await thirdOperationCompleted.Task.WaitAsync(
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken
        );
        events.Should().Equal("first-start", "first-end", "error", "third");
    }

    private readonly record struct LogEntry(LogLevel Level, Exception? Exception);

    private static ArgumentException CreateArgumentException(string value) =>
        new($"Synthetic application failure: {value}", nameof(value));

    private sealed class RecordingLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => Entries.Add(new LogEntry(logLevel, exception));
    }
}
