// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using GbcNet.Core;
using GbcNet.Core.Hardware;

namespace GbcNet.App.Configuration.Sections.BootRom;

internal readonly record struct BootRomConfig(
    string? DmgPath = null,
    string? CgbPath = null,
    string? SgbPath = null
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
            _ => throw new ArgumentOutOfRangeException(nameof(model), model, message: null),
        };

    public string? GetPath(HardwareModel model) =>
        model switch
        {
            HardwareModel.Dmg => DmgPath,
            HardwareModel.Cgb => CgbPath,
            HardwareModel.Sgb => SgbPath,
            _ => throw new ArgumentOutOfRangeException(nameof(model), model, message: null),
        };

    public Dictionary<HardwareModel, string?> ToDictionary() =>
        new()
        {
            [HardwareModel.Dmg] = DmgPath,
            [HardwareModel.Cgb] = CgbPath,
            [HardwareModel.Sgb] = SgbPath,
        };

    public static BootRomConfig FromDictionary(IReadOnlyDictionary<HardwareModel, string?> paths) =>
        new(
            GetPath(paths, HardwareModel.Dmg),
            GetPath(paths, HardwareModel.Cgb),
            GetPath(paths, HardwareModel.Sgb)
        );

    private static string? GetPath(
        IReadOnlyDictionary<HardwareModel, string?> paths,
        HardwareModel model
    ) => paths.TryGetValue(model, out var path) ? path : null;
}
