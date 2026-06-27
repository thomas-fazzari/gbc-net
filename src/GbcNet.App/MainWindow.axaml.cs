using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using GbcNet.App.Audio;
using GbcNet.App.Chrome;
using GbcNet.App.Configuration;
using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.App.Emulation;
using GbcNet.App.Input;
using GbcNet.App.Rendering;
using GbcNet.App.Saves;
using GbcNet.Core;
using GbcNet.Core.Ppu;
using Microsoft.Extensions.Logging;

namespace GbcNet.App;

internal sealed partial class MainWindow : Window, IDisposable
{
    private readonly AppConfigurationService _configurationService;
    private readonly string _configurationPath;
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

        _configurationService = configurationService;
        _configurationPath = startupConfiguration.ConfigPath;
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
        MainMenu.ConfigurationRequested += (_, _) => _operationRunner.Run(OpenConfigurationAsync);
        MainMenu.ConfigurationFileLocationRequested += (_, _) =>
            _operationRunner.Run(OpenConfigurationDirectoryAsync);
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

    private async Task OpenConfigurationAsync()
    {
        var bootRomConfig = _configurationService.LoadBootRomConfig();
        if (bootRomConfig.IsFailed)
        {
            _statusBar.ShowError(StatusBarPresenter.FormatErrors(bootRomConfig.Errors));
            return;
        }

        var savedConfig = await new SettingsWindow(bootRomConfig.Value)
            .ShowDialog<BootRomConfig?>(this)
            .ConfigureAwait(true);
        if (savedConfig is null)
        {
            return;
        }

        var saved = _configurationService.SaveBootRomConfig(savedConfig.Value);
        if (saved.IsFailed)
        {
            _statusBar.ShowError(StatusBarPresenter.FormatErrors(saved.Errors));
            return;
        }

        ReloadBootRomOptions();
    }

    private Task OpenConfigurationDirectoryAsync()
    {
        var directoryPath = Path.GetDirectoryName(_configurationPath);

        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new InvalidOperationException("Configuration file path has no directory.");
        }

        Directory.CreateDirectory(directoryPath);

        using var process =
            Process.Start(new ProcessStartInfo { FileName = directoryPath, UseShellExecute = true })
            ?? throw new InvalidOperationException(
                "Configuration file location could not be opened."
            );

        return Task.CompletedTask;
    }

    private void ReloadBootRomOptions()
    {
        var bootRomOptions = _configurationService.LoadBootRomOptions();
        if (bootRomOptions.IsFailed)
        {
            _emulationSession.SetBootRomOptions(new BootRomOptions());
            _statusBar.ShowError(StatusBarPresenter.FormatErrors(bootRomOptions.Errors));
            return;
        }

        _emulationSession.SetBootRomOptions(bootRomOptions.Value);
        _statusBar.ShowStatus("Configuration saved.");
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
