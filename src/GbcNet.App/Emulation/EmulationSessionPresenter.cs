// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using GbcNet.App.Input;
using GbcNet.App.Library;
using GbcNet.App.Menus;
using GbcNet.App.Shell;
using GbcNet.App.Shell.Chrome;
using GbcNet.Core;

namespace GbcNet.App.Emulation;

internal sealed class EmulationSessionPresenter(
    EmulationController controller,
    InputRouter inputRouter,
    LibraryService libraryService,
    StatusBarPresenter statusBar,
    MainMenu menu,
    ShellOperationRunner operationRunner
)
{
    private const int RecentRomLimit = 5;
    private string? _loadedRomCoverPath;

    private static readonly FilePickerFileType _gameBoyRomFileType = new("Game Boy ROM")
    {
        Patterns = ["*.gb", "*.gbc", "*.sgb"],
        AppleUniformTypeIdentifiers = ["public.data"],
        MimeTypes = ["application/x-gameboy-rom", "application/x-gameboy-color-rom"],
    };

    public event EventHandler? SessionClosed;
    public event EventHandler? SessionFaulted;
    public event EventHandler? SessionOpened;

    public void AttachDragDrop(Control target)
    {
        DragDrop.SetAllowDrop(target, value: true);
        DragDrop.AddDragOverHandler(target, DragDrop_OnDragOver);
        DragDrop.AddDropHandler(target, DragDrop_OnDrop);
    }

    public void SetBootRomOptions(BootRomOptions options)
    {
        controller.SetBootRomOptions(options);
    }

    public async Task OpenRomAsync(IStorageProvider storageProvider)
    {
        var files = await storageProvider
            .OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "Open Game Boy ROM",
                    AllowMultiple = false,
                    FileTypeFilter = [_gameBoyRomFileType],
                }
            )
            .ConfigureAwait(true);

        if (files.Count > 0)
        {
            await OpenRomFileAsync(files[0]).ConfigureAwait(true);
        }
    }

    public async Task OpenRomFileAsync(IStorageFile file)
    {
        inputRouter.Clear();
        var state = await controller.OpenRomFileAsync(file).ConfigureAwait(true);

        _loadedRomCoverPath = null;
        ApplyRomActionResult(state);

        if (file.Path.IsFile && state.LoadedCartridgeHeader is { } cartridgeHeader)
        {
            try
            {
                _loadedRomCoverPath = libraryService.RecordLoadedRom(
                    file.Path.LocalPath,
                    state.LoadedRom,
                    cartridgeHeader
                );
                ShowLoadedRomStatus(state);
                SyncRecentRoms();
            }
            catch (InvalidOperationException exception)
            {
                statusBar.ShowError(exception.Message);
            }
        }
    }

    public async Task OpenRecentRomAsync(IStorageProvider storageProvider, string path)
    {
        var file = await storageProvider.TryGetFileFromPathAsync(path).ConfigureAwait(true);
        if (file is null)
        {
            statusBar.ShowError($"Recent ROM not found: {path}");

            try
            {
                libraryService.RemoveRomPath(path);
            }
            catch (InvalidOperationException exception)
            {
                statusBar.ShowError(exception.Message);
            }

            SyncRecentRoms();
            return;
        }

        await OpenRomFileAsync(file).ConfigureAwait(true);
    }

    public async Task ResetAsync()
    {
        inputRouter.Clear();
        ApplyRomActionResult(await controller.ResetAsync().ConfigureAwait(true));
    }

    public async Task StopAsync()
    {
        await controller.StopAsync().ConfigureAwait(true);
        inputRouter.Clear();
        SyncMenuState();
        SessionClosed?.Invoke(this, EventArgs.Empty);
    }

    public void TogglePause()
    {
        controller.TogglePause();
        SyncMenuState();
    }

    public void ToggleFastForward()
    {
        controller.ToggleFastForward();
        SyncMenuState();
    }

    public void SetFastForwardSpeed(EmulationSpeed speed)
    {
        controller.SetFastForwardSpeed(speed);
        SyncMenuState();
    }

    public bool ApplyKeyboardInput(Key key, bool pressed)
    {
        if (!controller.State.HasSession)
        {
            return false;
        }

        if (!pressed || key is not Key.Tab)
        {
            return inputRouter.Apply(key, pressed);
        }

        ToggleFastForward();
        return true;
    }

    public void ShowFault(Exception exception)
    {
        Dispatcher.UIThread.Post(() =>
        {
            inputRouter.Clear();
            SessionFaulted?.Invoke(this, EventArgs.Empty);
            SyncMenuState();
            statusBar.ShowError(exception.Message);
        });
    }

    public void SyncMenuState()
    {
        var state = controller.State;
        menu.SetEmulationActionsEnabled(state.HasSession);
        menu.SetPauseState(state.HasSession, state.IsPaused);
        menu.SetFastForwardState(state.FastForwardEnabled, state.FastForwardSpeed);
        statusBar.ShowSpeed(
            state.HasSession ? $"Speed {state.EffectiveSpeed.GetDisplayName()}" : string.Empty
        );
    }

    public void SyncRecentRoms()
    {
        try
        {
            menu.SetRecentRoms(libraryService.GetRoms(RecentRomLimit));
        }
        catch (InvalidOperationException exception)
        {
            statusBar.ShowError(exception.Message);
            menu.SetRecentRoms([]);
        }
    }

    private static void DragDrop_OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = RomFileFilter.GetDragEffects(e.DataTransfer.Formats);
        e.Handled = true;
    }

    private void DragDrop_OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;

        var file = RomFileFilter.GetFirstDroppedRom(e.DataTransfer.TryGetFiles());
        if (file is null)
        {
            statusBar.ShowError(RomFileFilter.UnsupportedDroppedFileMessage);
            return;
        }

        operationRunner.Run(() => OpenRomFileAsync(file));
    }

    private void ApplyRomActionResult(EmulationControllerState state)
    {
        if (state.HasSession)
        {
            ShowLoadedRomStatus(state);
            SessionOpened?.Invoke(this, EventArgs.Empty);
        }
        SyncMenuState();
    }

    private void ShowLoadedRomStatus(EmulationControllerState state) =>
        statusBar.ShowRomFileName(
            state.LoadedRomFileName,
            state.HardwareModel,
            _loadedRomCoverPath
        );
}
