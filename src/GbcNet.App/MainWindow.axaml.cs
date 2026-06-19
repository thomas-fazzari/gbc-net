using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FluentResults;
using GbcNet.App.Audio;
using GbcNet.App.Configuration;
using GbcNet.App.Configuration.Kdl;
using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.App.Emulation;
using GbcNet.App.Input;
using GbcNet.App.Menus;
using GbcNet.App.Rendering;
using GbcNet.App.Saves;
using GbcNet.Core;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Hardware;
using GbcNet.Core.Ppu;

namespace GbcNet.App;

internal sealed partial class MainWindow : Window, IDisposable
{
    private static readonly FilePickerFileType _gameBoyRomFileType = new("Game Boy ROM")
    {
        Patterns = ["*.gb", "*.gbc"],
        AppleUniformTypeIdentifiers = ["public.data"],
        MimeTypes = ["application/x-gameboy-rom", "application/x-gameboy-color-rom"],
    };

    private const string UnsupportedDroppedFileMessage = "Drop a .gb or .gbc ROM file.";

    private EmulationSession? _emulationSession;
    private readonly IAudioOutput _audioOutput;
    private readonly KeyboardInputMapper _keyboardInputMapper;
    private readonly CartridgeSaveFileService _cartridgeSaveFileService;
    private readonly InputRouter _inputRouter;
    private GameBoyOptions _gameBoyOptions;
    private readonly string _configPath;
    private byte[]? _loadedRom;
    private string _loadedRomName = string.Empty;
    private readonly LcdFramePresenter _framePresenter;
    private bool _closeAfterAsyncStop;
    private bool _fastForwardEnabled;
    private EmulationSpeed _fastForwardSpeed = EmulationSpeed.Two;
    private int _closeStopStarted;

    public MainWindow(
        InputConfiguration inputConfiguration,
        StartupConfiguration startupConfiguration,
        CartridgeSaveFileService cartridgeSaveFileService,
        IAudioOutput audioOutput
    )
    {
        InitializeComponent();
        _framePresenter = new LcdFramePresenter(ScreenImage);
        ConfigureMenu();
        ConfigureDragDrop();

        if (startupConfiguration.StartupMessage is not null)
        {
            ShowError(startupConfiguration.StartupMessage);
        }

        _cartridgeSaveFileService = cartridgeSaveFileService;
        _audioOutput = audioOutput;
        _gameBoyOptions = startupConfiguration.GameBoyOptions;
        _configPath = startupConfiguration.ConfigPath;
        _keyboardInputMapper = new KeyboardInputMapper(inputConfiguration.Bindings);
        _inputRouter = new InputRouter(
            inputConfiguration.Bindings,
            (button, pressed) => _emulationSession?.SetButtonState(button, pressed)
        );
    }

    private void ConfigureMenu()
    {
        MainMenu.AttachNativeMenu(this);
        MainMenu.OpenRomRequested += OpenRomMenu_OnClick;
        MainMenu.CloseRequested += CloseMenu_OnClick;
        MainMenu.ConfigurationRequested += ConfigurationMenu_OnClick;
        MainMenu.PauseRequested += PauseMenu_OnClick;
        MainMenu.ResetRequested += ResetMenu_OnClick;
        MainMenu.FastForwardRequested += FastForwardMenu_OnClick;
        MainMenu.FastForwardSpeedSelected += FastForwardSpeedMenu_OnSelected;
        MainMenu.FullscreenRequested += FullscreenMenu_OnClick;
        MainMenu.StatusBarRequested += StatusBarMenu_OnClick;
        MainMenu.SetFastForwardState(_fastForwardEnabled, _fastForwardSpeed);
        MainMenu.SetFullscreenState(isFullscreen: false);
        MainMenu.SetStatusBarState(isVisible: true);
    }

    private void ConfigureDragDrop()
    {
        DragDrop.SetAllowDrop(this, true);
        DragDrop.AddDragOverHandler(this, DragDrop_OnDragOver);
        DragDrop.AddDropHandler(this, DragDrop_OnDrop);
    }

    private void RunUiTask(Func<Task> action)
    {
        _ = RunUiTaskAsync(action);
    }

