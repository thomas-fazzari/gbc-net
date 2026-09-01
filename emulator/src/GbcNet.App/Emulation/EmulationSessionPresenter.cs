// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using GbcNet.App.Cheats;
using GbcNet.App.Configuration;
using GbcNet.App.Configuration.Sections.Emulation;
using GbcNet.App.Input;
using GbcNet.App.Library;
using GbcNet.App.Saves;
using GbcNet.App.Shell;
using GbcNet.App.Shell.Chrome;
using GbcNet.Core;
using Microsoft.Extensions.Logging;

namespace GbcNet.App.Emulation;

internal sealed class EmulationSessionPresenter(
    EmulationController controller,
    InputRouter inputRouter,
    LibraryService libraryService,
    AppConfigurationService configurationService,
    ShellPresenter shell,
    MainMenu menu,
    ShellOperationRunner operationRunner,
    ILogger<EmulationSessionPresenter> logger,
    ILogger cheatsLogger,
    TimeProvider? timeProvider = null
)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private RomStorageIdentity? _activeRomIdentity;
    private long? _playStartedAtTimestamp;

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
        var files = await storageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open Game Boy ROM",
                AllowMultiple = false,
                FileTypeFilter = [_gameBoyRomFileType],
            }
        );

        if (files.Count > 0)
        {
            await OpenRomFileAsync(files[0]);
        }
    }

    public async Task OpenRomFileAsync(IStorageFile file)
    {
        inputRouter.Clear();
        var result = await controller.OpenRomFileAsync(file);
        if (result.IsError)
        {
            shell.ShowError(result.FirstError.Description);
            return;
        }

        FlushPlayTime();

        ClearPlayTime();
        var state = result.Value;
        ApplyRomActionResult(state);
        if (
            file.Path.IsFile
            && state
                is { LoadedCartridgeHeader: { } cartridgeHeader, LoadedRomIdentity: { } identity }
        )
        {
            try
            {
                libraryService.RecordLoadedRom(
                    file.Path.LocalPath,
                    identity.HashHex,
                    state.LoadedRom,
                    cartridgeHeader
                );

                BeginPlayTime(identity);
            }
            catch (InvalidOperationException exception)
            {
                EmulationSessionPresenterLog.LibraryRecordFailed(logger, exception);
                shell.ShowError(exception.Message);
            }
        }
    }

    public async Task OpenRecentRomAsync(IStorageProvider storageProvider, string path)
    {
        var file = await storageProvider.TryGetFileFromPathAsync(path);
        if (file is null)
        {
            EmulationSessionPresenterLog.RecentRomUnavailable(logger);
            shell.ShowError($"Recent ROM not found: {path}");

            try
            {
                libraryService.RemoveRomPath(path);
            }
            catch (InvalidOperationException exception)
            {
                EmulationSessionPresenterLog.RecentRomRemovalFailed(logger, exception);
                shell.ShowError(exception.Message);
            }

            return;
        }

        await OpenRomFileAsync(file);
    }

    public async Task ResetAsync()
    {
        inputRouter.Clear();
        var state = await controller.ResetAsync();
        ApplyRomActionResult(state);

        if (state is { HasSession: true, IsPaused: false } && _activeRomIdentity is not null)
        {
            ResumePlayTime();
        }
    }

    public async Task OpenCheatsAsync(Window owner)
    {
        var state = controller.State;
        if (!state.HasSession)
        {
            return;
        }

        var pausedByThisMethod = false;

        if (!state.IsPaused)
        {
            inputRouter.Clear();
            TogglePause();
            pausedByThisMethod = true;
        }

        try
        {
            await new CheatsWindow(
                controller.State.CheatCodes.ToArray(),
                entries => controller.SetCheatCodesAsync(entries, CancellationToken.None),
                cheatsLogger
            ).ShowDialog<bool?>(owner);
        }
        finally
        {
            if (pausedByThisMethod && controller.State is { HasSession: true, IsPaused: true })
            {
                TogglePause();
            }
        }
    }

    public async Task StopAsync()
    {
        await controller.StopAsync();
        FlushPlayTime();
        ClearPlayTime();
        inputRouter.Clear();
        SyncMenuState();
        SessionClosed?.Invoke(this, EventArgs.Empty);
    }

    public async Task SaveStateAsync(int slot)
    {
        await controller.SaveStateAsync(slot, CancellationToken.None);

        SyncSaveStateDates();
    }

    public Task LoadStateAsync(int slot) => controller.LoadStateAsync(slot, CancellationToken.None);

    public void TogglePause()
    {
        controller.TogglePause();
        if (controller.State.IsPaused)
        {
            FlushPlayTime();
        }
        else if (_activeRomIdentity is not null)
        {
            ResumePlayTime();
        }
        SyncMenuState();
    }

    public void ToggleFastForward()
    {
        controller.ToggleFastForward();
        SaveFastForwardConfig();
        SyncMenuState();
    }

    public void SetFastForwardSpeed(EmulationSpeed speed)
    {
        controller.SetFastForwardSpeed(speed);
        SaveFastForwardConfig();
        SyncMenuState();
    }

    private void SaveFastForwardConfig()
    {
        var state = controller.State;
        try
        {
            configurationService.SaveEmulationConfig(
                new EmulationConfig
                {
                    FastForwardEnabled = state.FastForwardEnabled,
                    FastForwardSpeed = state.FastForwardSpeed,
                }
            );
        }
        catch (ConfigurationException exception)
        {
            EmulationSessionPresenterLog.FastForwardSettingsSaveFailed(logger, exception);
            shell.ShowError(exception.Message);
        }
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
            FlushPlayTime();
            ClearPlayTime();
            inputRouter.Clear();
            SessionFaulted?.Invoke(this, EventArgs.Empty);
            SyncMenuState();
            shell.ShowError(exception.Message);
        });
    }

    public void SyncMenuState()
    {
        var state = controller.State;

        menu.SetEmulationActionsEnabled(state.HasSession);
        menu.SetCheatsEnabled(state.HasSession);
        menu.SetPauseState(state.HasSession, state.IsPaused);
        menu.SetFastForwardState(state.FastForwardEnabled, state.FastForwardSpeed);

        SyncSaveStateDates();

        shell.ShowEmulationState(
            state.HasSession,
            state.IsPaused,
            state.FastForwardEnabled,
            state.HasSession ? state.EffectiveSpeed.GetDisplayName() : string.Empty
        );
    }

    private void SyncSaveStateDates() =>
        menu.SetStateSlotDates(
            controller.State.HasSession
                ? controller.GetSaveStateDates(MainMenu.StateSlotCount)
                : new DateTime?[MainMenu.StateSlotCount]
        );

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
            shell.ShowError(RomFileFilter.UnsupportedDroppedFileMessage);
            return;
        }

        operationRunner.Run(() => OpenRomFileAsync(file));
    }

    private void BeginPlayTime(RomStorageIdentity rom)
    {
        _activeRomIdentity = rom;
        ResumePlayTime();
    }

    private void ResumePlayTime() => _playStartedAtTimestamp ??= _timeProvider.GetTimestamp();

    private void FlushPlayTime()
    {
        if (_playStartedAtTimestamp is not { } startedAt || _activeRomIdentity is not { } identity)
        {
            return;
        }

        _playStartedAtTimestamp = null;
        try
        {
            libraryService.RecordPlayTime(
                identity.HashHex,
                _timeProvider.GetElapsedTime(startedAt, _timeProvider.GetTimestamp())
            );
        }
        catch (InvalidOperationException exception)
        {
            EmulationSessionPresenterLog.LibraryPlayTimeRecordFailed(logger, exception);
            shell.ShowError(exception.Message);
        }
    }

    private void ClearPlayTime()
    {
        _activeRomIdentity = null;
        _playStartedAtTimestamp = null;
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
        shell.ShowRomFileName(state.LoadedRomFileName);
}

internal static partial class EmulationSessionPresenterLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Loaded ROM could not be recorded in the library."
    )]
    internal static partial void LibraryRecordFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Recent ROM is no longer available.")]
    internal static partial void RecentRomUnavailable(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Unavailable recent ROM could not be removed from the library."
    )]
    internal static partial void RecentRomRemovalFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ROM play time could not be recorded.")]
    internal static partial void LibraryPlayTimeRecordFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Fast-forward settings could not be saved.")]
    internal static partial void FastForwardSettingsSaveFailed(ILogger logger, Exception exception);
}
