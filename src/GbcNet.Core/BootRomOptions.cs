// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core;

/// <summary>
/// Model-specific boot ROM images used to start execution at the hardware reset vector.
/// </summary>
public sealed class BootRomOptions
{
    /// <summary>
    /// Required byte length of a DMG boot ROM image.
    /// </summary>
    public const int DmgBootRomSize = 256;

    /// <summary>
    /// Required byte length of a CGB boot ROM image.
    /// </summary>
    public const int CgbBootRomSize = 2048;

    /// <summary>
    /// Required byte length of an SGB boot ROM image.
    /// </summary>
    public const int SgbBootRomSize = 256;

    /// <summary>
    /// Optional 256-byte DMG boot ROM image.
    /// </summary>
    /// <remarks>
    /// Leave empty to skip the boot ROM and seed post-boot state directly.
    /// </remarks>
    public ReadOnlyMemory<byte> DmgBootRom { get; init; }

    /// <summary>
    /// Optional 2048-byte CGB boot ROM image.
    /// </summary>
    /// <remarks>
    /// Leave empty to skip the boot ROM and seed post-boot state directly.
    /// </remarks>
    public ReadOnlyMemory<byte> CgbBootRom { get; init; }

    /// <summary>
    /// Optional 256-byte SGB boot ROM image.
    /// </summary>
    /// <remarks>
    /// Leave empty to skip the boot ROM and seed post-boot state directly.
    /// </remarks>
    public ReadOnlyMemory<byte> SgbBootRom { get; init; }
}
