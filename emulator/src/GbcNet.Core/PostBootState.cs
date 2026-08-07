// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Memory;
using GbcNet.Core.Sm83;

namespace GbcNet.Core;

/// <summary>
/// Applies hardware register state used when boot ROM execution is skipped.
/// </summary>
internal static class PostBootState
{
    internal static void SetCpuRegisters(Registers registers, PostBootCpuRegisterState state)
    {
        registers.A = state.A;
        registers.F = state.F;
        registers.BC = state.BC;
        registers.DE = state.DE;
        registers.HL = state.HL;
        registers.PC = state.PC;
        registers.SP = state.SP;
    }

    internal static void SetHardwareRegisterStates(
        MemoryBus bus,
        ReadOnlySpan<PostBootHardwareRegisterState> registerStates
    )
    {
        foreach (var registerState in registerStates)
        {
            bus.SetHardwareRegisterState(registerState.Address, registerState.RegisterValue);
        }
    }

    /// <summary>
    /// Post-boot APU register values indexed from FF10 through FF26, shared by DMG, CGB, and SGB.
    /// </summary>
    private const ushort AudioRegistersStart = 0xFF10;

    private static ReadOnlySpan<byte> AudioRegisterStates =>
        [
            0x80,
            0xBF,
            0xF3,
            0xFF,
            0xBF,
            0xFF,
            0x3F,
            0x00,
            0xFF,
            0xBF,
            0x7F,
            0xFF,
            0x9F,
            0xFF,
            0xBF,
            0xFF,
            0xFF,
            0x00,
            0x00,
            0xBF,
            0x77,
            0xF3,
            0xF1,
        ];

    internal static void ApplyAudioRegisters(MemoryBus bus)
    {
        var values = AudioRegisterStates;
        for (var offset = 0; offset < values.Length; offset++)
        {
            bus.SetHardwareRegisterState((ushort)(AudioRegistersStart + offset), values[offset]);
        }
    }
}

internal readonly record struct PostBootCpuRegisterState(
    byte A,
    byte F,
    ushort BC,
    ushort DE,
    ushort HL,
    ushort PC,
    ushort SP
);

internal readonly record struct PostBootHardwareRegisterState(ushort Address, byte RegisterValue);
