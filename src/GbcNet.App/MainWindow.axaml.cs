// Copyright (C) 2026 thomas-fazzari, Fournux
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
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
    private readonly LcdFramePresenter _framePresenter;
    private readonly ShellOperationRunner _operationRunner;
    private readonly StatusBarPresenter _statusBar;
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
        LibraryService libraryService,
        IAudioOutput audioOutput,
        ILogger<MainWindow> logger
    )
    {
        InitializeComponent();
        Background = AppChrome.Brush(AppChrome.Bg);
        StatusBar.Background = AppChrome.Brush(AppChrome.Panel);
        StatusBar.BorderBrush = AppChrome.Brush(AppChrome.Hair);
        StatusCoverFrame.Background = AppChrome.Brush(AppChrome.Surface);
        StatusTextBlock.Foreground = AppChrome.Brush(AppChrome.Muted);
        StatusSpeedTextBlock.Foreground = AppChrome.Brush(AppChrome.Muted);

        var libraryView = new LibraryView();
        var emulationView = new EmulationView();
        ContentHost.Content = libraryView;

        _framePresenter = new LcdFramePresenter(emulationView.Screen);

        var statusGrid = (Grid)StatusTextBlock.Parent!;
        StatusCoverFrame.IsVisible = false;
        statusGrid.ColumnSpacing = 8;

        var statusHardwareBadgeTextBlock = new TextBlock
        {
            Foreground = AppChrome.Brush(AppChrome.Muted),
            FontSize = 10,
            FontWeight = FontWeight.SemiBold,
        };
        var statusHardwareBadge = new Border
        {
            BorderBrush = AppChrome.Brush(AppChrome.Hair),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(5, 1),
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
            Child = statusHardwareBadgeTextBlock,
        };
        Grid.SetColumn(statusHardwareBadge, 2);
        statusGrid.Children.Add(statusHardwareBadge);

        statusGrid.Children.Remove(StatusSpeedTextBlock);
        StatusSpeedTextBlock.FontSize = 10;
        StatusSpeedTextBlock.FontWeight = FontWeight.SemiBold;
        var statusSpeedBadge = new Border
        {
            BorderBrush = AppChrome.Brush(AppChrome.Hair),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(5, 1),
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
            Child = StatusSpeedTextBlock,
        };
        Grid.SetColumn(statusSpeedBadge, 3);
        statusGrid.Children.Add(statusSpeedBadge);

        _statusBar = new StatusBarPresenter(
            StatusTextBlock,
            StatusCoverFrame,
            StatusCoverImage,
            statusHardwareBadge,
            statusHardwareBadgeTextBlock,
            statusSpeedBadge,
            StatusSpeedTextBlock
        );
        _operationRunner = new ShellOperationRunner(
            exception => _statusBar.ShowError(exception.Message),
            logger
        );

        SetStatusBarAvailable(false);

        var emulationController = new EmulationController(
            startupConfiguration.BootRomOptions,
            audioOutput,
            cartridgeSaveFileService,
            OnFrameCompleted,
            OnEmulationFaulted,
            startupConfiguration.EmulationConfig.FastForwardEnabled,
            startupConfiguration.EmulationConfig.FastForwardSpeed
        );
        _emulationSession = new EmulationSessionPresenter(
            emulationController,
            new InputRouter(inputMap.Bindings, emulationController.SetButtonState),
            libraryService,
            configurationService,
            _statusBar,
            MainMenu,
            _operationRunner
        );

        var libraryPresenter = new LibraryPresenter(
            libraryView,
            libraryService,
            _operationRunner,
            StorageProvider,
            path => _emulationSession.OpenRecentRomAsync(StorageProvider, path)
        );

        _emulationSession.SessionOpened += (_, _) =>
        {
            ContentHost.Content = emulationView;
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
            _emulationSession.SetBootRomOptions
        );

        ConfigureMenu();
        _emulationSession.AttachDragDrop(this);
        libraryPresenter.Refresh();

        if (startupConfiguration.StartupErrorMessage is not null)
        {
            _statusBar.ShowError(startupConfiguration.StartupErrorMessage);
        }
    }

    private void ConfigureMenu()
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
        MainMenu.PauseRequested += (_, _) => _emulationSession.TogglePause();
        MainMenu.ResetRequested += (_, _) => _operationRunner.Run(_emulationSession.ResetAsync);
        MainMenu.FastForwardRequested += (_, _) => _emulationSession.ToggleFastForward();
        MainMenu.FastForwardSpeedSelected += (_, e) =>
            _emulationSession.SetFastForwardSpeed(e.Speed);
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
        MainMenu.SetFullscreenState(WindowState is WindowState.FullScreen);
        MainMenu.SetMenuBarState(_menuBarVisibleWhenAvailable);
        MainMenu.SetStatusBarAvailability(_statusBarAvailable);
        MainMenu.SetStatusBarState(StatusBar.IsVisible);
    }

    private void SyncFullscreenState(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property != WindowStateProperty || MainMenu is null)
        {
            return;
        }

        ApplyMenuBarVisibility();
        MainMenu.SetFullscreenState(WindowState is WindowState.FullScreen);
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
        MainMenu.SetStatusBarAvailability(_statusBarAvailable);
        MainMenu.SetStatusBarState(StatusBar.IsVisible);
    }

    private void ApplyMenuBarVisibility()
    {
        MainMenu.IsVisible =
            !OperatingSystem.IsMacOS()
            && _menuBarVisibleWhenAvailable
            && WindowState is not WindowState.FullScreen;
        MainMenu.SetMenuBarState(_menuBarVisibleWhenAvailable);
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

        if (Interlocked.Exchange(ref _closeStopStarted, 1) == 0)
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
        _statusBar.Dispose();
        _framePresenter.Dispose();
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

    private async Task StopAndCloseAsync()
    {
        try
        {
            await _emulationSession.StopAsync().ConfigureAwait(true);
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
            Volatile.Write(ref _closeStopStarted, 0);
        }
    }

    private void ApplyKeyboardEvent(KeyEventArgs e, bool pressed)
    {
        if (
            (pressed && MainMenu.TryHandleShortcut(e.Key, e.KeyModifiers))
            || (pressed && TryHandleChromeShortcut(e.Key, e.KeyModifiers))
            || _emulationSession.ApplyKeyboardInput(e.Key, pressed)
        )
        {
            e.Handled = true;
        }
    }

    private void OnFrameCompleted(FrameCompletedEventArgs e)
    {
        _framePresenter.Enqueue(e.Frame);
    }

    private void OnEmulationFaulted(Exception e) => _emulationSession.ShowFault(e);
}
