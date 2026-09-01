// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Configuration.Sections.Audio;
using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.App.Configuration.Sections.Input;

namespace GbcNet.App.Configuration;

internal sealed record SettingsConfig(BootRomConfig BootRoms, InputConfig Input)
{
    public AudioConfig Audio { get; init; } = new();
}
