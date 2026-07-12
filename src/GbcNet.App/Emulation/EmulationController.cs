// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Platform.Storage;
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
    BootRomOptions bootRomOptions,
    IAudioOutput audioOutput,
    CartridgeBatterySaveFileService cartridgeSaveFileService,
    SaveStateFileService saveStateFileService,
    Action<LcdFrame> handleFrame,
    Action<Exception> handleFault,
    bool fastForwardEnabled,
    EmulationSpeed fastForwardSpeed
)
{
    private EmulationSession? _session;
    private BootRomOptions _bootRomOptions = bootRomOptions;
    private byte[]? _loadedRom;
    private CartridgeHeader? _loadedCartridgeHeader;
    private string _loadedRomFileName = string.Empty;
    private RomStorageIdentity? _loadedRomStorageIdentity;

    private bool _fastForwardEnabled = fastForwardEnabled;
    private EmulationSpeed _fastForwardSpeed = Enum.IsDefined(fastForwardSpeed)
        ? fastForwardSpeed
        : EmulationSpeed.Two;

    public EmulationControllerState State =>
        new(
            HasSession: _session is not null,
            IsPaused: _session?.IsPaused ?? false,
            FastForwardEnabled: _fastForwardEnabled,
            FastForwardSpeed: _fastForwardSpeed,
            LoadedRom: _loadedRom.AsMemory(),
            LoadedCartridgeHeader: _loadedCartridgeHeader,
            LoadedRomFileName: _loadedRomFileName,
            HardwareModel: _session?.HardwareModel
        );

    public void SetBootRomOptions(BootRomOptions options)
    {
        _bootRomOptions = options;
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

    public async Task<EmulationControllerState> OpenRomFileAsync(IStorageFile file)
    {
        var rom = await ReadFileAsync(file).ConfigureAwait(true);
        await StopAsync().ConfigureAwait(true);
        var (cartridge, savePath) = LoadCartridge(rom);

        _loadedRom = rom;
        _loadedCartridgeHeader = cartridge.Header;
        _loadedRomFileName = file.Name;
        _loadedRomStorageIdentity = RomStorageIdentity.Create(cartridge.Header.Title, rom);

        Start(cartridge, savePath);
        return State;
    }

    public async Task<EmulationControllerState> ResetAsync()
    {
        if (_loadedRom is null)
        {
            return State;
        }

        await StopAsync().ConfigureAwait(true);
        var (cartridge, savePath) = LoadCartridge(_loadedRom);

        _loadedCartridgeHeader = cartridge.Header;
        Start(cartridge, savePath);
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

    public async Task SaveStateAsync(int slot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (session, rom) = GetSaveStateTarget();
        var state = await session.CaptureSaveStateAsync().ConfigureAwait(true);

        cancellationToken.ThrowIfCancellationRequested();

        await saveStateFileService
            .SaveAsync(rom, slot, session.HardwareModel, state, cancellationToken)
            .ConfigureAwait(true);
    }

    public async Task LoadStateAsync(int slot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (session, rom) = GetSaveStateTarget();
        var state = await saveStateFileService
            .LoadAsync(rom, slot, session.HardwareModel, cancellationToken)
            .ConfigureAwait(true);

        cancellationToken.ThrowIfCancellationRequested();

        await session.RestoreSaveStateAsync(state).ConfigureAwait(true);
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

    private (Cartridge Cartridge, string? SavePath) LoadCartridge(byte[] rom)
    {
        var cartridge = Cartridge.LoadOrThrow(rom);
        return (cartridge, cartridgeSaveFileService.Load(cartridge, rom));
    }

    private (EmulationSession Session, RomStorageIdentity Rom) GetSaveStateTarget()
    {
        if (_session is not { } session || _loadedRomStorageIdentity is not { } rom)
        {
            throw new InvalidOperationException("No ROM is loaded.");
        }

        return (session, rom);
    }

    private void Start(Cartridge cartridge, string? savePath)
    {
        var hardwareModel = cartridge.Header.HardwareKind switch
        {
            CartridgeHardwareKind.GBC => HardwareModel.Cgb,
            CartridgeHardwareKind.SGB => HardwareModel.Sgb,
            _ => HardwareModel.Dmg,
        };
        CartridgeBatterySaveWriter? saveWriter = null;

        if (savePath is not null)
        {
            saveWriter = new CartridgeBatterySaveWriter(
                cartridge,
                save => cartridgeSaveFileService.SaveAsync(savePath, save),
                handleFault
            );
        }

        _session = new EmulationSession(
            new GameBoy(cartridge, hardwareModel, _bootRomOptions),
            audioOutput,
            handleFrame,
            HandleFatalSessionFault,
            saveWriter
        );
        ApplyFastForwardSettings();
    }

    private void HandleFatalSessionFault(Exception exception)
    {
        _session = null;
        handleFault(exception);
    }

    private void ApplyFastForwardSettings() =>
        _session?.SetFastForward(_fastForwardEnabled, _fastForwardSpeed);
}

internal readonly record struct EmulationControllerState(
    bool HasSession,
    bool IsPaused,
    bool FastForwardEnabled,
    EmulationSpeed FastForwardSpeed,
    ReadOnlyMemory<byte> LoadedRom,
    CartridgeHeader? LoadedCartridgeHeader,
    string LoadedRomFileName,
    HardwareModel? HardwareModel
)
{
    public EmulationSpeed EffectiveSpeed =>
        FastForwardEnabled ? FastForwardSpeed : EmulationSpeed.Normal;
}
