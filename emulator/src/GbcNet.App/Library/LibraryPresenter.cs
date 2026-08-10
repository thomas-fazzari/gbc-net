// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Platform.Storage;
using ErrorOr;
using GbcNet.App.Shell;
using Microsoft.Extensions.Logging;

namespace GbcNet.App.Library;

internal sealed class LibraryPresenter
{
    private readonly LibraryService _libraryService;
    private readonly LibraryView _view;
    private readonly IStorageProvider _storageProvider;
    private readonly ILogger<LibraryPresenter> _logger;

    public LibraryPresenter(
        LibraryView view,
        LibraryService libraryService,
        ShellOperationRunner operationRunner,
        IStorageProvider storageProvider,
        ILogger<LibraryPresenter> logger,
        Func<string, Task> openRomAsync
    )
    {
        _libraryService = libraryService;
        _storageProvider = storageProvider;
        _view = view;
        _logger = logger;
        view.RomSelected = entry =>
            operationRunner.Run(async () =>
            {
                await openRomAsync(entry.LastKnownPath);
                Refresh();
            });
        view.SetCoverRequested = entry => operationRunner.Run(() => SetCoverAsync(entry));
        view.ClearCoverRequested = entry => operationRunner.Run(() => ClearCoverAsync(entry));
        view.RemoveRequested = entry => operationRunner.Run(() => RemoveRomAsync(entry));
        view.QueryChanged = Refresh;
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
            _view.ShowError("Cover image must be a local file.");
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

    private Task ClearCoverAsync(LibraryEntry entry)
    {
        var result = _libraryService.ClearCover(entry.RomHash);
        if (result.IsError)
        {
            ShowExpectedError(result.FirstError);
            return Task.CompletedTask;
        }

        Refresh();
        return Task.CompletedTask;
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

    public void Refresh()
    {
        try
        {
            _view.Load(_libraryService.GetRoms(_view.Query));
        }
        catch (InvalidOperationException exception)
        {
            LibraryPresenterLog.RefreshFailed(_logger, exception);
            _view.ShowError(exception.Message);
        }
    }

    private void ShowExpectedError(Error error) => _view.ShowError(error.Description);
}

internal static partial class LibraryPresenterLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "ROM library refresh failed.")]
    internal static partial void RefreshFailed(ILogger logger, Exception exception);
}
