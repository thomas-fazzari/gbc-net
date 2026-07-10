// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using GbcNet.App.Configuration;
using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.Core.Hardware;

namespace GbcNet.Tests.App.Configuration;

public sealed class AppConfigurationFileTests
{
    [Fact]
    public void Save_WritesJsonThatRoundTripsEscapedBootRomPaths()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        const string dmgPath = "dir\\boot\"rom.bin";
        var bootRoms = new BootRomConfig(dmgPath, "cgb.bin", "sgb.bin");

        var config = AppConfigurationFile.CreateDefault();
        config.BootRoms = bootRoms;

        AppConfigurationFile.Save(configPath, config);

        using var json = JsonDocument.Parse(File.ReadAllText(configPath));
        Assert.Equal(
            dmgPath,
            json.RootElement.GetProperty("bootRoms")
                .GetProperty(BootRomConfig.JsonName(HardwareModel.Dmg))
                .GetString()
        );
        Assert.Equal(bootRoms, AppConfigurationFile.Load(configPath).BootRoms);
    }
}
