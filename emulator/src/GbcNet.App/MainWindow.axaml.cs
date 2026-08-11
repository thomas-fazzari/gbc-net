// Copyright (C) 2026 thomas-fazzari, Fournux
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using GbcNet.App.Audio;
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
    private readonly LibraryPresenter _libraryPresenter;
    private readonly MacOsTitleBar _macOsTitleBar;
    private readonly ShellOperationRunner _operationRunner;
    private readonly ShellPresenter _shell;
    private readonly MainWindowMenuAdapter _menuAdapter;
    private readonly ILogger<MainWindow> _logger;
    private readonly HashSet<Key> _pressedKeys = [];
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
        InitializeComponent();
        _macOsTitleBar = new MacOsTitleBar(this);
        UpdateTitleBarInsets();
        LibrarySearchTextBox.TextChanged += OnLibrarySearchTextChanged;
        UpdateSearchAction();

        var libraryView = new LibraryView();
        libraryView.SetViewMode(startupConfiguration.LibraryConfig.ViewMode);
        LibrarySearchHost.DataContext = libraryView;
        var emulationView = new EmulationView();
        ContentHost.Content = libraryView;

        _framePresenter = new LcdFramePresenter(emulationView.Screen);

        _shell = new ShellPresenter(
            RomTitleTextBlock,
            EmulationStateBadge,
            EmulationStateTextBlock,
            PauseTitleBarButton,
            FastForwardTitleBarButton,
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
        libraryView.OpenRomRequested = () => MainMenu.OpenRomCommand.Execute(parameter: null);
        _libraryPresenter = new LibraryPresenter(
            libraryView,
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
            SetEmulationTitleBar(isEmulating: true);
        };
        _emulationSession.SessionClosed += (_, _) =>
        {
            ContentHost.Content = libraryView;
            SetEmulationTitleBar(isEmulating: false);
            _libraryPresenter.Refresh();
        };
        _emulationSession.SessionFaulted += (_, _) =>
        {
            ContentHost.Content = libraryView;
            SetEmulationTitleBar(isEmulating: false);
            _libraryPresenter.Refresh();
        };

        _menuAdapter = new MainWindowMenuAdapter(
            MainMenu,
            this,
            _emulationSession,
            _gamepadManager,
            audioOutput,
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
            GbcNetApplication.ApplyTheme,
            _gamepadManager,
            loggerFactory.CreateLogger<ConfigurationPresenter>(),
            loggerFactory.CreateLogger<SettingsWindow>()
        );

        _menuAdapter.Configure(
            emulationView,
            startupConfiguration.AudioConfig,
            configurationPresenter
        );
        libraryView.ViewModeChanged = _menuAdapter.SaveLibraryViewMode;
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
        // Fires during base Window construction, before _menuAdapter is assigned.
        _menuAdapter?.SyncFullscreenState(change);

        if (change.Property == WindowStateProperty && TitleBarHost is { } titleBar)
        {
            titleBar.IsVisible = WindowState is not WindowState.FullScreen;
        }

        if (
            change.Property == WindowDecorationMarginProperty
            || change.Property == OffScreenMarginProperty
        )
        {
            UpdateTitleBarInsets();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        Dispose();
        base.OnClosed(e);
    }

    public void Dispose()
    {
        _macOsTitleBar.Dispose();
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
        if (!e.Handled && e.Key is Key.Escape && WindowState is WindowState.FullScreen)
        {
            WindowState = WindowState.Normal;
            e.Handled = true;
        }

        if (!e.Handled && IsSearchGesture(e) && LibrarySearchHost.IsVisible)
        {
            LibrarySearchTextBox.Focus();
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

    private void OnPersistenceError(Exception exception)
    {
        MainWindowLog.PersistenceFailed(_logger, exception);
        Dispatcher.UIThread.Post(() => _shell.ShowError(exception.Message));
    }

    private void OnEmulationFaulted(Exception exception)
    {
        MainWindowLog.EmulationFaulted(_logger, exception);
        _emulationSession.ShowFault(exception);
    }

    private void SetEmulationTitleBar(bool isEmulating)
    {
        LibraryTitleBarLeft.IsVisible = !isEmulating;
        LibrarySearchHost.IsVisible = !isEmulating;
        LibraryTitleBarActions.IsVisible = !isEmulating;
        EmulationTitleBarLeft.IsVisible = isEmulating;
        EmulationTitleBarCenter.IsVisible = isEmulating;
        EmulationTitleBarActions.IsVisible = isEmulating;
    }

    private void OnClearSearchClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        LibrarySearchTextBox.Text = string.Empty;
        LibrarySearchTextBox.Focus();
    }

    private void OnLibrarySearchTextChanged(object? sender, TextChangedEventArgs e) =>
        UpdateSearchAction();

    private void UpdateSearchAction()
    {
        var hasSearchText = !string.IsNullOrEmpty(LibrarySearchTextBox.Text);
        ClearSearchButton.IsVisible = hasSearchText;
        OpenRomSearchButton.IsVisible = !hasSearchText;
        SearchBrowseDivider.IsVisible = !hasSearchText;
    }

    private void UpdateTitleBarInsets()
    {
        if (TitleBarActionsHost is null || EmulationTitleBarLeft is null)
        {
            return;
        }

        var left = OperatingSystem.IsMacOS()
            ? 88
            : Math.Max(WindowDecorationMargin.Left, OffScreenMargin.Left) + 8;
        var right = Math.Max(WindowDecorationMargin.Right, OffScreenMargin.Right) + 8;
        EmulationTitleBarLeft.Margin = new Thickness(left, 0, 0, 0);
        TitleBarActionsHost.Margin = new Thickness(0, 0, right, 0);
    }

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
