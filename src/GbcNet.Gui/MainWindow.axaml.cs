using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FluentResults;
using GbcNet.Core;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Hardware;
using GbcNet.Core.Ppu;
using GbcNet.Gui.Configuration;
using GbcNet.Gui.Emulation;
using GbcNet.Gui.Input;
using GbcNet.Gui.Rendering;
using GbcNet.Gui.Saves;

namespace GbcNet.Gui;

internal sealed partial class MainWindow : Window, IDisposable
{
    private static readonly FilePickerFileType _gameBoyRomFileType = new("Game Boy ROM")
    {
        Patterns = ["*.gb", "*.gbc"],
        AppleUniformTypeIdentifiers = ["public.data"],
        MimeTypes = ["application/octet-stream"],
    };

    private readonly NativeMenuItem _pauseMenuItem = new("Pause")
    {
        Gesture = KeyGesture.Parse("Space"),
        IsEnabled = false,
    };
    private readonly NativeMenuItem _resetMenuItem = new("Reset")
    {
        Gesture = KeyGesture.Parse("Meta+R"),
        IsEnabled = false,
    };
    private EmulationSession? _emulationSession;
    private readonly KeyboardInputMapper _keyboardInputMapper;
    private readonly CartridgeSaveFileService _cartridgeSaveFileService;
    private readonly InputRouter _inputRouter;
    private byte[]? _loadedRom;
    private string _loadedRomName = string.Empty;
    private WriteableBitmap? _screenBitmap;

    public MainWindow(
        InputConfiguration inputConfiguration,
        StartupConfiguration startupConfiguration,
        CartridgeSaveFileService cartridgeSaveFileService
    )
    {
        InitializeComponent();
        ConfigureNativeMenu();

        if (startupConfiguration.StartupMessage is not null)
        {
            Title = startupConfiguration.StartupMessage;
        }

        _cartridgeSaveFileService = cartridgeSaveFileService;
        _keyboardInputMapper = new KeyboardInputMapper(inputConfiguration.Bindings);
        _inputRouter = new InputRouter(
            inputConfiguration.Bindings,
            (button, pressed) => _emulationSession?.SetButtonState(button, pressed)
        );
    }

    private void ConfigureNativeMenu()
    {
        var openMenuItem = new NativeMenuItem("Open ROM...")
        {
            Gesture = KeyGesture.Parse("Meta+O"),
        };
        openMenuItem.Click += OpenRomMenu_OnClick;

        var closeMenuItem = new NativeMenuItem("Close") { Gesture = KeyGesture.Parse("Meta+W") };
        closeMenuItem.Click += CloseMenu_OnClick;

        _pauseMenuItem.Click += PauseMenu_OnClick;
        _resetMenuItem.Click += ResetMenu_OnClick;

        var fileMenuItem = new NativeMenuItem("File")
        {
            Menu = [openMenuItem, new NativeMenuItemSeparator(), closeMenuItem],
        };
        var emulationMenuItem = new NativeMenuItem("Emulation")
        {
            Menu = [_pauseMenuItem, _resetMenuItem],
        };

        NativeMenu.SetMenu(this, [fileMenuItem, emulationMenuItem]);
    }

    protected override void OnClosed(EventArgs e)
    {
        Dispose();
        base.OnClosed(e);
    }

    public void Dispose()
    {
        StopEmulationSession();
        _screenBitmap?.Dispose();
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
        _pauseMenuItem.Header = _emulationSession.IsPaused ? "Resume" : "Pause";
    }

    private void ResetMenu_OnClick(object? sender, EventArgs e)
    {
        if (_loadedRom is null)
        {
            return;
        }

        StopEmulationSession();
        Result<Cartridge> cartridge = LoadCartridge(_loadedRom);

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
        IReadOnlyList<IStorageFile> files = await StorageProvider
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

        byte[] rom = await ReadFileAsync(files[0]).ConfigureAwait(true);
        StopEmulationSession();
        Result<Cartridge> cartridge = LoadCartridge(rom);

        if (cartridge.IsFailed)
        {
            Title = string.Join(
                Environment.NewLine,
                cartridge.Errors.Select(error => error.Message)
            );
            return;
        }

        _loadedRom = rom;
        _loadedRomName = files[0].Name;
        StartEmulation(cartridge.Value, rom, files[0].Name);
    }

    private static async Task<byte[]> ReadFileAsync(IStorageFile file)
    {
        Stream stream = await file.OpenReadAsync().ConfigureAwait(false);
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
        Result<Cartridge> cartridge = Cartridge.Load(rom);
        if (cartridge.IsFailed)
        {
            return cartridge;
        }

        Result save = _cartridgeSaveFileService.Load(cartridge.Value, rom);
        return save.IsFailed ? Result.Fail<Cartridge>(save.Errors) : cartridge;
    }

    private void StartEmulation(Cartridge cartridge, byte[] rom, string romName)
    {
        _inputRouter.Clear();
        _emulationSession = new EmulationSession(
            new GameBoy(cartridge, HardwareModel.Dmg),
            OnFrameCompleted,
            OnEmulationFaulted,
            () => _cartridgeSaveFileService.Save(cartridge, rom)
        );
        Title = romName;
        _pauseMenuItem.Header = "Pause";
        _pauseMenuItem.IsEnabled = true;
        _resetMenuItem.IsEnabled = true;
    }

    private void StopEmulationSession()
    {
        if (_emulationSession is null)
        {
            return;
        }

        _emulationSession.Dispose();
        _emulationSession = null;
        _inputRouter.Clear();
        _pauseMenuItem.Header = "Pause";
        _pauseMenuItem.IsEnabled = false;
        _resetMenuItem.IsEnabled = false;
    }

    private void ApplyKeyboardInput(KeyEventArgs e, bool pressed)
    {
        if (_emulationSession is null)
        {
            return;
        }

        if (_keyboardInputMapper.TryMap(e.Key, out PhysicalInput input))
        {
            e.Handled = _inputRouter.Apply(input, pressed);
        }
    }

    private void OnFrameCompleted(FrameCompletedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _screenBitmap?.Dispose();
            _screenBitmap = LcdFrameBitmapConverter.ToBitmap(e.Frame);
            ScreenImage.Source = _screenBitmap;
        });
    }

    private void OnEmulationFaulted(Exception e)
    {
        Dispatcher.UIThread.Post(() => Title = e.Message);
    }
}
