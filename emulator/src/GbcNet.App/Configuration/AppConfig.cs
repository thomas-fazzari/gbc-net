// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics.CodeAnalysis;
using GbcNet.App.Configuration.Sections.Audio;
using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.App.Configuration.Sections.Emulation;
using GbcNet.App.Configuration.Sections.Input;
using GbcNet.App.Configuration.Sections.Library;
using GbcNet.App.Infrastructure.Configuration;

namespace GbcNet.App.Configuration;

internal sealed class AppConfig
{
    [AllowNull]
    public InputConfig Input
    {
        get;
        set => field = value ?? AppConfigurationFile.CreateDefaultInputConfig();
    } = AppConfigurationFile.CreateDefaultInputConfig();

    [AllowNull]
    public EmulationConfig Emulation
    {
        get;
        set => field = value ?? new EmulationConfig();
    } = new();

    public AudioConfig Audio { get; set; } = new();

    [AllowNull]
    public LibraryConfig Library
    {
        get;
        set => field = value ?? new LibraryConfig();
    } = new();

    public BootRomConfig BootRoms { get; set; }
}
