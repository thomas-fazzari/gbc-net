// Copyright (C) 2026 thomas-fazzari, Fournux
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using GbcNet.App.Audio;
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
    private readonly ConfigurationPresenter _configurationPresenter;
    private readonly EmulationSessionPresenter _emulationSession;
    private readonly GamepadManager _gamepadManager;
    private readonly LcdFramePresenter _framePresenter;
    private readonly ShellOperationRunner _operationRunner;
    private readonly StatusBarPresenter _statusBar;
    private readonly ILogger<MainWindow> _logger;
    private readonly HashSet<Key> _pressedKeys = [];
    private bool _statusBarAvailable = true;
    private bool _statusBarVisibleWhenAvailable = true;
    private bool _menuBarVisibleWhenAvailable = true;
    private bool _closeAfterAsyncStop;
    private int _closeStopStarted;

    public MainWindow(
        InputMap inputMap,
        StartupConfiguration startupConfiguration,
        AppConfigurationService configurationService,
        CartridgeBatterySaveFileService cartridgeSaveFileService,
        SaveStateFileService saveStateFileService,
        LibraryService libraryService,
        IAudioOutput audioOutput,
        ILogger<MainWindow> logger,
        ILoggerFactory loggerFactory
    )
    {
        _logger = logger;
        InitializeComponent();

        var libraryView = new LibraryView();
        var emulationView = new EmulationView();
        ContentHost.Content = libraryView;

        _framePresenter = new LcdFramePresenter(emulationView.Screen);

        _statusBar = new StatusBarPresenter(
            message: StatusTextBlock,
            coverFrame: StatusCoverFrame,
            coverImage: StatusCoverImage,
            hardwareBadge: StatusHardwareBadge,
            hardwareBadgeText: StatusHardwareBadgeTextBlock,
            speedBadge: StatusSpeedBadge,
            speed: StatusSpeedTextBlock
        );
        _operationRunner = new ShellOperationRunner(
            exception => _statusBar.ShowError(exception.Message),
            logger
        );

        SetStatusBarAvailable(isAvailable: false);

        var emulationController = new EmulationController(
            startupConfiguration.BootRomOptions,
            audioOutput,
            cartridgeSaveFileService,
            saveStateFileService,
            OnFrameCompleted,
            handleFault: OnEmulationFaulted,
            handlePersistenceError: OnPersistenceError,
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
            _statusBar,
            MainMenu,
            _operationRunner,
            loggerFactory.CreateLogger<EmulationSessionPresenter>()
        );
        _gamepadManager = new GamepadManager(
            inputRouter,
            togglePause: _emulationSession.TogglePause,
            toggleFastForward: _emulationSession.ToggleFastForward,
            loggerFactory.CreateLogger<GamepadManager>()
        );

        _gamepadManager.Start();

        var libraryPresenter = new LibraryPresenter(
            libraryView,
            libraryService,
            _operationRunner,
            StorageProvider,
            loggerFactory.CreateLogger<LibraryPresenter>(),
            path => _emulationSession.OpenRecentRomAsync(StorageProvider, path)
        );

        _emulationSession.SessionOpened += (_, _) =>
        {
            ContentHost.Content = emulationView;
            emulationView.Focus();
            SetMenuBarVisible(isVisible: false);
            SetStatusBarAvailable(isAvailable: true);
            SetStatusBarVisible(isVisible: false);
        };
        _emulationSession.SessionClosed += (_, _) =>
        {
            ContentHost.Content = libraryView;
            SetMenuBarVisible(isVisible: true);
            SetStatusBarAvailable(isAvailable: false);
            libraryPresenter.Refresh();
        };
        _emulationSession.SessionFaulted += (_, _) =>
        {
            ContentHost.Content = libraryView;
            SetMenuBarVisible(isVisible: true);
            SetStatusBarAvailable(isAvailable: true);
            SetStatusBarVisible(isVisible: true);
            libraryPresenter.Refresh();
        };

        _configurationPresenter = new ConfigurationPresenter(
            configurationService,
            startupConfiguration.ConfigPath,
            _statusBar,
            _emulationSession.SetBootRomOptions,
            input =>
            {
                var replacementMap = InputMap.FromConfig(input);
                inputRouter.ReplaceBindings(
                    replacementMap.KeyboardBindings,
                    replacementMap.GamepadBindings
                );
            },
            _gamepadManager,
            loggerFactory.CreateLogger<ConfigurationPresenter>()
        );

        ConfigureMenu(emulationView);
        _emulationSession.AttachDragDrop(this);
        libraryPresenter.Refresh();

        if (startupConfiguration.StartupErrorMessage is not null)
        {
            _statusBar.ShowError(startupConfiguration.StartupErrorMessage);
        }
    }

    private void ConfigureMenu(EmulationView emulationView)
    {
        MainMenu.AttachNativeMenu(this);
        MainMenu.OpenRomRequested += (_, _) =>
            _operationRunner.Run(() => _emulationSession.OpenRomAsync(StorageProvider));
        MainMenu.RecentRomsRequested += (_, _) => _emulationSession.SyncRecentRoms();
        MainMenu.RecentRomSelected += (_, e) =>
            _operationRunner.Run(() =>
                _emulationSession.OpenRecentRomAsync(StorageProvider, e.Path)
            );
        MainMenu.CloseRequested += (_, _) => _operationRunner.Run(_emulationSession.StopAsync);
        MainMenu.ConfigurationRequested += (_, _) =>
            _operationRunner.Run(() => _configurationPresenter.OpenAsync(this));
        MainMenu.ConfigurationFileLocationRequested += (_, _) =>
            _operationRunner.Run(_configurationPresenter.OpenConfigurationDirectoryAsync);
        MainMenu.LogFileLocationRequested += (_, _) =>
            _operationRunner.Run(ConfigurationPresenter.OpenLogDirectoryAsync);
        MainMenu.PauseRequested += (_, _) => _emulationSession.TogglePause();
        MainMenu.ResetRequested += (_, _) => _operationRunner.Run(_emulationSession.ResetAsync);
        MainMenu.SaveStateRequested += (_, e) =>
            _operationRunner.Run(() => _emulationSession.SaveStateAsync(e.SlotIndex));
        MainMenu.LoadStateRequested += (_, e) =>
            _operationRunner.Run(() => _emulationSession.LoadStateAsync(e.SlotIndex));
        MainMenu.FastForwardRequested += (_, _) => _emulationSession.ToggleFastForward();
        MainMenu.FastForwardSpeedSelected += (_, e) =>
        {
            _emulationSession.SetFastForwardSpeed(e.Speed);
            Dispatcher.UIThread.Post(() => emulationView.Focus(), DispatcherPriority.Input);
        };
        Activated += (_, _) => _gamepadManager.SetGameplayEnabled(enabled: true);
        Deactivated += (_, _) => _gamepadManager.SetGameplayEnabled(enabled: false);
        MainMenu.FullscreenRequested += (_, _) => ToggleFullscreen();
        MainMenu.MenuBarRequested += (_, _) => ToggleMenuBar();
        MainMenu.StatusBarRequested += (_, _) => ToggleStatusBar();
        MainMenu.GitHubRepositoryRequested += (_, _) =>
            _operationRunner.Run(OpenGitHubRepositoryAsync);
        SyncMenuState();
        _emulationSession.SyncMenuState();
        _emulationSession.SyncRecentRoms();
    }

    private static Task OpenGitHubRepositoryAsync()
    {
        using var process =
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = "https://github.com/thomas-fazzari/gbc-net",
                    UseShellExecute = true,
                }
            ) ?? throw new InvalidOperationException("GitHub repository could not be opened.");

        return Task.CompletedTask;
    }

    private void SyncMenuState()
    {
        MainMenu.SetFullscreenState(isFullscreen: WindowState is WindowState.FullScreen);
        MainMenu.SetMenuBarState(isVisible: _menuBarVisibleWhenAvailable);
        MainMenu.SetStatusBarAvailability(isAvailable: _statusBarAvailable);
        MainMenu.SetStatusBarState(isVisible: StatusBar.IsVisible);
    }

    private void SyncFullscreenState(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property != WindowStateProperty || MainMenu is null)
        {
            return;
        }

        ApplyMenuBarVisibility();
        MainMenu.SetFullscreenState(isFullscreen: WindowState is WindowState.FullScreen);
    }

    private void ToggleFullscreen()
    {
        WindowState =
            WindowState is WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
    }

    private void ToggleMenuBar()
    {
        if (OperatingSystem.IsMacOS())
        {
            return;
        }

        _menuBarVisibleWhenAvailable = !_menuBarVisibleWhenAvailable;
        ApplyMenuBarVisibility();
    }

    private void ToggleStatusBar()
    {
        if (!_statusBarAvailable)
        {
            return;
        }

        _statusBarVisibleWhenAvailable = !_statusBarVisibleWhenAvailable;
        ApplyStatusBarVisibility();
    }

    private void SetMenuBarVisible(bool isVisible)
    {
        if (OperatingSystem.IsMacOS())
        {
            return;
        }

        _menuBarVisibleWhenAvailable = isVisible;
        ApplyMenuBarVisibility();
    }

    private void SetStatusBarVisible(bool isVisible)
    {
        _statusBarVisibleWhenAvailable = isVisible;
        ApplyStatusBarVisibility();
    }

    private void SetStatusBarAvailable(bool isAvailable)
    {
        _statusBarAvailable = isAvailable;
        ApplyStatusBarVisibility();
    }

    private void ApplyStatusBarVisibility()
    {
        StatusBar.IsVisible = _statusBarAvailable && _statusBarVisibleWhenAvailable;
        MainMenu.SetStatusBarAvailability(isAvailable: _statusBarAvailable);
        MainMenu.SetStatusBarState(isVisible: StatusBar.IsVisible);
    }

    private void ApplyMenuBarVisibility()
    {
        MainMenu.IsVisible =
            !OperatingSystem.IsMacOS()
            && _menuBarVisibleWhenAvailable
            && WindowState is not WindowState.FullScreen;
        MainMenu.SetMenuBarState(isVisible: _menuBarVisibleWhenAvailable);
    }

    private bool TryHandleChromeShortcut(Key key, KeyModifiers modifiers)
    {
        switch (key)
        {
            case Key.Enter when modifiers.HasFlag(KeyModifiers.Alt):
                ToggleFullscreen();
                return true;

            case Key.I
                when modifiers.HasFlag(
                    OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control
                ):
                ToggleStatusBar();
                return true;

            default:
                return false;
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
        SyncFullscreenState(change);
    }

    protected override void OnClosed(EventArgs e)
    {
        Dispose();
        base.OnClosed(e);
    }

    public void Dispose()
    {
        _gamepadManager.Dispose();
        _statusBar.Dispose();
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
        catch (Exception exception)
            when (exception
                    is IOException
                        or UnauthorizedAccessException
                        or InvalidOperationException
                        or NotSupportedException
                        or ArgumentException
            )
        {
            _statusBar.ShowError(exception.Message);
            Volatile.Write(location: ref _closeStopStarted, value: 0);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        ApplyKeyboardEvent(e, pressed: true);
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

        if (
            (
                pressed
                && (
                    MainMenu.TryHandleShortcut(e.Key, e.KeyModifiers)
                    || TryHandleChromeShortcut(e.Key, e.KeyModifiers)
                )
            ) || _emulationSession.ApplyKeyboardInput(e.Key, pressed: pressed)
        )
        {
            e.Handled = true;
        }
    }

    private void OnFrameCompleted(LcdFrame frame)
    {
        _framePresenter.Enqueue(frame);
    }

    private void OnPersistenceError(Exception exception)
    {
        MainWindowLog.PersistenceFailed(_logger, exception);
        Dispatcher.UIThread.Post(() => _statusBar.ShowError(exception.Message));
    }

    private void OnEmulationFaulted(Exception exception)
    {
        MainWindowLog.EmulationFaulted(_logger, exception);
        _emulationSession.ShowFault(exception);
    }
}

internal static partial class MainWindowLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Battery save persistence failed.")]
    internal static partial void PersistenceFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Emulation session faulted.")]
    internal static partial void EmulationFaulted(ILogger logger, Exception exception);
}
