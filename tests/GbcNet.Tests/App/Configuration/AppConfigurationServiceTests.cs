// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using GbcNet.App.Configuration;
using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.App.Configuration.Sections.Input;
using GbcNet.Core;
using GbcNet.Core.Hardware;
using Microsoft.Extensions.Logging.Abstractions;

namespace GbcNet.Tests.App.Configuration;

public sealed class AppConfigurationServiceTests
{
    [Fact]
    public void SaveSettingsAndLoadBootRomConfig_RoundTripsPaths()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        Directory.CreateDirectory(tempDirectory.Path);
        File.WriteAllBytes(
            Path.Combine(tempDirectory.Path, "dmg.bin"),
            BootRomTestFactory.CreateDmg(marker: 0xD0)
        );
        File.WriteAllBytes(
            Path.Combine(tempDirectory.Path, "cgb.bin"),
            BootRomTestFactory.CreateCgb(marker: 0xC0)
        );
        File.WriteAllBytes(
            Path.Combine(tempDirectory.Path, "sgb.bin"),
            BootRomTestFactory.CreateSgb(marker: 0x50)
        );
        var service = CreateService(configPath);

        service.SaveSettings(
            new SettingsConfig(
                new BootRomConfig("dmg.bin", "cgb.bin", "sgb.bin"),
                AppConfigurationFile.CreateDefaultInputConfig()
            )
        );

