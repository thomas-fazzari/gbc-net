using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
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
    private static readonly FilePickerFileType _gameBoyRomFileType = new("Game Boy ROM")
    {
        Patterns = ["*.gb", "*.gbc"],
        AppleUniformTypeIdentifiers = ["public.data"],
        MimeTypes = ["application/x-gameboy-rom", "application/x-gameboy-color-rom"],
    };

    private readonly AppConfigurationService _configurationService;
    private readonly string _configurationPath;
    private readonly EmulationController _emulationController;
    private readonly LcdFramePresenter _framePresenter;
    private readonly InputRouter _inputRouter;
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
        _emulationController = new EmulationController(
            startupConfiguration.BootRomOptions,
            audioOutput,
            cartridgeSaveFileService,
            OnFrameCompleted,
            OnEmulationMetricsUpdated,
            OnEmulationFaulted
        );
        _inputRouter = new InputRouter(inputMap.Bindings, _emulationController.SetButtonState);

        ConfigureMenu();
        ConfigureDragDrop();
        SyncEmulationMenuState();

        if (startupConfiguration.StartupErrorMessage is not null)
        {
            _statusBar.ShowError(startupConfiguration.StartupErrorMessage);
        }
    }

    private void ConfigureMenu()
    {
        MainMenu.AttachNativeMenu(this);
        MainMenu.OpenRomRequested += (_, _) => _operationRunner.Run(OpenRomAsync);
        MainMenu.CloseRequested += (_, _) => Close();
        MainMenu.ConfigurationRequested += (_, _) => _operationRunner.Run(OpenConfigurationAsync);
        MainMenu.ConfigurationFileLocationRequested += (_, _) =>
            _operationRunner.Run(OpenConfigurationDirectoryAsync);
        MainMenu.PauseRequested += (_, _) => TogglePause();
        MainMenu.ResetRequested += (_, _) => _operationRunner.Run(ResetRomAsync);
        MainMenu.FastForwardRequested += (_, _) => ToggleFastForward();
        MainMenu.FastForwardSpeedSelected += (_, e) => SetFastForwardSpeed(e.Speed);
        MainMenu.FullscreenRequested += (_, _) => ToggleFullscreen();
        MainMenu.StatusBarRequested += (_, _) => ToggleStatusBar();
        MainMenu.SetFullscreenState(isFullscreen: false);
        MainMenu.SetStatusBarState(isVisible: true);
    }

    private void ConfigureDragDrop()
    {
        DragDrop.SetAllowDrop(this, true);
        DragDrop.AddDragOverHandler(this, DragDrop_OnDragOver);
        DragDrop.AddDropHandler(this, DragDrop_OnDrop);
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
            _statusBar.ShowError(RomFileFilter.UnsupportedDroppedFileMessage);
            return;
        }

        _operationRunner.Run(() => OpenRomFileAsync(file));
    }

    private async Task StopAndCloseAsync()
    {
        try
        {
            await StopEmulationSessionAsync().ConfigureAwait(true);
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
            _emulationController.SetBootRomOptions(new BootRomOptions());
            _statusBar.ShowError(StatusBarPresenter.FormatErrors(bootRomOptions.Errors));
            return;
        }

        _emulationController.SetBootRomOptions(bootRomOptions.Value);
        _statusBar.ShowStatus("Configuration saved.");
    }

    private async Task OpenRomAsync()
    {
        var files = await StorageProvider
            .OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "Open Game Boy ROM",
                    AllowMultiple = false,
                    FileTypeFilter = [_gameBoyRomFileType],
                }
            )
            .ConfigureAwait(true);

        if (files.Count > 0)
        {
            await OpenRomFileAsync(files[0]).ConfigureAwait(true);
        }
    }

    private async Task OpenRomFileAsync(IStorageFile file)
    {
        _inputRouter.Clear();
        var result = await _emulationController.OpenRomFileAsync(file).ConfigureAwait(true);
        if (result.IsFailed)
        {
            _statusBar.ShowError(StatusBarPresenter.FormatErrors(result.Errors));
            SyncEmulationMenuState();
            return;
        }

        _statusBar.ShowRomFileName(result.Value.LoadedRomFileName);
        SyncEmulationMenuState();
    }

    private async Task ResetRomAsync()
    {
        _inputRouter.Clear();
        var result = await _emulationController.ResetAsync().ConfigureAwait(true);
        if (result.IsFailed)
        {
            _statusBar.ShowError(StatusBarPresenter.FormatErrors(result.Errors));
            SyncEmulationMenuState();
            return;
        }

        if (result.Value.HasSession)
        {
            _statusBar.ShowRomFileName(result.Value.LoadedRomFileName);
        }
        SyncEmulationMenuState();
    }

    private async Task StopEmulationSessionAsync()
    {
        await _emulationController.StopAsync().ConfigureAwait(true);
        _inputRouter.Clear();
        SyncEmulationMenuState();
    }

    private void TogglePause()
    {
        _emulationController.TogglePause();
        SyncEmulationMenuState();
    }

    private void ToggleFastForward()
    {
        _emulationController.ToggleFastForward();
        SyncEmulationMenuState();
    }

    private void SetFastForwardSpeed(EmulationSpeed speed)
    {
        _emulationController.SetFastForwardSpeed(speed);
        SyncEmulationMenuState();
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
        if (pressed && TryHandleAppShortcut(e))
        {
            return;
        }

        if (!_emulationController.State.HasSession)
        {
            return;
        }

        if (pressed && e.Key is Key.Tab)
        {
            ToggleFastForward();
            e.Handled = true;
            return;
        }

        e.Handled = _inputRouter.Apply(e.Key, pressed);
    }

    private bool TryHandleAppShortcut(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter when e.KeyModifiers.HasFlag(KeyModifiers.Alt):
                ToggleFullscreen();
                e.Handled = true;
                return true;

            case Key.I
                when e.KeyModifiers.HasFlag(
                    OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control
                ):
                ToggleStatusBar();
                e.Handled = true;
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
        Dispatcher.UIThread.Post(() => _statusBar.ShowError(e.Message));
    }

    private void OnEmulationMetricsUpdated(EmulationMetrics metrics)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_emulationController.State.HasSession)
            {
                _statusBar.ShowMetrics(metrics.SpeedMultiplier, metrics.RenderedFramesPerSecond);
            }
        });
    }

    private void SyncEmulationMenuState()
    {
        var state = _emulationController.State;
        MainMenu.SetEmulationActionsEnabled(state.HasSession);
        MainMenu.SetPauseState(state.HasSession, state.IsPaused);
        MainMenu.SetFastForwardState(state.FastForwardEnabled, state.FastForwardSpeed);
    }
}
