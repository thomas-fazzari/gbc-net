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
using GbcNet.Gui.Emulation;
using GbcNet.Gui.Input;
using GbcNet.Gui.Input.Options;
using GbcNet.Gui.Rendering;

namespace GbcNet.Gui;

internal sealed partial class MainWindow : Window, IDisposable
{
    private static readonly FilePickerFileType _gameBoyRomFileType = new("Game Boy ROM")
    {
        Patterns = ["*.gb", "*.gbc"],
        AppleUniformTypeIdentifiers = ["public.data"],
        MimeTypes = ["application/octet-stream"],
    };

    private EmulationSession? _emulationSession;
    private readonly KeyboardInputMapper _keyboardInputMapper;
    private readonly InputRouter _inputRouter;
    private WriteableBitmap? _screenBitmap;

    public MainWindow()
    {
        InitializeComponent();
        Result<InputConfiguration> inputConfiguration = InputConfiguration.FromOptions(
            InputOptions.CreateDefault()
        );
        _keyboardInputMapper = new KeyboardInputMapper(inputConfiguration.Value.Bindings);
        _inputRouter = new InputRouter(
            inputConfiguration.Value.Bindings,
            (button, pressed) => _emulationSession?.SetButtonState(button, pressed)
        );
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
        Result<Cartridge> cartridge = Cartridge.Load(rom);

        if (cartridge.IsFailed)
        {
            Title = string.Join(
                Environment.NewLine,
                cartridge.Errors.Select(error => error.Message)
            );
            return;
        }

        StartEmulation(cartridge.Value, files[0].Name);
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

    private void StartEmulation(Cartridge cartridge, string romName)
    {
        StopEmulationSession();
        _inputRouter.Clear();
        _emulationSession = new EmulationSession(
            new GameBoy(cartridge, HardwareModel.Dmg),
            OnFrameCompleted,
            OnEmulationFaulted
        );
        Title = romName;
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
