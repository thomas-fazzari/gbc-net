// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Platform.Storage;
using ErrorOr;
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
    CheatCodeService cheatCodeService,
    Action<LcdFrame> handleFrame,
    Action<Exception> handleFault,
    Action<Exception> handlePersistenceError,
    bool fastForwardEnabled,
    EmulationSpeed fastForwardSpeed
)
{
    internal const int MaximumRomSize = 8 * 1024 * 1024;
    internal const string NoActiveCheatSessionErrorCode = "Cheat.NoActiveSession";
    internal const string InvalidCheatCodesErrorCode = "Cheat.InvalidCodes";
    internal const string CheatSessionChangedErrorCode = "Cheat.SessionChanged";

    private EmulationSession? _session;
    private BootRomOptions _bootRomOptions = bootRomOptions;
    private ReadOnlyMemory<byte> _loadedRom;
    private CartridgeHeader? _loadedCartridgeHeader;
    private string _loadedRomFileName = string.Empty;
    private RomStorageIdentity? _loadedRomStorageIdentity;
    private CheatCodeEntry[] _cheatCodes = [];

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
            LoadedRom: _loadedRom,
            LoadedRomIdentity: _loadedRomStorageIdentity,
            LoadedCartridgeHeader: _loadedCartridgeHeader,
            LoadedRomFileName: _loadedRomFileName,
            HardwareModel: _session?.HardwareModel,
            CheatCodes: _cheatCodes
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

    public async Task<ErrorOr<EmulationControllerState>> OpenRomFileAsync(IStorageFile file)
    {
        var readResult = await ReadFileAsync(file);
        if (readResult.IsError)
        {
            return readResult.Errors;
        }

        var rom = readResult.Value;
        var loadResult = Cartridge.Load(rom.Span);
        if (loadResult.IsFailure)
        {
            var error = loadResult.Error;
            return Error.Validation($"Rom.{error.Code}", error.Message);
        }

        var cartridge = loadResult.Cartridge;
        var identity = RomStorageIdentity.Create(cartridge.Header.Title, rom.Span);
        var savePath = cartridgeSaveFileService.Load(cartridge, identity);
        var entries = await cheatCodeService.LoadAsync(identity.Hash, CancellationToken.None);
        var activeCodes = GetActiveCodes(entries);

        await StopAsync();

        _loadedRom = rom;
        _loadedCartridgeHeader = cartridge.Header;
        _loadedRomFileName = file.Name;
        _loadedRomStorageIdentity = identity;
        _cheatCodes = entries;

        Start(cartridge, savePath, activeCodes);
        return State;
    }

    public async Task<EmulationControllerState> ResetAsync()
    {
        if (_loadedRom.IsEmpty || _loadedRomStorageIdentity is not { } identity)
        {
            return State;
        }

        var cartridge = Cartridge.LoadOrThrow(_loadedRom.Span);
        var savePath = cartridgeSaveFileService.Load(cartridge, identity);
        var activeCodes = GetActiveCodes(_cheatCodes);
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

    public async Task<ErrorOr<Success>> SetCheatCodesAsync(
        IReadOnlyList<CheatCodeEntry> entries,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (_session is not { } session || _loadedRomStorageIdentity is not { } identity)
        {
            return Error.NotFound(NoActiveCheatSessionErrorCode, "No ROM is loaded.");
        }

        CheatCodeEntry[] savedEntries;
        try
        {
            savedEntries = await cheatCodeService.ReplaceAsync(
                identity.Hash,
                entries,
                cancellationToken
            );
        }
        catch (ArgumentException exception)
        {
            return Error.Validation(InvalidCheatCodesErrorCode, exception.Message);
        }

        _cheatCodes = savedEntries;

        try
        {
            await session.SetCheatCodesAsync(GetActiveCodes(savedEntries));
        }
        catch (OperationCanceledException)
        {
            return Error.Conflict(
                CheatSessionChangedErrorCode,
                "The emulation session changed before cheat codes could be applied."
            );
        }

        return Result.Success;
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

    private static async Task<ErrorOr<ReadOnlyMemory<byte>>> ReadFileAsync(IStorageFile file)
    {
        await using var stream = await file.OpenReadAsync();
        if (stream.CanSeek)
        {
            var length = stream.Length;
            if (length > MaximumRomSize)
            {
                return RomFileTooLargeError();
            }

            var rom = GC.AllocateUninitializedArray<byte>((int)length);
            await stream.ReadExactlyAsync(rom, CancellationToken.None);
            return new ReadOnlyMemory<byte>(rom);
        }

        var buffer = GC.AllocateUninitializedArray<byte>(MaximumRomSize + 1);
        var bytesRead = await stream.ReadAtLeastAsync(
            buffer,
            buffer.Length,
            throwOnEndOfStream: false,
            CancellationToken.None
        );
        if (bytesRead > MaximumRomSize)
        {
            return RomFileTooLargeError();
        }

        return new ReadOnlyMemory<byte>(buffer, start: 0, length: bytesRead);
    }

    private static Error RomFileTooLargeError() =>
        Error.Validation(
            $"Rom.{nameof(CartridgeLoadErrorCode.UnsupportedRomSize)}",
            "ROM files larger than 8 MiB are not supported."
        );

    private (EmulationSession Session, RomStorageIdentity Rom) GetSaveStateTarget()
    {
        if (_session is not { } session || _loadedRomStorageIdentity is not { } rom)
        {
            throw new InvalidOperationException("No ROM is loaded.");
        }

        return (session, rom);
    }

    private void Start(Cartridge cartridge, string? savePath, ReadOnlySpan<CheatCode> codes)
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
        gameBoy.Cheats.SetCodes(codes);
        _session = new EmulationSession(
            gameBoy,
            audioOutput,
            handleFrame,
            HandleFatalSessionFault,
            saveWriter
        );
        ApplyFastForwardSettings();
    }

    private static CheatCode[] GetActiveCodes(ReadOnlySpan<CheatCodeEntry> entries)
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

        var codes = new CheatCode[count];
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
    RomStorageIdentity? LoadedRomIdentity,
    CartridgeHeader? LoadedCartridgeHeader,
    string LoadedRomFileName,
    HardwareModel? HardwareModel,
    ReadOnlyMemory<CheatCodeEntry> CheatCodes
)
{
    public EmulationSpeed EffectiveSpeed =>
        FastForwardEnabled ? FastForwardSpeed : EmulationSpeed.Normal;
}
