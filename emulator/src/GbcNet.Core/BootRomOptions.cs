// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using GbcNet.Core.Hardware;

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
    /// Packed byte length of a CGB boot ROM image with the unused 0100-01FF gap omitted.
    /// </summary>
    public const int CgbBootRomSize = 2048;

    /// <summary>
    /// Mapped byte length of a CGB boot ROM image including the unused 0100-01FF gap.
    /// </summary>
    public const int CgbBootRomMappedSize = 2304;

    /// <summary>
    /// Required byte length of an SGB boot ROM image.
    /// </summary>
    public const int SgbBootRomSize = 256;

    public static bool IsValidSize(HardwareModel model, int size) =>
        model switch
        {
            HardwareModel.Dmg => size == DmgBootRomSize,
            HardwareModel.Cgb => size is CgbBootRomSize or CgbBootRomMappedSize,
            HardwareModel.Sgb => size == SgbBootRomSize,
            _ => throw new ArgumentOutOfRangeException(nameof(model), model, message: null),
        };

    public static string SizeDescription(HardwareModel model) =>
        model switch
        {
            HardwareModel.Dmg => DmgBootRomSize.ToString(CultureInfo.InvariantCulture),
            HardwareModel.Cgb =>
                $"{CgbBootRomSize.ToString(CultureInfo.InvariantCulture)} or {CgbBootRomMappedSize.ToString(CultureInfo.InvariantCulture)}",
            HardwareModel.Sgb => SgbBootRomSize.ToString(CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(model), model, message: null),
        };

    /// <summary>
    /// Optional 256-byte DMG boot ROM image.
    /// </summary>
    /// <remarks>
    /// Leave empty to skip the boot ROM and seed post-boot state directly.
    /// </remarks>
    public ReadOnlyMemory<byte> DmgBootRom { get; init; }

    /// <summary>
    /// Optional 2048-byte packed or 2304-byte mapped CGB boot ROM image.
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
