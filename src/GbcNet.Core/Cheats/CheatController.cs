// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Memory;

namespace GbcNet.Core.Cheats;

/// <summary>
/// Controls cheat codes applied by one Game Boy instance.
/// </summary>
public sealed class CheatController
{
    private readonly GameBoy _gameBoy;
    private readonly MemoryBus _bus;

    internal CheatController(GameBoy gameBoy, MemoryBus bus)
    {
        _gameBoy = gameBoy;
        _bus = bus;
    }

    /// <summary>
    /// Replaces all active cheat codes for the machine.
    /// </summary>
    /// <remarks>
    /// Call from the machine thread between <see cref="GameBoy.Step"/> calls.
    /// </remarks>
    public void SetCodes(ReadOnlySpan<CheatCode> codes)
    {
        _gameBoy.ThrowIfStepping();
        _bus.SetCheatCodes(codes);
    }
}
