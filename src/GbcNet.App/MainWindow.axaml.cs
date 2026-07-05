// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

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
    private readonly WindowChromePresenter _windowChrome;
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

        var libraryView = new LibraryView();
        var emulationView = new EmulationView();
        ContentHost.Content = libraryView;

        _framePresenter = new LcdFramePresenter(emulationView.Screen);

        var statusGrid = (Grid)StatusTextBlock.Parent!;
        statusGrid.ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto");
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
        Grid.SetColumn(statusHardwareBadge, 1);
        statusGrid.Children.Add(statusHardwareBadge);

        statusGrid.Children.Remove(StatusSpeedTextBlock);
        StatusSpeedTextBlock.Foreground = AppChrome.Brush(AppChrome.Muted);
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
        Grid.SetColumn(statusSpeedBadge, 2);
        statusGrid.Children.Add(statusSpeedBadge);

        _statusBar = new StatusBarPresenter(
            StatusTextBlock,
            statusHardwareBadge,
            statusHardwareBadgeTextBlock,
            statusSpeedBadge,
            StatusSpeedTextBlock
        );
        _operationRunner = new ShellOperationRunner(
            exception => _statusBar.ShowError(exception.Message),
            logger
        );

        _windowChrome = new WindowChromePresenter(this, StatusBar, MainMenu);
        _windowChrome.SetStatusBarAvailable(false);

        var emulationController = new EmulationController(
            startupConfiguration.BootRomOptions,
            audioOutput,
            cartridgeSaveFileService,
            OnFrameCompleted,
            OnEmulationFaulted
        );
        _emulationSession = new EmulationSessionPresenter(
            emulationController,
            new InputRouter(inputMap.Bindings, emulationController.SetButtonState),
            libraryService,
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
            _windowChrome.SetStatusBarAvailable(isAvailable: true);
        };
        _emulationSession.SessionClosed += (_, _) =>
        {
            ContentHost.Content = libraryView;
            _windowChrome.SetStatusBarAvailable(isAvailable: false);
            libraryPresenter.Refresh();
        };
        _emulationSession.SessionFaulted += (_, _) =>
        {
            ContentHost.Content = libraryView;
            _windowChrome.SetStatusBarAvailable(isAvailable: true);
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
        MainMenu.FullscreenRequested += (_, _) => _windowChrome.ToggleFullscreen();
        MainMenu.StatusBarRequested += (_, _) => _windowChrome.ToggleStatusBar();
        _windowChrome.SyncMenuState();
        _emulationSession.SyncMenuState();
        _emulationSession.SyncRecentRoms();
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

        // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
        _windowChrome?.SyncFullscreenState(change);
    }

    protected override void OnClosed(EventArgs e)
    {
        Dispose();
        base.OnClosed(e);
    }

    public void Dispose()
    {
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
            (pressed && _windowChrome.TryHandleShortcut(e.Key, e.KeyModifiers))
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
