using Microsoft.Extensions.Logging;

namespace GbcNet.App.Shell;

internal sealed class ShellOperationRunner(Action<Exception> handleError, ILogger logger)
{
    private readonly Lock _gate = new();
    private Task _tail = Task.CompletedTask;

    public void Run(Func<Task> action)
    {
        _ = RunAsync(action);
    }

    public Task RunAsync(Func<Task> action)
    {
        lock (_gate)
        {
            var operation = RunAfterAsync(_tail, action);
            _tail = ObserveFaults(operation);
            return operation;
        }
    }

    private static Task ObserveFaults(Task task) =>
        task.ContinueWith(
            static completed =>
            {
                if (completed.IsFaulted)
                {
                    _ = completed.Exception;
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
        );

    private async Task RunAfterAsync(Task previous, Func<Task> action)
    {
        await previous.ConfigureAwait(true);

        try
        {
            await action().ConfigureAwait(true);
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
}
