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
    public void SaveSettingsAndLoadBootRomConfig_RoundTripPaths()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);

        Directory.CreateDirectory(tempDirectory.Path);
        var service = new AppConfigurationService(
            configPath,
            NullLogger<AppConfigurationService>.Instance
        );

        service.SaveSettings(
            new SettingsConfig(
                new BootRomConfig("dmg.bin", "cgb.bin", "sgb.bin"),
                AppConfigurationFile.CreateDefaultInputConfig()
            )
        );

        var config = service.LoadBootRomConfig();

        Assert.Equal(new BootRomConfig("dmg.bin", "cgb.bin", "sgb.bin"), config);
    }

    [Fact]
    public void SaveSettings_WritesJsonThatRoundTripsEscapedPaths()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        const string dmgPath = "dir\\boot\"rom.bin";

        Directory.CreateDirectory(tempDirectory.Path);
        var service = new AppConfigurationService(
            configPath,
            NullLogger<AppConfigurationService>.Instance
        );

        service.SaveSettings(
            new SettingsConfig(
                new BootRomConfig(dmgPath, CgbPath: "cgb.bin"),
                AppConfigurationFile.CreateDefaultInputConfig()
            )
        );

        using var json = JsonDocument.Parse(File.ReadAllText(configPath));
        Assert.Equal(
            dmgPath,
            json.RootElement.GetProperty("bootRoms")
                .GetProperty(BootRomConfig.JsonName(HardwareModel.Dmg))
                .GetString()
        );
        Assert.Equal(new BootRomConfig(dmgPath, CgbPath: "cgb.bin"), service.LoadBootRomConfig());
    }

    [Fact]
    public void LoadSettings_LoadsBootRomsAndInputTogether()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);

        Directory.CreateDirectory(tempDirectory.Path);
        File.WriteAllText(
            configPath,
            $$"""
            {
              "input": {
                "version": 1,
                "activeProfile": "SpeedRun",
                "profiles": {
                  "default": {
                    "keyboard": [
                      { "button": "up", "key": "Up" },
                      { "button": "down", "key": "Down" },
                      { "button": "left", "key": "Left" },
                      { "button": "right", "key": "Right" },
                      { "button": "a", "key": "Z" },
                      { "button": "b", "key": "X" },
                      { "button": "start", "key": "Enter" },
                      { "button": "select", "key": "Back" }
                    ]
                  },
                  "SpeedRun": {
                    "keyboard": [
                      { "button": "up", "key": "I" },
                      { "button": "down", "key": "K" },
                      { "button": "left", "key": "J" },
                      { "button": "right", "key": "L" },
                      { "button": "a", "key": "A" },
                      { "button": "b", "key": "S" },
                      { "button": "start", "key": "D" },
                      { "button": "select", "key": "F" }
                    ]
                  }
                }
              },
              "bootRoms": {
                "{{BootRomConfig.JsonName(HardwareModel.Dmg)}}": "dmg.bin",
                "{{BootRomConfig.JsonName(HardwareModel.Cgb)}}": "cgb.bin"
              }
            }
            """
        );
        var service = new AppConfigurationService(
            configPath,
            NullLogger<AppConfigurationService>.Instance
        );

        var settings = service.LoadSettings();

        Assert.Equal(new BootRomConfig("dmg.bin", "cgb.bin"), settings.BootRoms);
        Assert.Equal("SpeedRun", settings.Input.ActiveProfile);
        Assert.True(settings.Input.Profiles.ContainsKey("default"));
        Assert.True(settings.Input.Profiles.ContainsKey("SpeedRun"));
    }

    [Fact]
    public void LoadSettings_ReturnsStrictInvalidInputForPresenterValidation()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);

        Directory.CreateDirectory(tempDirectory.Path);
        File.WriteAllText(
            configPath,
            $$"""
            {
              "input": {
                "version": 1,
                "activeProfile": "default",
                "profiles": {
                  "default": {
                    "keyboard": [
                      { "button": "a", "key": "Z" }
                    ]
                  }
                }
              },
              "bootRoms": {
                "{{BootRomConfig.JsonName(HardwareModel.Dmg)}}": "dmg.bin"
              }
            }
            """
        );
        var service = new AppConfigurationService(
            configPath,
            NullLogger<AppConfigurationService>.Instance
        );

        var settings = service.LoadSettings();
        var errors = InputConfigValidator.Validate(settings.Input);

        Assert.Equal(new BootRomConfig("dmg.bin"), settings.BootRoms);
        Assert.Contains(
            errors,
            error => error.Contains("exactly 8 keyboard bindings", StringComparison.Ordinal)
        );
        Assert.Single(settings.Input.Profiles["default"].Keyboard);
    }

    [Fact]
    public void SaveSettings_SavesBothSectionsAndPreservesEmulation()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);

        Directory.CreateDirectory(tempDirectory.Path);
        AppConfigurationFile.Save(
            configPath,
            new AppConfig
            {
                BootRoms = new BootRomConfig("old-dmg.bin"),
                Emulation = new() { FastForwardEnabled = true },
                Input = CreateStrictInput("Alternate"),
            },
            NullLogger<AppConfigurationService>.Instance
        );
        var service = new AppConfigurationService(
            configPath,
            NullLogger<AppConfigurationService>.Instance
        );

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
        Assert.Equal("SpeedRun", appConfig.Input.ActiveProfile);
        Assert.Equal(["default", "SpeedRun"], appConfig.Input.Profiles.Keys);
        Assert.Equal(8, appConfig.Input.Profiles["default"].Keyboard.Count);
        Assert.Equal(8, appConfig.Input.Profiles["SpeedRun"].Keyboard.Count);
    }

    [Fact]
    public void SaveSettings_InvalidInputThrowsAndLeavesFileUntouched()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);

        Directory.CreateDirectory(tempDirectory.Path);
        File.WriteAllText(
            configPath,
            """
            {
              "bootRoms": {
                "dmg": "original-dmg.bin"
              }
            }
            """
        );
        var originalBytes = File.ReadAllBytes(configPath);
        var service = new AppConfigurationService(
            configPath,
            NullLogger<AppConfigurationService>.Instance
        );
        var invalidInput = new InputConfig
        {
            ActiveProfile = "SpeedRun",
            Profiles = new Dictionary<string, InputProfileConfig>(StringComparer.Ordinal)
            {
                ["default"] = CreateProfile(
                    "Up",
                    "Down",
                    "Left",
                    "Right",
                    "Z",
                    "X",
                    "Enter",
                    "Back"
                ),
                ["SpeedRun"] = new()
                {
                    Keyboard =
                    [
                        new("down", "K"),
                        new("left", "J"),
                        new("right", "L"),
                        new("a", "A"),
                        new("b", "S"),
                        new("start", "D"),
                        new("select", "F"),
                    ],
                },
            },
        };

        var exception = Assert.Throws<ConfigurationException>(() =>
            service.SaveSettings(new SettingsConfig(new BootRomConfig("new-dmg.bin"), invalidInput))
        );

        Assert.Contains("exactly 8 keyboard bindings", exception.Message, StringComparison.Ordinal);
        Assert.Equal(originalBytes, File.ReadAllBytes(configPath));
    }

    [Fact]
    public void LoadBootRomOptions_ResolvesRelativePathsFromConfigDirectory()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);

        Directory.CreateDirectory(tempDirectory.Path);
        File.WriteAllBytes(
            Path.Combine(tempDirectory.Path, "dmg.bin"),
            CreateBootRom(BootRomOptions.DmgBootRomSize, marker: 0xD0)
        );
        File.WriteAllBytes(
            Path.Combine(tempDirectory.Path, "cgb.bin"),
            CreateBootRom(BootRomOptions.CgbBootRomSize, marker: 0xC0)
        );
        File.WriteAllBytes(
            Path.Combine(tempDirectory.Path, "sgb.bin"),
            CreateBootRom(BootRomOptions.SgbBootRomSize, marker: 0x50)
        );
        var service = new AppConfigurationService(
            configPath,
            NullLogger<AppConfigurationService>.Instance
        );
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
        File.WriteAllText(
            configPath,
            """
            {
              "bootRoms": {
                "invalid": "boot.bin"
              }
            }
            """
        );
        var service = new AppConfigurationService(
            configPath,
            NullLogger<AppConfigurationService>.Instance
        );

        var exception = Assert.Throws<ConfigurationException>(() => service.LoadBootRomConfig());

        Assert.Contains("could not be parsed", exception.Message, StringComparison.Ordinal);
    }

    private static InputConfig CreateStrictInput(string activeProfileName) =>
        new()
        {
            ActiveProfile = activeProfileName,
            Profiles = new Dictionary<string, InputProfileConfig>(StringComparer.Ordinal)
            {
                ["default"] = CreateProfile(
                    "Up",
                    "Down",
                    "Left",
                    "Right",
                    "Z",
                    "X",
                    "Enter",
                    "Back"
                ),
                [activeProfileName] = CreateProfile("I", "K", "J", "L", "A", "S", "D", "F"),
            },
        };

    private static InputProfileConfig CreateProfile(
        string up,
        string down,
        string left,
        string right,
        string a,
        string b,
        string start,
        string select
    ) =>
        new()
        {
            Keyboard =
            [
                new("up", up),
                new("down", down),
                new("left", left),
                new("right", right),
                new("a", a),
                new("b", b),
                new("start", start),
                new("select", select),
            ],
        };

    private static byte[] CreateBootRom(int length, byte marker)
    {
        var bytes = new byte[length];
        bytes[0] = marker;
        return bytes;
    }
}
