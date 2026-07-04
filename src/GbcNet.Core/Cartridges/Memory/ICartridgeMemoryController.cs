// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Cartridges.Memory;

/// <summary>
/// Handles cartridge-local ROM, cartridge RAM, and MBC control register accesses.
/// </summary>
internal interface ICartridgeMemoryController
{
    /// <summary>
    /// Reads a byte from the CPU-visible cartridge ROM area at 0000-7FFF.
    /// </summary>
    byte ReadRom(ushort address);

    /// <summary>
    /// Handles a CPU write to the ROM area, usually as an MBC control register write.
    /// </summary>
    void WriteRom(ushort address, byte value);

    /// <summary>
    /// Reads a byte from the A000-BFFF external RAM window using an offset from A000.
    /// </summary>
    byte ReadRamOffset(ushort offset);

    /// <summary>
    /// Writes a byte to the A000-BFFF external RAM window using an offset from A000.
    /// </summary>
    void WriteRamOffset(ushort offset, byte value);

    /// <summary>
    /// Battery-backed save data exposed by this cartridge controller.
    /// </summary>
    ICartridgeSaveData SaveData { get; }
}