        Assert.Equal(
            new BootRomConfig("dmg.bin", "cgb.bin", "sgb.bin"),
            service.LoadBootRomConfig()
        );
    }

    [Fact]
    public void LoadSettings_LoadsIndependentInputSections()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        var input = CreateStrictInput("SpeedRun");
        input.Keyboard.ActiveProfile = "SpeedRun";
        input.Gamepad.ActiveProfile = InputConfig.DefaultProfileName;
        AppConfigurationFile.Save(
            configPath,
            new AppConfig { BootRoms = new BootRomConfig("dmg.bin", "cgb.bin"), Input = input },
            NullLogger.Instance
        );
        var service = CreateService(configPath);

        var settings = service.LoadSettings();

        Assert.Equal(new BootRomConfig("dmg.bin", "cgb.bin"), settings.BootRoms);
        Assert.Equal("SpeedRun", settings.Input.Keyboard.ActiveProfile);
        Assert.Equal(InputConfig.DefaultProfileName, settings.Input.Gamepad.ActiveProfile);
        Assert.True(settings.Input.Keyboard.Profiles.ContainsKey("SpeedRun"));
        Assert.True(settings.Input.Gamepad.Profiles.ContainsKey("SpeedRun"));
    }

    [Fact]
    public void SaveSettings_SavesBothSectionsAndPreservesEmulation()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        Directory.CreateDirectory(tempDirectory.Path);
        File.WriteAllBytes(
            Path.Combine(tempDirectory.Path, "new-dmg.bin"),
            BootRomTestFactory.CreateDmg(marker: 0xD0)
        );
        File.WriteAllBytes(
            Path.Combine(tempDirectory.Path, "new-cgb.bin"),
            BootRomTestFactory.CreateCgb(marker: 0xC0)
        );
        File.WriteAllBytes(
            Path.Combine(tempDirectory.Path, "new-sgb.bin"),
            BootRomTestFactory.CreateSgb(marker: 0x50)
        );
        AppConfigurationFile.Save(
            configPath,
            new AppConfig
            {
                BootRoms = new BootRomConfig("old-dmg.bin"),
                Emulation = new() { FastForwardEnabled = true },
                Input = CreateStrictInput("Alternate"),
            },
            NullLogger.Instance
        );
        var service = CreateService(configPath);

        service.SaveSettings(
            new SettingsConfig(
                new BootRomConfig("new-dmg.bin", "new-cgb.bin", "new-sgb.bin"),
                CreateStrictInput("SpeedRun")
            )
        );

        var appConfig = AppConfigurationFile.Load(configPath);

        Assert.Equal(
            new BootRomConfig("new-dmg.bin", "new-cgb.bin", "new-sgb.bin"),
            appConfig.BootRoms
        );
        Assert.True(appConfig.Emulation.FastForwardEnabled);
        Assert.Equal("SpeedRun", appConfig.Input.Keyboard.ActiveProfile);
        Assert.Equal("SpeedRun", appConfig.Input.Gamepad.ActiveProfile);
        Assert.True(appConfig.Input.Keyboard.Profiles.ContainsKey("SpeedRun"));
        Assert.True(appConfig.Input.Gamepad.Profiles.ContainsKey("SpeedRun"));
    }

    [Fact]
    public void SaveSettings_InvalidInputThrowsAndLeavesFileUntouched()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        var originalConfig = AppConfigurationFile.CreateDefault();
        originalConfig.BootRoms = new BootRomConfig("original-dmg.bin");
        AppConfigurationFile.Save(configPath, originalConfig, NullLogger.Instance);
        var originalBytes = File.ReadAllBytes(configPath);
        var invalidInput = AppConfigurationFile.CreateDefaultInputConfig();
        invalidInput.Gamepad = new GamepadInputConfig
        {
            ActiveProfile = InputConfig.DefaultProfileName,
            Profiles = new Dictionary<string, GamepadProfileConfig>(StringComparer.Ordinal)
            {
                [InputConfig.DefaultProfileName] = new() { Bindings = [new("A", "East")] },
            },
        };
        var service = CreateService(configPath);

        var exception = Assert.Throws<ConfigurationException>(() =>
            service.SaveSettings(new SettingsConfig(new BootRomConfig("new-dmg.bin"), invalidInput))
        );

        Assert.Contains("exactly 4 bindings", exception.Message, StringComparison.Ordinal);
        Assert.Equal(originalBytes, File.ReadAllBytes(configPath));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"input\":{\"version\":1,\"activeProfile\":\"default\",\"profiles\":{}}}")]
    public void SaveSettings_WhenExistingConfigIsUnreadableOrOld_ReplacesItWithV2(
        string existingContents
    )
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        Directory.CreateDirectory(tempDirectory.Path);
        File.WriteAllText(configPath, existingContents);
        File.WriteAllBytes(
            Path.Combine(tempDirectory.Path, "replacement-dmg.bin"),
            BootRomTestFactory.CreateDmg(marker: 0xD0)
        );
        var service = CreateService(configPath);

        service.SaveSettings(
            new SettingsConfig(
                new BootRomConfig("replacement-dmg.bin"),
                AppConfigurationFile.CreateDefaultInputConfig()
            )
        );

        var saved = AppConfigurationFile.Load(configPath);
        using var json = JsonDocument.Parse(File.ReadAllText(configPath));
        var input = json.RootElement.GetProperty("input");

        Assert.Equal(new BootRomConfig("replacement-dmg.bin"), saved.BootRoms);
        Assert.Empty(InputConfigValidator.Validate(saved.Input));
        Assert.Equal(2, input.GetProperty("version").GetInt32());
        Assert.True(input.TryGetProperty("keyboard", out _));
        Assert.True(input.TryGetProperty("gamepad", out _));
        Assert.False(input.TryGetProperty("activeProfile", out _));
        Assert.False(File.Exists(configPath + ".tmp"));
    }

    [Fact]
    public void SaveSettings_InvalidBootRomPreservesValidPathsAndSavesOtherSections()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        Directory.CreateDirectory(tempDirectory.Path);
        File.WriteAllBytes(
            Path.Combine(tempDirectory.Path, "current-dmg.bin"),
            BootRomTestFactory.CreateDmg(marker: 0xD0)
        );
        File.WriteAllBytes(
            Path.Combine(tempDirectory.Path, "new-cgb.bin"),
            BootRomTestFactory.CreateCgb(marker: 0xC0)
        );
        AppConfigurationFile.Save(
            configPath,
            new AppConfig
            {
                BootRoms = new BootRomConfig("current-dmg.bin"),
                Input = AppConfigurationFile.CreateDefaultInputConfig(),
            },
            NullLogger.Instance
        );
        var service = CreateService(configPath);

        var errors = service.SaveSettings(
            new SettingsConfig(
                new BootRomConfig("missing-dmg.bin", "new-cgb.bin"),
                CreateStrictInput("SpeedRun")
            )
        );

        var saved = AppConfigurationFile.Load(configPath);
        Assert.Single(errors);
        Assert.Contains("DMG boot ROM file could not be read", errors[0], StringComparison.Ordinal);
        Assert.Equal(new BootRomConfig("current-dmg.bin", "new-cgb.bin"), saved.BootRoms);
        Assert.Equal("SpeedRun", saved.Input.Keyboard.ActiveProfile);
        Assert.Equal("SpeedRun", saved.Input.Gamepad.ActiveProfile);
    }

    [Fact]
    public void LoadBootRomOptions_ResolvesRelativePathsFromConfigDirectory()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        Directory.CreateDirectory(tempDirectory.Path);
        File.WriteAllBytes(
            Path.Combine(tempDirectory.Path, "dmg.bin"),
            BootRomTestFactory.CreateDmg(marker: 0xD0)
        );
        File.WriteAllBytes(
            Path.Combine(tempDirectory.Path, "cgb.bin"),
            BootRomTestFactory.CreateCgb(marker: 0xC0)
        );
        File.WriteAllBytes(
            Path.Combine(tempDirectory.Path, "sgb.bin"),
            BootRomTestFactory.CreateSgb(marker: 0x50)
        );
        var service = CreateService(configPath);
        service.SaveSettings(
            new SettingsConfig(
                new BootRomConfig("dmg.bin", "cgb.bin", "sgb.bin"),
                AppConfigurationFile.CreateDefaultInputConfig()
            )
        );

        var options = service.LoadBootRomOptions();

        Assert.Equal(BootRomOptions.DmgBootRomSize, options.DmgBootRom.Length);
        Assert.Equal(BootRomOptions.CgbBootRomSize, options.CgbBootRom.Length);
        Assert.Equal(BootRomOptions.SgbBootRomSize, options.SgbBootRom.Length);
        Assert.Equal(0xD0, options.DmgBootRom.Span[0]);
        Assert.Equal(0xC0, options.CgbBootRom.Span[0]);
        Assert.Equal(0x50, options.SgbBootRom.Span[0]);
    }

    [Fact]
    public void BootRomConfig_MapsKnownModelsAndRejectsUnsupportedModel()
    {
        var config = new BootRomConfig("dmg.bin", "cgb.bin", "sgb.bin");

        Assert.Equal("dmg.bin", config.GetPath(HardwareModel.Dmg));
        Assert.Equal("cgb.bin", config.GetPath(HardwareModel.Cgb));
        Assert.Equal("sgb.bin", config.GetPath(HardwareModel.Sgb));
        Assert.Equal(BootRomOptions.DmgBootRomSize, BootRomConfig.Size(HardwareModel.Dmg));
        Assert.Equal(BootRomOptions.CgbBootRomSize, BootRomConfig.Size(HardwareModel.Cgb));
        Assert.Equal(BootRomOptions.SgbBootRomSize, BootRomConfig.Size(HardwareModel.Sgb));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            config.GetPath((HardwareModel)int.MaxValue)
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BootRomConfig.Size((HardwareModel)int.MaxValue)
        );
    }

    [Fact]
    public void LoadBootRomConfig_ThrowsForUnknownBootRomProperty()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        Directory.CreateDirectory(tempDirectory.Path);
        File.WriteAllText(configPath, """{ "bootRoms": { "invalid": "boot.bin" } }""");
        var service = CreateService(configPath);

        var exception = Assert.Throws<ConfigurationException>(() => service.LoadBootRomConfig());

        Assert.Contains("could not be parsed", exception.Message, StringComparison.Ordinal);
    }

    private static AppConfigurationService CreateService(string configPath) =>
        new(configPath, NullLogger<AppConfigurationService>.Instance);

    private static InputConfig CreateStrictInput(string activeProfileName)
    {
        var input = AppConfigurationFile.CreateDefaultInputConfig();
        var defaultKeyboardProfile = input.Keyboard.Profiles[InputConfig.DefaultProfileName];
        var defaultGamepadProfile = input.Gamepad.Profiles[InputConfig.DefaultProfileName];
        input.Keyboard.ActiveProfile = activeProfileName;
        input.Gamepad.ActiveProfile = activeProfileName;
        input.Keyboard.Profiles = new Dictionary<string, KeyboardProfileConfig>(
            StringComparer.Ordinal
        )
        {
            [InputConfig.DefaultProfileName] = defaultKeyboardProfile,
            [activeProfileName] = defaultKeyboardProfile,
        };
        input.Gamepad.Profiles = new Dictionary<string, GamepadProfileConfig>(
            StringComparer.Ordinal
        )
        {
            [InputConfig.DefaultProfileName] = defaultGamepadProfile,
            [activeProfileName] = defaultGamepadProfile,
        };
        return input;
    }
}
