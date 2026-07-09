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
        var tempDirectory = TestDirectories.GetTemporaryDirectoryPath();
        var configPath = Path.Combine(tempDirectory, UserDataPaths.ConfigFileName);
        const string dmgPath = "dir\\boot\"rom.bin";
        var bootRoms = new BootRomConfig(dmgPath, "cgb.bin", "sgb.bin");

        try
        {
            var config = AppConfigurationFile.CreateDefault();
            config.BootRoms = bootRoms.ToDictionary();

            AppConfigurationFile.Save(configPath, config);

            using var json = JsonDocument.Parse(File.ReadAllText(configPath));
            Assert.Equal(
                dmgPath,
                json.RootElement.GetProperty("bootRoms")
                    .GetProperty(BootRomConfig.JsonName(HardwareModel.Dmg))
                    .GetString()
            );
            Assert.Equal(
                bootRoms,
                BootRomConfig.FromDictionary(AppConfigurationFile.Load(configPath).BootRoms)
            );
        }
        finally
        {
            TestDirectories.DeleteDirectoryIfExists(tempDirectory);
        }
    }
}
