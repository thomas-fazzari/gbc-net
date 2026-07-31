// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Platform.Storage;
using GbcNet.App.Audio;
using GbcNet.App.Cheats;
using GbcNet.App.Saves;
using GbcNet.Core;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Cheats;
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
    GameGenieService gameGenieService,
    Action<LcdFrame> handleFrame,
    Action<Exception> handleFault,
    Action<Exception> handlePersistenceError,
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
    private GameGenieCodeEntry[] _gameGenieCodes = [];

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
            HardwareModel: _session?.HardwareModel,
            GameGenieCodes: _gameGenieCodes
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
        var rom = await ReadFileAsync(file);
        var (cartridge, savePath) = LoadCartridge(rom);
        var identity = RomStorageIdentity.Create(cartridge.Header.Title, rom);
        var codes = await gameGenieService.LoadAsync(identity.Hash, CancellationToken.None);
        var activeCodes = GetActiveGameGenieCodes(codes);

        await StopAsync();

        _loadedRom = rom;
        _loadedCartridgeHeader = cartridge.Header;
        _loadedRomFileName = file.Name;
        _loadedRomStorageIdentity = identity;
        _gameGenieCodes = codes;

        Start(cartridge, savePath, activeCodes);
        return State;
    }

    public async Task<EmulationControllerState> ResetAsync()
    {
        if (_loadedRom is null)
        {
            return State;
        }

        var (cartridge, savePath) = LoadCartridge(_loadedRom);
        var activeCodes = GetActiveGameGenieCodes(_gameGenieCodes);
        await StopAsync();

        _loadedCartridgeHeader = cartridge.Header;
        Start(cartridge, savePath, activeCodes);
        return State;
    }

    public async Task StopAsync()
    {
        var session = _session;
        if (session is null)
        {
            return;
        }

        await session.PrepareToStopAsync();
        _session = null;
        await session.StopAsync();
    }

    public async Task SaveStateAsync(int slot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (session, rom) = GetSaveStateTarget();
        var state = await session.CaptureSaveStateAsync();

        cancellationToken.ThrowIfCancellationRequested();

        await saveStateFileService.SaveAsync(
            rom,
            slot,
            session.HardwareModel,
            state,
            cancellationToken
        );
    }

    public async Task LoadStateAsync(int slot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (session, rom) = GetSaveStateTarget();
        var state = await saveStateFileService.LoadAsync(
            rom,
            slot,
            session.HardwareModel,
            cancellationToken
        );

        cancellationToken.ThrowIfCancellationRequested();

        await session.RestoreSaveStateAsync(state);
    }

    public async Task SetGameGenieCodesAsync(
        IReadOnlyList<GameGenieCodeEntry> entries,
        CancellationToken cancellationToken = default
    )
    {
        if (_session is not { } session || _loadedRomStorageIdentity is not { } identity)
        {
            throw new InvalidOperationException("No ROM is loaded.");
        }

        var codes = await gameGenieService.ReplaceAsync(identity.Hash, entries, cancellationToken);
        _gameGenieCodes = codes;

        try
        {
            await session.SetGameGenieCodesAsync(GetActiveGameGenieCodes(codes));
        }
        catch (InvalidOperationException exception)
            when (string.Equals(
                    exception.Message,
                    "Emulation session is stopped.",
                    StringComparison.Ordinal
                )
            )
        {
            return;
        }
        catch (OperationCanceledException)
        {
            return;
        }
    }

    public DateTime?[] GetSaveStateDates(int slotCount)
    {
        if (_loadedRomStorageIdentity is not { } rom)
        {
            return new DateTime?[slotCount];
        }

        return
        [
            .. Enumerable
                .Range(start: 0, count: slotCount)
                .Select(slot => saveStateFileService.GetSaveStateDate(rom, slot)),
        ];
    }

    private static async Task<byte[]> ReadFileAsync(IStorageFile file)
    {
        var stream = await file.OpenReadAsync();
        var memoryStream = new MemoryStream();
        await using (stream)
        await using (memoryStream)
        {
            await stream.CopyToAsync(memoryStream, CancellationToken.None);
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

    private void Start(
        Cartridge cartridge,
        string? savePath,
        ReadOnlySpan<GameGenieCode> gameGenieCodes
    )
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
                handlePersistenceError
            );
        }

        var gameBoy = new GameBoy(cartridge, hardwareModel, _bootRomOptions);
        gameBoy.SetGameGenieCodes(gameGenieCodes);
        _session = new EmulationSession(
            gameBoy,
            audioOutput,
            handleFrame,
            HandleFatalSessionFault,
            saveWriter
        );
        ApplyFastForwardSettings();
    }

    private static GameGenieCode[] GetActiveGameGenieCodes(ReadOnlySpan<GameGenieCodeEntry> entries)
    {
        var count = 0;
        foreach (var entry in entries)
        {
            if (entry.IsEnabled)
            {
                count++;
            }
        }

        if (count == 0)
        {
            return [];
        }

        var codes = new GameGenieCode[count];
        var index = 0;
        foreach (var entry in entries)
        {
            if (entry.IsEnabled)
            {
                codes[index++] = entry.Code;
            }
        }

        return codes;
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
    HardwareModel? HardwareModel,
    ReadOnlyMemory<GameGenieCodeEntry> GameGenieCodes
)
{
    public EmulationSpeed EffectiveSpeed =>
        FastForwardEnabled ? FastForwardSpeed : EmulationSpeed.Normal;
}
