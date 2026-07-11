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
    public void Save_WritesExactV2InputShape()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);

        AppConfigurationFile.Save(
            configPath,
            AppConfigurationFile.CreateDefault(),
            NullLogger.Instance
        );

        using var json = JsonDocument.Parse(File.ReadAllText(configPath));
        var input = json.RootElement.GetProperty("input");

        Assert.Equal(2, input.GetProperty("version").GetInt32());
        Assert.False(input.TryGetProperty("activeProfile", out _));
        Assert.False(input.TryGetProperty("profiles", out _));
        Assert.Equal(3, input.EnumerateObject().Count());
        AssertSectionHasDefaultProfile(input.GetProperty("keyboard"), "bindings");
        AssertSectionHasDefaultProfile(input.GetProperty("gamepad"), "bindings");
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

    private static void AssertSectionHasDefaultProfile(JsonElement section, string bindingsProperty)
    {
        Assert.Equal(2, section.EnumerateObject().Count());
        Assert.Equal("default", section.GetProperty("activeProfile").GetString());
        var profile = section.GetProperty("profiles").GetProperty("default");
        Assert.Single(profile.EnumerateObject());
        Assert.Equal(JsonValueKind.Array, profile.GetProperty(bindingsProperty).ValueKind);
    }
}
