using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FluentResults;
using GbcNet.Core;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Hardware;
using GbcNet.Core.Ppu;
using GbcNet.Gui.Audio;
using GbcNet.Gui.Configuration;
using GbcNet.Gui.Emulation;
using GbcNet.Gui.Input;
using GbcNet.Gui.Menus;
using GbcNet.Gui.Rendering;
using GbcNet.Gui.Saves;

namespace GbcNet.Gui;

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
    private byte[]? _loadedRom;
    private string _loadedRomName = string.Empty;
    private readonly LcdFrameBitmapRenderer _screenRenderer = new();
    private LcdFrame? _pendingFrame;
    private bool _closeAfterAsyncStop;
    private bool _fastForwardEnabled;
    private EmulationSpeed _fastForwardSpeed = EmulationSpeed.Two;
    private int _closeStopStarted;
    private int _isFrameRenderQueued;

    public MainWindow(
        InputConfiguration inputConfiguration,
        StartupConfiguration startupConfiguration,
        CartridgeSaveFileService cartridgeSaveFileService,
        IAudioOutput audioOutput
    )
    {
        InitializeComponent();
        ConfigureMenu();
        ConfigureDragDrop();

        if (startupConfiguration.StartupMessage is not null)
        {
            Title = startupConfiguration.StartupMessage;
        }

        _cartridgeSaveFileService = cartridgeSaveFileService;
        _audioOutput = audioOutput;
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
        MainMenu.PauseRequested += PauseMenu_OnClick;
        MainMenu.ResetRequested += ResetMenu_OnClick;
        MainMenu.FastForwardRequested += FastForwardMenu_OnClick;
        MainMenu.FastForwardSpeedSelected += FastForwardSpeedMenu_OnSelected;
        MainMenu.FullscreenRequested += FullscreenMenu_OnClick;
        MainMenu.SetFastForwardState(_fastForwardEnabled, _fastForwardSpeed);
        MainMenu.SetFullscreenState(isFullscreen: false);
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
            _ = StopEmulationSessionAsync()
                .ContinueWith(
                    static (task, state) =>
                    {
                        var window = (MainWindow)state!;

                        if (task.IsFaulted)
                        {
                            window.Title = task.Exception!.GetBaseException().Message;
                            Volatile.Write(ref window._closeStopStarted, 0);
                            return;
                        }

                        window._closeAfterAsyncStop = true;
                        window.Close();
                    },
                    this,
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.FromCurrentSynchronizationContext()
                );
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
        _screenRenderer.Dispose();
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
            Title = UnsupportedDroppedFileMessage;
            return;
        }

        _ = OpenRomFileAsync(file)
            .ContinueWith(
                static (task, state) =>
                {
                    if (task.Exception is not null)
                    {
                        ((MainWindow)state!).Title = task.Exception.GetBaseException().Message;
                    }
                },
                this,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.FromCurrentSynchronizationContext()
            );
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
        _ = OpenRomAsync()
            .ContinueWith(
                static (task, state) =>
                {
                    if (task.Exception is not null)
                    {
                        ((MainWindow)state!).Title = task.Exception.GetBaseException().Message;
                    }
                },
                this,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.FromCurrentSynchronizationContext()
            );
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

    private void ResetMenu_OnClick(object? sender, EventArgs e)
    {
        _ = ResetRomAsync()
            .ContinueWith(
                static (task, state) =>
                {
                    if (task.Exception is not null)
                    {
                        ((MainWindow)state!).Title = task.Exception.GetBaseException().Message;
                    }
                },
                this,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.FromCurrentSynchronizationContext()
            );
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
            Title = string.Join(
                Environment.NewLine,
                cartridge.Errors.Select(error => error.Message)
            );
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
            Title = string.Join(
                Environment.NewLine,
                cartridge.Errors.Select(error => error.Message)
            );
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
            new GameBoy(cartridge, hardwareModel),
            _audioOutput,
            OnFrameCompleted,
            OnEmulationMetricsUpdated,
            OnEmulationFaulted,
            () => _cartridgeSaveFileService.Save(cartridge, rom)
        );
        ApplyFastForwardSettings();
        Title = romName;
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

        if (pressed && e.Key == Key.Tab)
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
        Interlocked.Exchange(ref _pendingFrame, e.Frame);

        if (Interlocked.Exchange(ref _isFrameRenderQueued, 1) == 0)
        {
            Dispatcher.UIThread.Post(RenderPendingFrame);
        }
    }

    private void RenderPendingFrame()
    {
        var frame = Interlocked.Exchange(location1: ref _pendingFrame, value: null);
        Volatile.Write(location: ref _isFrameRenderQueued, value: 0);

        if (frame is not null)
        {
            ScreenImage.Source = _screenRenderer.Render(frame: frame);
        }

        // Keep latest frame only; fast-forward must not build a Dispatcher backlog.
        if (
            Interlocked.CompareExchange(location1: ref _pendingFrame, value: null, comparand: null)
                is not null
            && Interlocked.Exchange(location1: ref _isFrameRenderQueued, value: 1) == 0
        )
        {
            Dispatcher.UIThread.Post(action: RenderPendingFrame);
        }
    }

    private void OnEmulationFaulted(Exception e)
    {
        Dispatcher.UIThread.Post(() => Title = e.Message);
    }

    private void OnEmulationMetricsUpdated(EmulationMetrics metrics)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_emulationSession is null)
            {
                return;
            }

            Title = string.Create(
                CultureInfo.InvariantCulture,
                $"{_loadedRomName} | {metrics.TargetSpeed:0.#}x | {metrics.DisplayFramesPerSecond:0} fps"
            );
        });
    }

    private void ApplyFastForwardSettings()
    {
        MainMenu.SetFastForwardState(_fastForwardEnabled, _fastForwardSpeed);
        _emulationSession?.SetFastForward(_fastForwardEnabled, _fastForwardSpeed);
    }
}
