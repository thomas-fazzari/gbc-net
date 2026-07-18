// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using System.Text.Json.Serialization;
using GbcNet.Core;
using GbcNet.Core.Hardware;

namespace GbcNet.App.Configuration.Sections.BootRom;

internal readonly record struct BootRomConfig(
    [property: JsonPropertyName("dmg")] string? DmgPath = null,
    [property: JsonPropertyName("cgb")] string? CgbPath = null,
    [property: JsonPropertyName("sgb")] string? SgbPath = null
)
{
    public static string JsonName(HardwareModel model) =>
        JsonNamingPolicy.CamelCase.ConvertName(model.ToString());

    public static string DisplayName(HardwareModel model) => model.ToString().ToUpperInvariant();

    public static int Size(HardwareModel model) =>
        model switch
        {
            HardwareModel.Dmg => BootRomOptions.DmgBootRomSize,
            HardwareModel.Cgb => BootRomOptions.CgbBootRomSize,
            HardwareModel.Sgb => BootRomOptions.SgbBootRomSize,
            _ => throw new ArgumentOutOfRangeException(
                paramName: nameof(model),
                actualValue: model,
                message: null
            ),
        };

    public string? GetPath(HardwareModel model) =>
        model switch
        {
            HardwareModel.Dmg => DmgPath,
            HardwareModel.Cgb => CgbPath,
            HardwareModel.Sgb => SgbPath,
            _ => throw new ArgumentOutOfRangeException(
                paramName: nameof(model),
                actualValue: model,
                message: null
            ),
        };
}
