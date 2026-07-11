// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using GbcNet.App.Configuration;
using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.Core.Hardware;
using Microsoft.Extensions.Logging.Abstractions;

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

        AppConfigurationFile.Save(configPath, config, NullLogger.Instance);
        Assert.False(File.Exists(configPath + ".tmp"));

        using var json = JsonDocument.Parse(File.ReadAllText(configPath));
        Assert.Equal(
            dmgPath,
            json.RootElement.GetProperty("bootRoms")
                .GetProperty(BootRomConfig.JsonName(HardwareModel.Dmg))
                .GetString()
        );
        Assert.Equal(bootRoms, AppConfigurationFile.Load(configPath).BootRoms);
    }

    [Fact]
    public void Save_WhenTemporaryFileCannotBeCreated_LeavesExistingConfigUnchanged()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        var originalConfig = AppConfigurationFile.CreateDefault();
        originalConfig.BootRoms = new BootRomConfig("dmg.bin", "cgb.bin", "sgb.bin");
        AppConfigurationFile.Save(configPath, originalConfig, NullLogger.Instance);
        var originalBytes = File.ReadAllBytes(configPath);
        var temporaryPath = configPath + ".tmp";
        Directory.CreateDirectory(temporaryPath);

        var replacementConfig = AppConfigurationFile.CreateDefault();
        replacementConfig.BootRoms = new BootRomConfig("new-dmg.bin", "new-cgb.bin", "new-sgb.bin");

        Assert.Throws<ConfigurationException>(() =>
            AppConfigurationFile.Save(configPath, replacementConfig, NullLogger.Instance)
        );

        Assert.Equal(originalBytes, File.ReadAllBytes(configPath));
        Assert.Equal(originalConfig.BootRoms, AppConfigurationFile.Load(configPath).BootRoms);
        Assert.True(Directory.Exists(temporaryPath));
    }
}