    private async Task RunUiTaskAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (Exception exception) when (IsExpectedUiException(exception))
        {
            ShowError(exception.Message);
        }
    }

    private const int StatusRomNameMaxLength = 72;

    private void ShowStatus(string message)
    {
        StatusTextBlock.Foreground = new SolidColorBrush(Color.Parse("#a1a1aa"));
        StatusTextBlock.Text = message;
        StatusMetricsTextBlock.Text = string.Empty;
    }

    private void ShowError(string message)
    {
        StatusTextBlock.Foreground = new SolidColorBrush(Color.Parse("#fca5a5"));
        StatusTextBlock.Text = message;
        StatusMetricsTextBlock.Text = string.Empty;
    }

    private void ShowMetrics(EmulationMetrics metrics)
    {
        StatusMetricsTextBlock.Text = string.Create(
            CultureInfo.InvariantCulture,
            $"{metrics.TargetSpeed:0.#}x | {metrics.DisplayFramesPerSecond:0} fps"
        );
    }

    private static string FormatRomName(string romName) =>
        romName.Length <= StatusRomNameMaxLength
            ? romName
            : $"{romName.AsSpan(0, StatusRomNameMaxLength - 1)}…";

    private static string FormatErrors(IEnumerable<IError> errors) =>
        string.Join(Environment.NewLine, errors.Select(static error => error.Message));

    private static bool IsExpectedUiException(Exception exception) =>
        exception
            is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or NotSupportedException
                or ArgumentException;

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
            RunUiTask(StopAndCloseAsync);
        }
    }

    private async Task StopAndCloseAsync()
    {
        try
        {
            await StopEmulationSessionAsync().ConfigureAwait(true);
            _closeAfterAsyncStop = true;
            Close();
        }
        catch (Exception exception) when (IsExpectedUiException(exception))
        {
            ShowError(exception.Message);
            Volatile.Write(ref _closeStopStarted, 0);
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
        ApplyKeyboardInput(e, pressed: true);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        ApplyKeyboardInput(e, pressed: false);
    }

    private static void DragDrop_OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Formats.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void DragDrop_OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;

        var file = GetFirstDroppedRom(e.DataTransfer.TryGetFiles());

        if (file is null)
        {
            ShowError(UnsupportedDroppedFileMessage);
            return;
        }

        RunUiTask(() => OpenRomFileAsync(file));
    }

    private static IStorageFile? GetFirstDroppedRom(IEnumerable<IStorageItem>? items)
    {
        if (items is null)
        {
            return null;
        }

        foreach (var item in items)
        {
            if (item is IStorageFile file && IsRomFileName(file.Name))
            {
                return file;
            }
        }

        return null;
    }

    private static bool IsRomFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName);

        return extension.Equals(".gb", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".gbc", StringComparison.OrdinalIgnoreCase);
    }

    private void OpenRomMenu_OnClick(object? sender, EventArgs e)
    {
        RunUiTask(OpenRomAsync);
    }

    private void ConfigurationMenu_OnClick(object? sender, EventArgs e)
    {
        RunUiTask(OpenConfigurationAsync);
    }

    private async Task OpenConfigurationAsync()
    {
        var document = KdlConfigurationFile.LoadOrCreate(_configPath);
        if (document.IsFailed)
        {
            ShowError(FormatErrors(document.Errors));
            return;
        }

        var pathOptions = BootRomOptionsReader.ReadPaths(document.Value);
        if (pathOptions.IsFailed)
        {
            ShowError(FormatErrors(pathOptions.Errors));
            return;
        }

        var savedOptions = await new SettingsWindow(pathOptions.Value)
            .ShowDialog<BootRomPathOptions?>(this)
            .ConfigureAwait(true);
        if (savedOptions is null)
        {
            return;
        }

        var saved = BootRomOptionsWriter.Write(_configPath, savedOptions.Value);
        if (saved.IsFailed)
        {
            ShowError(FormatErrors(saved.Errors));
            return;
        }

        ReloadGameBoyOptions();
    }

    private void ReloadGameBoyOptions()
    {
        var document = KdlConfigurationFile.LoadOrCreate(_configPath);
        if (document.IsFailed)
        {
            _gameBoyOptions = new GameBoyOptions();
            ShowError(FormatErrors(document.Errors));
            return;
        }

        var options = BootRomOptionsReader.Read(
            document.Value,
            Path.GetDirectoryName(_configPath) ?? Environment.CurrentDirectory
        );
        if (options.IsFailed)
        {
            _gameBoyOptions = new GameBoyOptions();
            ShowError(FormatErrors(options.Errors));
            return;
        }

        _gameBoyOptions = options.Value;
        ShowStatus("Configuration saved.");
    }

    private void CloseMenu_OnClick(object? sender, EventArgs e)
    {
        Close();
    }

    private void PauseMenu_OnClick(object? sender, EventArgs e)
    {
        if (_emulationSession is null)
        {
            return;
        }

        _emulationSession.IsPaused = !_emulationSession.IsPaused;
        MainMenu.SetPauseState(isEnabled: true, _emulationSession.IsPaused);
    }

    private void FastForwardSpeedMenu_OnSelected(
        object? sender,
        FastForwardSpeedSelectedEventArgs e
    )
    {
        SetFastForwardSpeed(e.Speed);
    }

    private void FullscreenMenu_OnClick(object? sender, EventArgs e)
    {
        ToggleFullscreen();
    }

    private void StatusBarMenu_OnClick(object? sender, EventArgs e)
    {
        StatusBar.IsVisible = !StatusBar.IsVisible;
        MainMenu.SetStatusBarState(StatusBar.IsVisible);
    }

    private void ResetMenu_OnClick(object? sender, EventArgs e)
    {
        RunUiTask(ResetRomAsync);
    }

    private void FastForwardMenu_OnClick(object? sender, EventArgs e)
    {
        _fastForwardEnabled = !_fastForwardEnabled;
        ApplyFastForwardSettings();
    }

    private void SetFastForwardSpeed(EmulationSpeed speed)
    {
        _fastForwardSpeed = speed;
        ApplyFastForwardSettings();
    }

    private void ToggleFullscreen()
    {
        WindowState =
            WindowState is WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
    }

    private async Task ResetRomAsync()
    {
        if (_loadedRom is null)
        {
            return;
        }

        await StopEmulationSessionAsync().ConfigureAwait(true);
        var cartridge = LoadCartridge(_loadedRom);

        if (cartridge.IsFailed)
        {
            ShowError(FormatErrors(cartridge.Errors));
            return;
        }

        StartEmulation(cartridge.Value, _loadedRom, _loadedRomName);
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

        if (files.Count == 0)
        {
            return;
        }

        await OpenRomFileAsync(files[0]).ConfigureAwait(true);
    }

    private async Task OpenRomFileAsync(IStorageFile file)
    {
        var rom = await ReadFileAsync(file).ConfigureAwait(true);
        await StopEmulationSessionAsync().ConfigureAwait(true);
        var cartridge = LoadCartridge(rom);

        if (cartridge.IsFailed)
        {
            ShowError(FormatErrors(cartridge.Errors));
            return;
        }

        _loadedRom = rom;
        _loadedRomName = file.Name;
        StartEmulation(cartridge.Value, rom, file.Name);
    }

    private static async Task<byte[]> ReadFileAsync(IStorageFile file)
    {
        var stream = await file.OpenReadAsync().ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            var memoryStream = new MemoryStream();
            await using (memoryStream.ConfigureAwait(false))
            {
                await stream
                    .CopyToAsync(memoryStream, CancellationToken.None)
                    .ConfigureAwait(false);
                return memoryStream.ToArray();
            }
        }
    }

    private Result<Cartridge> LoadCartridge(byte[] rom)
    {
        var cartridge = Cartridge.Load(rom);
        if (cartridge.IsFailed)
        {
            return cartridge;
        }

        var save = _cartridgeSaveFileService.Load(cartridge.Value, rom);
        return save.IsFailed ? Result.Fail<Cartridge>(save.Errors) : cartridge;
    }

    private void StartEmulation(Cartridge cartridge, byte[] rom, string romName)
    {
        var hardwareModel = cartridge.Header.CgbSupport
            is CgbSupport.Required
                or CgbSupport.Enhanced
            ? HardwareModel.Cgb
            : HardwareModel.Dmg;

        _inputRouter.Clear();
        _emulationSession = new EmulationSession(
            new GameBoy(cartridge, hardwareModel, _gameBoyOptions),
            _audioOutput,
            OnFrameCompleted,
            OnEmulationMetricsUpdated,
            OnEmulationFaulted,
            () => _cartridgeSaveFileService.Save(cartridge, rom)
        );
        ApplyFastForwardSettings();
        ShowStatus(FormatRomName(romName));
        MainMenu.SetEmulationActionsEnabled(isEnabled: true);
    }

    private async Task StopEmulationSessionAsync()
    {
        var session = _emulationSession;

        if (session is null)
        {
            return;
        }

        _emulationSession = null;
        _inputRouter.Clear();
        MainMenu.SetEmulationActionsEnabled(isEnabled: false);
        await session.StopAsync().ConfigureAwait(true);
    }

    private void ApplyKeyboardInput(KeyEventArgs e, bool pressed)
    {
        if (pressed && e.Key is Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            ToggleFullscreen();
            e.Handled = true;
            return;
        }

        if (_emulationSession is null)
        {
            return;
        }

        if (pressed && e.Key is Key.Tab)
        {
            _fastForwardEnabled = !_fastForwardEnabled;
            ApplyFastForwardSettings();
            e.Handled = true;
            return;
        }

        if (_keyboardInputMapper.TryMap(e.Key, out var input))
        {
            e.Handled = _inputRouter.Apply(input, pressed);
        }
    }

    private void OnFrameCompleted(FrameCompletedEventArgs e)
    {
        _framePresenter.Enqueue(e.Frame);
    }

    private void OnEmulationFaulted(Exception e)
    {
        Dispatcher.UIThread.Post(() => ShowError(e.Message));
    }

    private void OnEmulationMetricsUpdated(EmulationMetrics metrics)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_emulationSession is null)
            {
                return;
            }

            ShowMetrics(metrics);
        });
    }

    private void ApplyFastForwardSettings()
    {
        MainMenu.SetFastForwardState(_fastForwardEnabled, _fastForwardSpeed);
        _emulationSession?.SetFastForward(_fastForwardEnabled, _fastForwardSpeed);
    }
}
