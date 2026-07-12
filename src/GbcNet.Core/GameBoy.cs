// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using GbcNet.Core.Apu;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Clock;
using GbcNet.Core.Hardware;
using GbcNet.Core.Hardware.Profiles;
using GbcNet.Core.Joypad;
using GbcNet.Core.Memory;
using GbcNet.Core.Ppu;
using GbcNet.Core.Sm83;

namespace GbcNet.Core;

/// <summary>
/// Game Boy CPU timing constants used by core and host.
/// </summary>
public static class GameBoyTiming
{
    /// <summary>
    /// CPU machine-cycle rate in normal-speed mode.
    /// </summary>
    public const int NormalCpuHz = 1_048_576;

    /// <summary>
    /// CPU machine-cycle rate in CGB double-speed mode.
    /// </summary>
    public const int DoubleCpuHz = NormalCpuHz * 2;

    /// <summary>
    /// CPU machine-cycle rate on NTSC Super Game Boy hardware.
    /// </summary>
    public const int SgbCpuHz = 1_073_864;
}

/// <summary>
/// Coordinates the emulated Game Boy hardware components for one execution step.
/// </summary>
public sealed class GameBoy
{
    private readonly MachineClock _clock;
    private bool _isStepping;

    /// <summary>
    /// Creates a Game Boy instance using the supplied cartridge and hardware model.
    /// </summary>
    public GameBoy(Cartridge cartridge, HardwareModel hardwareModel)
        : this(cartridge, hardwareModel, new BootRomOptions()) { }

    /// <summary>
    /// Creates a Game Boy instance using optional model-specific boot ROM images.
    /// </summary>
    public GameBoy(Cartridge cartridge, HardwareModel hardwareModel, BootRomOptions options)
    {
        ArgumentNullException.ThrowIfNull(cartridge);
        ArgumentNullException.ThrowIfNull(options);

        var hardwareProfile = HardwareProfileFactory.Create(hardwareModel, cartridge.Header);

        var bootRom = BootRom.Create(hardwareProfile.Model, options);
        Bus = new MemoryBus(cartridge, hardwareProfile, bootRom);
        Bus.Serial.ByteTransferred += OnSerialByteTransferred;
        _clock = new MachineClock(Bus);
        Cpu = new Cpu(Bus, _clock.TickMachineCycle);

        if (bootRom is not null)
        {
            Cpu.Registers.PC = 0x0000;
            Cpu.Registers.C = hardwareProfile.Model switch
            {
                HardwareModel.Sgb => 0x14,
                HardwareModel.Dmg => 0x13,
                _ => 0x00,
            };
        }

        HardwareModel = hardwareProfile.Model;
        CpuMachineCyclesPerSecond =
            hardwareProfile.Model is HardwareModel.Sgb
                ? GameBoyTiming.SgbCpuHz
                : GameBoyTiming.NormalCpuHz;

        if (bootRom is null)
        {
            hardwareProfile.ApplyPostBootState(cartridge, Cpu, Bus);
        }
        else
        {
            Cpu.Registers.PC = AddressMap.RomStart;
        }
    }

    // Raw payload delegates avoid allocating wrapper objects on frame and serial hot paths.
#pragma warning disable CA1003, MA0046
    /// <summary>
    /// Raised when a serial transfer completes, carrying the byte latched at transfer start.
    /// </summary>
    public event Action<byte>? SerialByteTransferred;

    /// <summary>
    /// Raised after a complete visible LCD frame is available at VBlank entry.
    /// </summary>
    public event Action<LcdFrame>? FrameCompleted;
#pragma warning restore CA1003, MA0046

    /// <summary>
    /// Enables host-visible LCD frame rendering while keeping LCD timing active either way.
    /// </summary>
    public bool VideoRenderingEnabled
    {
        get => Bus.Ppu.VideoRenderingEnabled;
        set => Bus.Ppu.VideoRenderingEnabled = value;
    }

    /// <summary>
    /// Executes one CPU step and advances hardware that runs from CPU cycles.
    /// </summary>
    /// <returns>
    /// Elapsed machine cycles.
    /// </returns>
    public int Step()
    {
        if (_isStepping)
        {
            throw new InvalidOperationException("Game Boy steps cannot be reentered.");
        }

        _isStepping = true;

        try
        {
            var machineCycles = Bus.Clock.TryStepSpeedSwitchPause() ? 1 : Cpu.Step();

            while (_clock.TryDequeueCompletedFrame(out var frame))
            {
                FrameCompleted?.Invoke(frame);
            }

            return machineCycles;
        }
        finally
        {
            _isStepping = false;
        }
    }

    /// <summary>
    /// Captures an opaque, complete machine continuation state.
    /// </summary>
    public GameBoyState CaptureState()
    {
        ThrowIfStepping();
        return new GameBoyState(HardwareModel, Cpu.CaptureState(), Bus.CaptureState());
    }

    /// <summary>
    /// Encodes a complete machine continuation state into the core's versioned payload format.
    /// </summary>
    public byte[] CaptureSaveState() => GameBoyStateCodec.Encode(CaptureState());

