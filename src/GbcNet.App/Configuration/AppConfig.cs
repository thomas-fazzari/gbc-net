// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Configuration.Sections.Input;
using GbcNet.Core.Hardware;

namespace GbcNet.App.Configuration;

internal sealed class AppConfig
{
    public InputConfig Input { get; set; } = AppConfigurationFile.CreateDefaultInputConfig();

    public Dictionary<HardwareModel, string?> BootRoms { get; set; } = [];
}
