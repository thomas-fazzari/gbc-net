// Copyright (C) 2026 thomas-fazzari, Fournux
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using GbcNet.App.Audio;
using GbcNet.App.Cheats;
using GbcNet.App.Configuration;
using GbcNet.App.Configuration.Sections.Audio;
using GbcNet.App.Configuration.Sections.Library;
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
    private readonly AppConfigurationService _configurationService;
    private readonly IAudioOutput _audioOutput;
    private readonly EmulationSessionPresenter _emulationSession;
    private readonly GamepadManager _gamepadManager;
    private readonly LcdFramePresenter _framePresenter;
    private readonly ShellOperationRunner _operationRunner;
    private readonly StatusBarPresenter _statusBar;
    private readonly ILogger<MainWindow> _logger;
    private readonly HashSet<Key> _pressedKeys = [];
    private AudioConfig _audioConfig;
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
        CheatCodeService cheatCodeService,
        LibraryService libraryService,
        IAudioOutput audioOutput,
        ILogger<MainWindow> logger,
        ILoggerFactory loggerFactory
    )
    {
        _logger = logger;
        _configurationService = configurationService;
        _audioOutput = audioOutput;
        _audioConfig = startupConfiguration.AudioConfig;
        InitializeComponent();
        ApplyAudioConfig(_audioConfig);

        var libraryView = new LibraryView();
        libraryView.SetViewMode(startupConfiguration.LibraryConfig.ViewMode);
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
            cheatCodeService,
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
        libraryView.OpenRomRequested = () =>
            _operationRunner.Run(() => _emulationSession.OpenRomAsync(StorageProvider));
        libraryView.ViewModeChanged = viewMode => SaveLibraryViewMode(libraryView, viewMode);

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
            ApplyAudioConfig,
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
        MainMenu.OpenRom = () =>
            _operationRunner.Run(() => _emulationSession.OpenRomAsync(StorageProvider));
        MainMenu.RefreshRecentRoms = _emulationSession.SyncRecentRoms;
        MainMenu.OpenRecentRom = path =>
            _operationRunner.Run(() => _emulationSession.OpenRecentRomAsync(StorageProvider, path));
        MainMenu.Close = () => _operationRunner.Run(_emulationSession.StopAsync);
        MainMenu.OpenConfiguration = () =>
            _operationRunner.Run(() => _configurationPresenter.OpenAsync(this));
        MainMenu.OpenConfigurationFileLocation = () =>
            _operationRunner.Run(_configurationPresenter.OpenConfigurationDirectoryAsync);
        MainMenu.OpenLogFileLocation = () =>
            _operationRunner.Run(ConfigurationPresenter.OpenLogDirectoryAsync);
        MainMenu.TogglePause = _emulationSession.TogglePause;
        MainMenu.Reset = () => _operationRunner.Run(_emulationSession.ResetAsync);
        MainMenu.OpenCheats = () =>
            _operationRunner.Run(async () =>
            {
                var gameplayEnabled = _gamepadManager.GameplayEnabled;
                _gamepadManager.SetGameplayEnabled(enabled: false);
                try
                {
                    await _emulationSession.OpenCheatsAsync(this);
                }
                finally
                {
                    _gamepadManager.SetGameplayEnabled(enabled: gameplayEnabled);
                    Dispatcher.UIThread.Post(() => emulationView.Focus(), DispatcherPriority.Input);
                }
            });
        MainMenu.ToggleMute = ToggleMute;
        MainMenu.SaveState = slotIndex =>
            _operationRunner.Run(() => _emulationSession.SaveStateAsync(slotIndex));
        MainMenu.LoadState = slotIndex =>
            _operationRunner.Run(() => _emulationSession.LoadStateAsync(slotIndex));
        MainMenu.ToggleFastForward = _emulationSession.ToggleFastForward;
        MainMenu.SetFastForwardSpeed = speed =>
        {
            _emulationSession.SetFastForwardSpeed(speed);
            Dispatcher.UIThread.Post(() => emulationView.Focus(), DispatcherPriority.Input);
        };
        MainMenu.ToggleFullscreen = ToggleFullscreen;
        MainMenu.ToggleMenuBar = ToggleMenuBar;
        MainMenu.ToggleStatusBar = ToggleStatusBar;
        MainMenu.OpenGitHubRepository = () => _operationRunner.Run(OpenGitHubRepositoryAsync);
        Activated += (_, _) => _gamepadManager.SetGameplayEnabled(enabled: true);
        Deactivated += (_, _) => _gamepadManager.SetGameplayEnabled(enabled: false);
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
        MainMenu.SetMuteState(isMuted: _audioConfig.Muted);
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

    private void ApplyAudioConfig(AudioConfig config)
    {
        _audioConfig = config;
        _audioOutput.SetVolume(config.VolumePercent, config.Muted);
        MainMenu.SetMuteState(isMuted: config.Muted);
    }

    private void ToggleMute()
    {
        ApplyAudioConfig(_audioConfig with { Muted = !_audioConfig.Muted });
        try
        {
            _configurationService.SaveAudioConfig(_audioConfig);
        }
        catch (ConfigurationException exception)
        {
            MainWindowLog.AudioSettingsSaveFailed(_logger, exception);
            _statusBar.ShowError(exception.Message);
        }
    }

    private void SaveLibraryViewMode(LibraryView libraryView, LibraryViewMode viewMode)
    {
        try
        {
            _configurationService.SaveLibraryConfig(new LibraryConfig { ViewMode = viewMode });
        }
        catch (ConfigurationException exception)
        {
            MainWindowLog.LibrarySettingsSaveFailed(_logger, exception);
            libraryView.ShowError(exception.Message);
        }
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Audio settings could not be saved.")]
    internal static partial void AudioSettingsSaveFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Library settings could not be saved.")]
    internal static partial void LibrarySettingsSaveFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Emulation session faulted.")]
    internal static partial void EmulationFaulted(ILogger logger, Exception exception);
}