    /// <summary>
    /// Decodes and restores a previously captured save-state payload.
    /// </summary>
    public void RestoreSaveState(ReadOnlySpan<byte> state)
    {
        ThrowIfStepping();
        RestoreState(GameBoyStateCodec.Decode(state));
    }

    /// <summary>
    /// Restores a state captured from a compatible Game Boy without executing hardware or
    /// notifying host observers.
    /// </summary>
    public void RestoreState(GameBoyState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ThrowIfStepping();

        if (state.HardwareModel != HardwareModel)
        {
            throw new ArgumentException(
                "State hardware model does not match this Game Boy.",
                nameof(state)
            );
        }

        Bus.ValidateState(state.Bus);
        Bus.RestoreState(state.Bus);
        Cpu.RestoreState(state.Cpu);
    }

    /// <summary>
    /// Drains signed PCM-friendly APU stereo samples after output conditioning.
    /// </summary>
    public int DrainAudioSamples(Span<ApuStereoSample> destination) =>
        Bus.Apu.DrainBufferedSamples(destination);

    /// <summary>
    /// Updates a joypad button state for the emulated machine.
    /// </summary>
    public void SetButtonState(JoypadButton button, bool pressed)
    {
        Bus.Joypad.SetButtonState(button, pressed);
    }

    /// <summary>
    /// Hardware model selected for this emulation instance.
    /// </summary>
    public HardwareModel HardwareModel { get; }

    /// <summary>
    /// Current CPU machine-cycle rate, doubled while CGB double-speed mode is active.
    /// </summary>
    public int CpuMachineCyclesPerSecond =>
        Bus.Clock.CgbDoubleSpeed ? GameBoyTiming.DoubleCpuHz : field;

    public bool IsBootRomMapped => Bus.IsBootRomMapped;

    internal MemoryBus Bus { get; }

    internal Cpu Cpu { get; }

    private void ThrowIfStepping()
    {
        if (_isStepping)
        {
            throw new InvalidOperationException(
                "Game Boy state operations require an instruction boundary."
            );
        }
    }

    private void OnSerialByteTransferred(byte transferredByte) =>
        SerialByteTransferred?.Invoke(transferredByte);
}

/// <summary>
/// Opaque continuation state for one Game Boy instance.
/// </summary>
public sealed class GameBoyState
{
    internal GameBoyState(HardwareModel hardwareModel, CpuState cpu, MemoryBusState bus)
    {
        HardwareModel = hardwareModel;
        Cpu = cpu;
        Bus = bus;
    }

    internal HardwareModel HardwareModel { get; }

    internal CpuState Cpu { get; }

    internal MemoryBusState Bus { get; }
}

internal static class GameBoyStateCodec
{
    private const int FormatVersion = 1;

    private static readonly JsonSerializerOptions _options = new()
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
    };

    internal static byte[] Encode(GameBoyState state) =>
        JsonSerializer.SerializeToUtf8Bytes(
            new GameBoyStatePayload(FormatVersion, state.HardwareModel, state.Cpu, state.Bus),
            _options
        );

    internal static GameBoyState Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new Utf8JsonReader(bytes);
            using var document = JsonDocument.ParseValue(ref reader);

            if (reader.Read())
            {
                throw new JsonException("Save-state payload contains trailing JSON.");
            }

            var payload = document.RootElement.Deserialize<GameBoyStatePayload>(_options);
            using var normalized = JsonDocument.Parse(
                JsonSerializer.SerializeToUtf8Bytes(payload, _options)
            );

            ValidateRequiredMembers(document.RootElement, normalized.RootElement);

            if (payload.FormatVersion != FormatVersion)
            {
                throw new InvalidDataException(
                    $"Unsupported Game Boy state format version {payload.FormatVersion}."
                );
            }

            return new GameBoyState(payload.HardwareModel, payload.Cpu, payload.Bus);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Game Boy state payload is malformed.", exception);
        }
    }

    private static void ValidateRequiredMembers(JsonElement input, JsonElement normalized)
    {
        if (input.ValueKind != normalized.ValueKind)
        {
            throw new JsonException("Save-state member has an invalid JSON type.");
        }

        if (normalized.ValueKind is JsonValueKind.Object)
        {
            foreach (var property in normalized.EnumerateObject())
            {
                if (!input.TryGetProperty(property.Name, out var inputProperty))
                {
                    throw new JsonException($"Save-state member '{property.Name}' is missing.");
                }

                ValidateRequiredMembers(inputProperty, property.Value);
            }

            return;
        }

        if (
            normalized.ValueKind is not JsonValueKind.Array
            || normalized.GetArrayLength() == 0
            || normalized[0].ValueKind is not (JsonValueKind.Array or JsonValueKind.Object)
        )
        {
            return;
        }

        var inputItems = input.EnumerateArray();
        foreach (var normalizedItem in normalized.EnumerateArray())
        {
            if (!inputItems.MoveNext())
            {
                throw new JsonException("Save-state array member is missing.");
            }

            ValidateRequiredMembers(inputItems.Current, normalizedItem);
        }
    }
}

internal readonly record struct GameBoyStatePayload(
    int FormatVersion,
    HardwareModel HardwareModel,
    CpuState Cpu,
    MemoryBusState Bus
);
