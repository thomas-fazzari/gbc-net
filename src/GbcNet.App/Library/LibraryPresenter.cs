// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Platform.Storage;
using ErrorOr;
using GbcNet.App.Shell;
using Microsoft.Extensions.Logging;

namespace GbcNet.App.Library;

internal sealed class LibraryPresenter : IDisposable
{
    private static readonly TimeSpan _searchDebounceDelay = TimeSpan.FromMilliseconds(200);

    private readonly LibraryService _libraryService;
    private readonly LibraryView _view;
    private readonly IStorageProvider _storageProvider;
    private readonly ILogger<LibraryPresenter> _logger;
    private readonly ShellOperationRunner _operationRunner;
    private readonly LibrarySearch _search;
    private readonly Action<string> _showError;

    public LibraryPresenter(
        LibraryView view,
        LibraryService libraryService,
        ShellOperationRunner operationRunner,
        IStorageProvider storageProvider,
        ILogger<LibraryPresenter> logger,
        Func<string, Task> openRomAsync,
        Action<string> showError
    )
    {
        _libraryService = libraryService;
        _storageProvider = storageProvider;
        _view = view;
        _logger = logger;
        _operationRunner = operationRunner;
        _showError = showError;
        _search = new LibrarySearch(
            (query, cancellationToken) =>
                Task.Run(() => libraryService.GetRoms(query), cancellationToken),
            view.Load,
            _searchDebounceDelay
        );
        view.RomSelected = entry =>
            operationRunner.Run(async () =>
            {
                await openRomAsync(entry.LastKnownPath);
                Refresh();
            });
        view.SetCoverRequested = entry => operationRunner.Run(() => SetCoverAsync(entry));
        view.ClearCoverRequested = entry => operationRunner.Run(() => ClearCover(entry));
        view.RemoveRequested = entry => operationRunner.Run(() => RemoveRomAsync(entry));
        view.QueryChanged = () => Refresh(debounce: true);
    }

    private async Task SetCoverAsync(LibraryEntry entry)
    {
        var files = await _storageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Set ROM Cover",
                AllowMultiple = false,
                FileTypeFilter = [FilePickerFileTypes.ImageAll],
            }
        );

        if (files.Count == 0)
        {
            return;
        }

        if (!files[0].Path.IsFile)
        {
            _showError("Cover image must be a local file.");
            return;
        }

        var result = _libraryService.AssignCoverImage(entry.RomHash, files[0].Path.LocalPath);
        if (result.IsError)
        {
            ShowExpectedError(result.FirstError);
            return;
        }

        Refresh();
    }

    private void ClearCover(LibraryEntry entry)
    {
        var result = _libraryService.ClearCover(entry.RomHash);
        if (result.IsError)
        {
            ShowExpectedError(result.FirstError);
            return;
        }

        Refresh();
    }

    private async Task RemoveRomAsync(LibraryEntry entry)
    {
        if (!await _view.ConfirmRemoveAsync())
        {
            return;
        }

        _libraryService.RemoveRomPath(entry.LastKnownPath);
        Refresh();
    }

    public void Refresh() => Refresh(debounce: false);

    public void Dispose()
    {
        _view.QueryChanged = null;
        _search.Dispose();
    }

    private void Refresh(bool debounce)
    {
        var refresh = RefreshAsync(_view.Query, debounce);
        _operationRunner.Run(() => refresh);
    }

    private async Task RefreshAsync(LibraryQuery query, bool debounce)
    {
        try
        {
            await _search.RefreshAsync(query, debounce);
        }
        catch (InvalidOperationException exception)
        {
            LibraryPresenterLog.RefreshFailed(_logger, exception);
            _showError(exception.Message);
        }
    }

    private void ShowExpectedError(Error error) => _showError(error.Description);
}

internal sealed class LibrarySearch(
    Func<LibraryQuery, CancellationToken, Task<IReadOnlyList<LibraryEntry>>> searchAsync,
    Action<IReadOnlyList<LibraryEntry>> load,
    TimeSpan debounceDelay
) : IDisposable
{
    private CancellationTokenSource? _cancellation;
    private bool _disposed;

    public async Task RefreshAsync(LibraryQuery query, bool debounce)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var cancellation = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _cancellation, cancellation);
        if (previous is not null)
        {
            await previous.CancelAsync();
        }

        await RunAsync(query, debounce, cancellation);
    }

    public void Dispose()
    {
        _disposed = true;
        Interlocked.Exchange(ref _cancellation, null)?.Cancel();
    }

    private async Task RunAsync(
        LibraryQuery query,
        bool debounce,
        CancellationTokenSource cancellation
    )
    {
        try
        {
            if (debounce)
            {
                await Task.Delay(debounceDelay, cancellation.Token);
            }

            var entries = await searchAsync(query, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            load(entries);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // A newer query or presenter disposal canceled this refresh.
        }
        finally
        {
            Interlocked.CompareExchange(ref _cancellation, null, cancellation);
            cancellation.Dispose();
        }
    }
}

internal static partial class LibraryPresenterLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "ROM library refresh failed.")]
    internal static partial void RefreshFailed(ILogger logger, Exception exception);
}
