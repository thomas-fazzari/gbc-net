// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Platform.Storage;
using GbcNet.App.Shell;

namespace GbcNet.App.Library;

internal sealed class LibraryPresenter
{
    private readonly LibraryService _libraryService;
    private readonly LibraryView _view;
    private readonly IStorageProvider _storageProvider;

    public LibraryPresenter(
        LibraryView view,
        LibraryService libraryService,
        ShellOperationRunner operationRunner,
        IStorageProvider storageProvider,
        Func<string, Task> openRomAsync
    )
    {
        _libraryService = libraryService;
        _storageProvider = storageProvider;
        _view = view;
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
            throw new NotSupportedException("Cover image must be a local file.");
        }

        _libraryService.AssignCoverImage(entry.RomHash, files[0].Path.LocalPath);
        Refresh();
    }

    private Task ClearCoverAsync(LibraryEntry entry)
    {
        _libraryService.ClearCover(entry.RomHash);
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
            _view.ShowError(exception.Message);
        }
    }
}
