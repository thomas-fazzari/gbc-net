using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FluentResults;
using GbcNet.App.Common;
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

    private static readonly FilePickerFileType _gameBoyRomFileType = new("Game Boy ROM")
    {
        Patterns = ["*.gb", "*.gbc"],
        AppleUniformTypeIdentifiers = ["public.data"],
        MimeTypes = ["application/x-gameboy-rom", "application/x-gameboy-color-rom"],
    };

    public event EventHandler? SessionClosed;
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
        var result = await controller.OpenRomFileAsync(file).ConfigureAwait(true);

        ApplyRomActionResult(result);

        if (result.IsSuccess && file.Path.IsFile)
        {
            var recorded = await libraryService
                .RecordOpenedRomAsync(file.Path.LocalPath)
                .ConfigureAwait(true);
            if (recorded.IsFailed)
            {
                statusBar.ShowError(ResultErrors.Format(recorded.Errors));
            }
            else
            {
                SyncRecentRoms();
            }
        }
    }

    public async Task OpenRecentRomAsync(IStorageProvider storageProvider, string path)
    {
        var file = await storageProvider.TryGetFileFromPathAsync(path).ConfigureAwait(true);
        if (file is null)
        {
            statusBar.ShowError($"Recent ROM not found: {path}");

            var removed = libraryService.RemoveRomPath(path);
            if (removed.IsFailed)
            {
                statusBar.ShowError(ResultErrors.Format(removed.Errors));
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
        Dispatcher.UIThread.Post(() => statusBar.ShowError(exception.Message));
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
        var recentRoms = libraryService.GetRoms(RecentRomLimit);
        if (recentRoms.IsFailed)
        {
            statusBar.ShowError(ResultErrors.Format(recentRoms.Errors));
            menu.SetRecentRoms([]);
            return;
        }

        menu.SetRecentRoms(recentRoms.Value);
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

    private void ApplyRomActionResult(Result<EmulationControllerState> result)
    {
        if (result.IsFailed)
        {
            statusBar.ShowError(ResultErrors.Format(result.Errors));
            SyncMenuState();
            return;
        }

        if (result.Value.HasSession)
        {
            statusBar.ShowRomFileName(result.Value.LoadedRomFileName);
            SessionOpened?.Invoke(this, EventArgs.Empty);
        }
        SyncMenuState();
    }
}
