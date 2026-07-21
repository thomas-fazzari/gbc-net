// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Configuration.Sections.Audio;
using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.App.Configuration.Sections.Emulation;
using GbcNet.App.Configuration.Sections.Input;

namespace GbcNet.App.Configuration;

internal sealed class AppConfig
{
    public InputConfig Input { get; set; } = AppConfigurationFile.CreateDefaultInputConfig();

    public EmulationConfig Emulation { get; set; } = new();

    public AudioConfig Audio { get; set; } = new();

    public BootRomConfig BootRoms { get; set; }
}
