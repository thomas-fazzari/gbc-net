// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Configuration.Kdl;
using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.Core;

namespace GbcNet.App.Configuration;

internal sealed class AppConfigurationService(string configPath)
{
    public BootRomConfig LoadBootRomConfig() =>
        BootRomConfigReader.ReadConfig(KdlConfigurationFile.LoadOrCreate(configPath));

    public BootRomOptions LoadBootRomOptions() =>
        BootRomConfigReader.ReadBootRomOptions(
            KdlConfigurationFile.LoadOrCreate(configPath),
            Path.GetDirectoryName(configPath) ?? Environment.CurrentDirectory
        );

    public void SaveBootRomConfig(BootRomConfig config) =>
        BootRomConfigWriter.Write(configPath, config);
}
