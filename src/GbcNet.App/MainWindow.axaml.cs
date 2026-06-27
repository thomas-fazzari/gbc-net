using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using GbcNet.App.Audio;
using GbcNet.App.Chrome;
using GbcNet.App.Configuration;
using GbcNet.App.Emulation;
using GbcNet.App.Input;
using GbcNet.App.Rendering;
using GbcNet.App.Saves;
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
    private bool _closeAfterAsyncStop;
    private int _closeStopStarted;

    public MainWindow(
        InputMap inputMap,
        StartupConfiguration startupConfiguration,
        AppConfigurationService configurationService,
        CartridgeBatterySaveFileService cartridgeSaveFileService,
        IAudioOutput audioOutput,
        ILogger<MainWindow> logger
    )
    {
        InitializeComponent();

        _framePresenter = new LcdFramePresenter(ScreenImage);

        _statusBar = new StatusBarPresenter(StatusTextBlock, StatusMetricsTextBlock);
        _operationRunner = new ShellOperationRunner(
            exception => _statusBar.ShowError(exception.Message),
            logger
        );

        var emulationController = new EmulationController(
            startupConfiguration.BootRomOptions,
            audioOutput,
            cartridgeSaveFileService,
            OnFrameCompleted,
            OnEmulationMetricsUpdated,
            OnEmulationFaulted
        );
        _emulationSession = new EmulationSessionPresenter(
            emulationController,
            new InputRouter(inputMap.Bindings, emulationController.SetButtonState),
            _statusBar,
            MainMenu,
            _operationRunner
        );

        _configurationPresenter = new ConfigurationPresenter(
            configurationService,
            startupConfiguration.ConfigPath,
            _statusBar,
            _emulationSession.SetBootRomOptions
        );

        ConfigureMenu();
        _emulationSession.AttachDragDrop(this);

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
        MainMenu.CloseRequested += (_, _) => Close();
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
        MainMenu.StatusBarRequested += (_, _) => ToggleStatusBar();
        MainMenu.SetFullscreenState(isFullscreen: false);
        MainMenu.SetStatusBarState(isVisible: true);
        _emulationSession.SyncMenuState();
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

        if (change.Property == WindowStateProperty && MainMenu is not null)
        {
            MainMenu.SetFullscreenState(WindowState is WindowState.FullScreen);
        }
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

    private void ToggleFullscreen()
    {
        WindowState =
            WindowState is WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
    }

    private void ToggleStatusBar()
    {
        StatusBar.IsVisible = !StatusBar.IsVisible;
        MainMenu.SetStatusBarState(StatusBar.IsVisible);
    }

    private void ApplyKeyboardEvent(KeyEventArgs e, bool pressed)
    {
        if (
            (pressed && TryHandleAppShortcut(e))
            || _emulationSession.ApplyKeyboardInput(e.Key, pressed)
        )
        {
            e.Handled = true;
        }
    }

    private bool TryHandleAppShortcut(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter when e.KeyModifiers.HasFlag(KeyModifiers.Alt):
                ToggleFullscreen();
                return true;

            case Key.I
                when e.KeyModifiers.HasFlag(
                    OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control
                ):
                ToggleStatusBar();
                return true;

            default:
                return false;
        }
    }

    private void OnFrameCompleted(FrameCompletedEventArgs e)
    {
        _framePresenter.Enqueue(e.Frame);
    }

    private void OnEmulationFaulted(Exception e)
    {
        _emulationSession.ShowFault(e);
    }

    private void OnEmulationMetricsUpdated(EmulationMetrics metrics)
    {
        _emulationSession.ShowMetrics(metrics);
    }
}
