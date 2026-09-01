// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using GbcNet.App.Cheats;
using GbcNet.App.Configuration;
using GbcNet.App.Emulation;
using GbcNet.App.Input;
using GbcNet.App.Library;
using GbcNet.App.Rendering;
using GbcNet.App.Saves;
using GbcNet.App.Shell;
using GbcNet.App.Shell.Chrome;
using GbcNet.Core.Ppu;
using Microsoft.Extensions.Logging;

namespace GbcNet.App;

internal sealed partial class MainWindow : Window, IDisposable
{
    private readonly EmulationSessionPresenter _emulationSession;
    private readonly GamepadManager _gamepadManager;
    private readonly LcdFramePresenter _framePresenter;
    private readonly LibraryView _libraryView;
    private readonly LibraryPresenter _libraryPresenter;
    private readonly ShellOperationRunner _operationRunner;
    private readonly ShellPresenter _shell;
    private readonly MainWindowMenuAdapter _menuAdapter;
    private readonly HashSet<Key> _pressedKeys = [];
    private bool _closeAfterAsyncStop;
    private bool _isEmulating;
    private int _closeStopStarted;

    public MainWindow(
        InputMap inputMap,
        StartupConfiguration startupConfiguration,
        AppConfigurationService configurationService,
        CartridgeBatterySaveFileService cartridgeSaveFileService,
        SaveStateFileService saveStateFileService,
        CheatCodeService cheatCodeService,
        LibraryService libraryService,
        IAudioOutput audioOutput,
        ILogger<MainWindow> logger,
        ILoggerFactory loggerFactory
    )
    {
        InitializeComponent();

        _libraryView = new LibraryView();
        _libraryView.SetViewMode(startupConfiguration.LibraryConfig.ViewMode);
        var emulationView = new EmulationView();
        ContentHost.Content = _libraryView;

        _framePresenter = new LcdFramePresenter(emulationView.Screen);

        _shell = new ShellPresenter(
            RomTitleTextBlock,
            EmulationStateTextBlock,
            PauseToolbarButton,
            FastForwardToolbarButton,
            Notification,
            NotificationItemsControl
        );
        _operationRunner = new ShellOperationRunner(
            exception => _shell.ShowError(exception.Message),
            logger
        );

        var emulationController = new EmulationController(
            startupConfiguration.BootRomOptions,
            audioOutput,
            cartridgeSaveFileService,
            saveStateFileService,
            cheatCodeService,
            OnFrameCompleted,
            handleFault: exception => OnEmulationFaulted(exception, logger),
            handlePersistenceError: exception => OnPersistenceError(exception, logger),
            fastForwardEnabled: startupConfiguration.EmulationConfig.FastForwardEnabled,
            startupConfiguration.EmulationConfig.FastForwardSpeed
        );
        var inputRouter = new InputRouter(
            inputMap.KeyboardBindings,
            inputMap.GamepadBindings,
            emulationController.SetButtonState
        );

        Deactivated += (_, _) =>
        {
            _pressedKeys.Clear();
            inputRouter.Clear();
        };

        _emulationSession = new EmulationSessionPresenter(
            emulationController,
            inputRouter,
            libraryService,
            configurationService,
            _shell,
            MainMenu,
            _operationRunner,
            loggerFactory.CreateLogger<EmulationSessionPresenter>(),
            loggerFactory.CreateLogger<CheatsWindow>()
        );
        _gamepadManager = new GamepadManager(
            inputRouter,
            togglePause: _emulationSession.TogglePause,
            toggleFastForward: _emulationSession.ToggleFastForward,
            loggerFactory.CreateLogger<GamepadManager>()
        );

        _gamepadManager.Start();
        _libraryView.OpenRomRequested = () => MainMenu.OpenRomCommand.Execute(parameter: null);
        _libraryView.ConfigurationRequested = () =>
            MainMenu.ConfigurationCommand.Execute(parameter: null);
        _libraryView.ActionsRequested = ToggleActionsOverlay;
        MainMenu.ActionInvoked += (_, _) => CloseActionsOverlay();
        SetEmulationToolbar(isEmulating: false);
        _libraryPresenter = new LibraryPresenter(
            _libraryView,
            libraryService,
            _operationRunner,
            StorageProvider,
            loggerFactory.CreateLogger<LibraryPresenter>(),
            path => _emulationSession.OpenRecentRomAsync(StorageProvider, path),
            _shell.ShowError
        );

        _emulationSession.SessionOpened += (_, _) =>
        {
            ContentHost.Content = emulationView;
            emulationView.Focus();
            SetEmulationToolbar(isEmulating: true);
        };
        EventHandler sessionEnded = (_, _) =>
        {
            ContentHost.Content = _libraryView;
            SetEmulationToolbar(isEmulating: false);
            _libraryPresenter.Refresh();
        };
        _emulationSession.SessionClosed += sessionEnded;
        _emulationSession.SessionFaulted += sessionEnded;

        _menuAdapter = new MainWindowMenuAdapter(
            MainMenu,
            this,
            _emulationSession,
            _gamepadManager,
            audioOutput,
            startupConfiguration.AudioConfig,
            configurationService,
            _shell,
            _operationRunner,
            loggerFactory.CreateLogger<MainWindowMenuAdapter>()
        );

        var configurationPresenter = new ConfigurationPresenter(
            configurationService,
            startupConfiguration.ConfigPath,
            _shell,
            _emulationSession.SetBootRomOptions,
            input =>
            {
                var replacementMap = InputMap.FromConfig(input);
                inputRouter.ReplaceBindings(
                    replacementMap.KeyboardBindings,
                    replacementMap.GamepadBindings
                );
            },
            _menuAdapter.ApplyAudioConfig,
            _gamepadManager,
            loggerFactory.CreateLogger<ConfigurationPresenter>(),
            loggerFactory.CreateLogger<SettingsWindow>()
        );

        _menuAdapter.Configure(emulationView, configurationPresenter);
        _libraryView.ViewModeChanged = _menuAdapter.SaveLibraryViewMode;
        _emulationSession.AttachDragDrop(this);
        _libraryPresenter.Refresh();

        if (startupConfiguration.StartupErrorMessage is not null)
        {
            _shell.ShowError(startupConfiguration.StartupErrorMessage);
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (e.Cancel || _closeAfterAsyncStop)
        {
            return;
        }

        e.Cancel = true;

        if (Interlocked.Exchange(location1: ref _closeStopStarted, value: 1) == 0)
        {
            _operationRunner.Run(StopAndCloseAsync);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != WindowStateProperty)
        {
            return;
        }

        UpdateEmulationToolbarVisibility();

        if (WindowState is WindowState.FullScreen && ActionsOverlay is { } overlay)
        {
            overlay.IsVisible = false;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        Dispose();
        base.OnClosed(e);
    }

    public void Dispose()
    {
        _gamepadManager.Dispose();
        _libraryPresenter.Dispose();
        _framePresenter.Dispose();
    }

    private async Task StopAndCloseAsync()
    {
        try
        {
            await _emulationSession.StopAsync();
            _closeAfterAsyncStop = true;
            Close();
        }
        finally
        {
            Volatile.Write(location: ref _closeStopStarted, value: 0);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e is { Handled: false, Key: Key.Escape } && ActionsOverlay.IsVisible)
        {
            CloseActionsOverlay();
            e.Handled = true;
        }

        if (e is { Handled: false, Key: Key.Escape } && WindowState is WindowState.FullScreen)
        {
            WindowState = WindowState.Normal;
            e.Handled = true;
        }

        if (!e.Handled && IsSearchGesture(e) && !_isEmulating)
        {
            CloseActionsOverlay();
            _libraryView.FocusSearch();
            e.Handled = true;
        }

        if (!e.Handled)
        {
            ApplyKeyboardEvent(e, pressed: true);
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        ApplyKeyboardEvent(e, pressed: false);
    }

    private void ApplyKeyboardEvent(KeyEventArgs e, bool pressed)
    {
        var keyStateChanged = pressed ? _pressedKeys.Add(e.Key) : _pressedKeys.Remove(e.Key);
        if (!keyStateChanged)
        {
            return;
        }

        if (_emulationSession.ApplyKeyboardInput(e.Key, pressed: pressed))
        {
            e.Handled = true;
        }
    }

    private void OnFrameCompleted(LcdFrame frame)
    {
        _framePresenter.Enqueue(frame);
    }

    private void OnPersistenceError(Exception exception, ILogger logger)
    {
        MainWindowLog.PersistenceFailed(logger, exception);
        Dispatcher.UIThread.Post(() => _shell.ShowError(exception.Message));
    }

    private void OnEmulationFaulted(Exception exception, ILogger logger)
    {
        MainWindowLog.EmulationFaulted(logger, exception);
        _emulationSession.ShowFault(exception);
    }

    private void SetEmulationToolbar(bool isEmulating)
    {
        _isEmulating = isEmulating;
        MainMenu.SetEmulationActionsEnabled(isEmulating);
        MainMenu.Margin = new Thickness(0, isEmulating ? 48 : 68, 8, 8);
        CloseActionsOverlay();
        UpdateEmulationToolbarVisibility();
    }

    private void UpdateEmulationToolbarVisibility()
    {
        if (AppToolbarHost is { } toolbar)
        {
            toolbar.IsVisible = _isEmulating && WindowState is not WindowState.FullScreen;
        }
    }

    private void OnToggleActionsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        ToggleActionsOverlay();

    private void OnActionsOverlayBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        CloseActionsOverlay();
        e.Handled = true;
    }

    private void ToggleActionsOverlay() => ActionsOverlay.IsVisible = !ActionsOverlay.IsVisible;

    private void CloseActionsOverlay() => ActionsOverlay.IsVisible = false;

    private void OnDismissNotificationClick(
        object? sender,
        Avalonia.Interactivity.RoutedEventArgs e
    ) => _shell.DismissError();

    private static bool IsSearchGesture(KeyEventArgs e) =>
        e.Key is Key.F
        && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) is not KeyModifiers.None;
}

internal static partial class MainWindowLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Battery save persistence failed.")]
    internal static partial void PersistenceFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Audio settings could not be saved.")]
    internal static partial void AudioSettingsSaveFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Library settings could not be saved.")]
    internal static partial void LibrarySettingsSaveFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Emulation session faulted.")]
    internal static partial void EmulationFaulted(ILogger logger, Exception exception);
}
