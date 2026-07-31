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
using GbcNet.App.Menus;
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
    StatusBarPresenter statusBar,
    MainMenu menu,
    ShellOperationRunner operationRunner,
    ILogger<EmulationSessionPresenter> logger,
    TimeProvider? timeProvider = null
)
{
    private const int RecentRomLimit = 5;
    private const int SaveStateSlotCount = 10;

    private string? _loadedRomCoverPath;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private ReadOnlyMemory<byte> _activeRom;
    private bool _hasActiveLibraryRom;
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
        var state = await controller.OpenRomFileAsync(file);
        FlushPlayTime();

        _loadedRomCoverPath = null;
        ClearPlayTime();
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

                BeginPlayTime(state.LoadedRom);
                ShowLoadedRomStatus(state);
                SyncRecentRoms();
            }
            catch (InvalidOperationException exception)
            {
                EmulationSessionPresenterLog.LibraryRecordFailed(logger, exception);
                statusBar.ShowError(exception.Message);
            }
        }
    }

    public async Task OpenRecentRomAsync(IStorageProvider storageProvider, string path)
    {
        var file = await storageProvider.TryGetFileFromPathAsync(path);
        if (file is null)
        {
            EmulationSessionPresenterLog.RecentRomUnavailable(logger);
            statusBar.ShowError($"Recent ROM not found: {path}");

            try
            {
                libraryService.RemoveRomPath(path);
            }
            catch (InvalidOperationException exception)
            {
                EmulationSessionPresenterLog.RecentRomRemovalFailed(logger, exception);
                statusBar.ShowError(exception.Message);
            }

            SyncRecentRoms();
            return;
        }

        await OpenRomFileAsync(file);
    }

    public async Task ResetAsync()
    {
        inputRouter.Clear();
        var state = await controller.ResetAsync();
        ApplyRomActionResult(state);

        if (state.HasSession && !state.IsPaused && _hasActiveLibraryRom)
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
                controller.State.GameGenieCodes.ToArray(),
                async entries =>
                {
                    try
                    {
                        await controller.SetGameGenieCodesAsync(entries, CancellationToken.None);
                    }
                    catch (Exception exception)
                        when (exception
                                is ArgumentException
                                    or InvalidOperationException
                                    or OperationCanceledException
                        )
                    {
                        EmulationSessionPresenterLog.GameGenieApplyFailed(logger, exception);
                        throw;
                    }
                }
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
        else if (_hasActiveLibraryRom)
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
            statusBar.ShowError(exception.Message);
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
            statusBar.ShowError(exception.Message);
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

        statusBar.ShowSpeed(
            state.HasSession ? $"Speed {state.EffectiveSpeed.GetDisplayName()}" : string.Empty
        );
    }

    private void SyncSaveStateDates() =>
        menu.SetSaveStateDates(
            controller.State.HasSession
                ? controller.GetSaveStateDates(SaveStateSlotCount)
                : new DateTime?[SaveStateSlotCount]
        );

    public void SyncRecentRoms()
    {
        try
        {
            menu.SetRecentRoms(libraryService.GetRoms(RecentRomLimit));
        }
        catch (InvalidOperationException exception)
        {
            EmulationSessionPresenterLog.RecentRomsRefreshFailed(logger, exception);
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

    private void BeginPlayTime(ReadOnlyMemory<byte> rom)
    {
        _activeRom = rom;
        _hasActiveLibraryRom = true;
        ResumePlayTime();
    }

    private void ResumePlayTime() => _playStartedAtTimestamp ??= _timeProvider.GetTimestamp();

    private void FlushPlayTime()
    {
        if (_playStartedAtTimestamp is not { } startedAt)
        {
            return;
        }

        _playStartedAtTimestamp = null;
        try
        {
            libraryService.RecordPlayTime(
                _activeRom,
                _timeProvider.GetElapsedTime(startedAt, _timeProvider.GetTimestamp())
            );
        }
        catch (InvalidOperationException exception)
        {
            EmulationSessionPresenterLog.LibraryPlayTimeRecordFailed(logger, exception);
            statusBar.ShowError(exception.Message);
        }
    }

    private void ClearPlayTime()
    {
        _activeRom = default;
        _hasActiveLibraryRom = false;
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
        statusBar.ShowRomFileName(
            state.LoadedRomFileName,
            state.HardwareModel,
            _loadedRomCoverPath
        );
}

internal static partial class EmulationSessionPresenterLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Game Genie codes could not be applied.")]
    internal static partial void GameGenieApplyFailed(ILogger logger, Exception exception);

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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Recent ROM list could not be refreshed.")]
    internal static partial void RecentRomsRefreshFailed(ILogger logger, Exception exception);
}
