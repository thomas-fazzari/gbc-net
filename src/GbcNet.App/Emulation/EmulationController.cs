using Avalonia.Platform.Storage;
using FluentResults;
using GbcNet.App.Audio;
using GbcNet.App.Saves;
using GbcNet.Core;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Hardware;
using GbcNet.Core.Joypad;
using GbcNet.Core.Ppu;

namespace GbcNet.App.Emulation;

/// <summary>
/// Owns the emulation session lifecycle, loaded ROM state, saves, pause, and fast-forward.
/// </summary>
internal sealed class EmulationController(
    GameBoyOptions gameBoyOptions,
    IAudioOutput audioOutput,
    CartridgeSaveFileService cartridgeSaveFileService,
    Action<FrameCompletedEventArgs> handleFrame,
    Action<EmulationMetrics> handleMetrics,
    Action<Exception> handleFault
)
{
    private EmulationSession? _session;
    private GameBoyOptions _gameBoyOptions = gameBoyOptions;
    private byte[]? _loadedRom;
    private string _loadedRomName = string.Empty;
    private bool _fastForwardEnabled;
    private EmulationSpeed _fastForwardSpeed = EmulationSpeed.Two;

    public EmulationControllerState State =>
        new(
            HasSession: _session is not null,
            IsPaused: _session?.IsPaused ?? false,
            FastForwardEnabled: _fastForwardEnabled,
            FastForwardSpeed: _fastForwardSpeed,
            LoadedRomName: _loadedRomName
        );

    public void SetGameBoyOptions(GameBoyOptions options)
    {
        _gameBoyOptions = options;
    }

    public void SetButtonState(JoypadButton button, bool pressed)
    {
        _session?.SetButtonState(button, pressed);
    }

    public void TogglePause()
    {
        _session?.IsPaused = !_session.IsPaused;
    }

    public void ToggleFastForward()
    {
        _fastForwardEnabled = !_fastForwardEnabled;
        ApplyFastForwardSettings();
    }

    public void SetFastForwardSpeed(EmulationSpeed speed)
    {
        _fastForwardSpeed = speed;
        ApplyFastForwardSettings();
    }

    public async Task<Result<EmulationControllerState>> OpenRomFileAsync(IStorageFile file)
    {
        var rom = await ReadFileAsync(file).ConfigureAwait(true);
        await StopAsync().ConfigureAwait(true);
        var cartridge = LoadCartridge(rom);
        if (cartridge.IsFailed)
        {
            return Result.Fail<EmulationControllerState>(cartridge.Errors);
        }

        _loadedRom = rom;
        _loadedRomName = file.Name;
        Start(cartridge.Value, rom);
        return State;
    }

    public async Task<Result<EmulationControllerState>> ResetAsync()
    {
        if (_loadedRom is null)
        {
            return State;
        }

        await StopAsync().ConfigureAwait(true);
        var cartridge = LoadCartridge(_loadedRom);
        if (cartridge.IsFailed)
        {
            return Result.Fail<EmulationControllerState>(cartridge.Errors);
        }

        Start(cartridge.Value, _loadedRom);
        return State;
    }

    public async Task StopAsync()
    {
        var session = _session;
        if (session is null)
        {
            return;
        }

        _session = null;
        await session.StopAsync().ConfigureAwait(true);
    }

    private static async Task<byte[]> ReadFileAsync(IStorageFile file)
    {
        var stream = await file.OpenReadAsync().ConfigureAwait(false);
        var memoryStream = new MemoryStream();
        await using (stream.ConfigureAwait(false))
        await using (memoryStream.ConfigureAwait(false))
        {
            await stream.CopyToAsync(memoryStream, CancellationToken.None).ConfigureAwait(false);
            return memoryStream.ToArray();
        }
    }

    private Result<Cartridge> LoadCartridge(byte[] rom)
    {
        var cartridge = Cartridge.Load(rom);
        if (cartridge.IsFailed)
        {
            return cartridge;
        }

        var save = cartridgeSaveFileService.Load(cartridge.Value, rom);
        return save.IsFailed ? Result.Fail<Cartridge>(save.Errors) : cartridge;
    }

    private void Start(Cartridge cartridge, byte[] rom)
    {
        var hardwareModel = cartridge.Header.CgbSupport
            is CgbSupport.Required
                or CgbSupport.Enhanced
            ? HardwareModel.Cgb
            : HardwareModel.Dmg;

        _session = new EmulationSession(
            new GameBoy(cartridge, hardwareModel, _gameBoyOptions),
            audioOutput,
            handleFrame,
            handleMetrics,
            handleFault,
            () => cartridgeSaveFileService.Save(cartridge, rom)
        );
        ApplyFastForwardSettings();
    }

    private void ApplyFastForwardSettings()
    {
        _session?.SetFastForward(_fastForwardEnabled, _fastForwardSpeed);
    }
}

internal readonly record struct EmulationControllerState(
    bool HasSession,
    bool IsPaused,
    bool FastForwardEnabled,
    EmulationSpeed FastForwardSpeed,
    string LoadedRomName
);
