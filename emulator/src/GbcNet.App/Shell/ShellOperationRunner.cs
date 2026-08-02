// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Microsoft.Extensions.Logging;

namespace GbcNet.App.Shell;

/// <summary>
/// Runs shell-level UI operations one at a time and reports expected failures.
/// </summary>
internal sealed class ShellOperationRunner(Action<Exception> handleError, ILogger logger)
{
    private readonly Lock _gate = new();
    private Task _tail = Task.CompletedTask;

    /// <summary>
    /// Starts an operation without forcing event handlers to become async void.
    /// </summary>
    public void Run(Func<Task> action)
    {
        _ = RunAsync(action);
    }

    /// <summary>
    /// Queues an operation after the previous shell operation has completed.
    /// </summary>
    public Task RunAsync(Func<Task> action)
    {
        lock (_gate)
        {
            var operation = RunAfterAsync(_tail, action);
            _tail = ObserveFaults(operation);
            return operation;
        }
    }

    private Task ObserveFaults(Task task) =>
        task.ContinueWith(
            continuationAction: completed =>
            {
                if (completed.IsFaulted)
                {
                    ShellOperationRunnerLog.UnexpectedOperationFailed(logger, completed.Exception);
                }
            },
            cancellationToken: CancellationToken.None,
            continuationOptions: TaskContinuationOptions.ExecuteSynchronously,
            scheduler: TaskScheduler.Default
        );

    private async Task RunAfterAsync(Task previous, Func<Task> action)
    {
        await previous;

        try
        {
            await action();
        }
        catch (Exception exception) when (IsExpectedUiException(exception))
        {
            ShellOperationRunnerLog.OperationFailed(logger, exception);
            handleError(exception);
        }
    }

    private static bool IsExpectedUiException(Exception exception) =>
        exception
            is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or NotSupportedException
                or ArgumentException;
}

internal static partial class ShellOperationRunnerLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "UI operation failed.")]
    internal static partial void OperationFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "UI operation failed unexpectedly.")]
    internal static partial void UnexpectedOperationFailed(ILogger logger, Exception exception);
}
