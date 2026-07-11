// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using Avalonia.Input;
using GbcNet.App.Configuration;
using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.App.Configuration.Sections.Emulation;
using GbcNet.App.Configuration.Sections.Input;
using GbcNet.App.Emulation;
using GbcNet.App.Input;
using GbcNet.Core;
using GbcNet.Core.Hardware;
using GbcNet.Core.Joypad;
using Microsoft.Extensions.Logging.Abstractions;

namespace GbcNet.Tests.App.Configuration;

public sealed class AppConfigurationIntegrationTests
{
    [Fact]
    public void Load_CreatesDefaultJsonConfigFile()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);

        var startupConfiguration = StartupConfigurationLoader.Load(configPath, NullLogger.Instance);

        Assert.Null(startupConfiguration.StartupErrorMessage);
        Assert.True(File.Exists(configPath));
        using var configJson = JsonDocument.Parse(File.ReadAllText(configPath));
        var root = configJson.RootElement;
        Assert.True(root.TryGetProperty("input", out var input));
        Assert.Equal(1, input.GetProperty("version").GetInt32());
        Assert.True(input.TryGetProperty("activeProfile", out var activeProfile));
        Assert.Equal(JsonValueKind.String, activeProfile.ValueKind);
        Assert.True(
            input.GetProperty("profiles").TryGetProperty(activeProfile.GetString()!, out _)
        );
        Assert.True(root.TryGetProperty("bootRoms", out var bootRoms));
        Assert.Equal(JsonValueKind.Object, bootRoms.ValueKind);
        AssertInputConfigIsValid(startupConfiguration.InputConfig);
    }

    [Fact]
    public void Load_MalformedConfigReportsParseErrorAndUsesDefaultFallback()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);

        Directory.CreateDirectory(tempDirectory.Path);
        File.WriteAllText(configPath, "{");

        var startupConfiguration = StartupConfigurationLoader.Load(configPath, NullLogger.Instance);

        Assert.Contains(
            "Configuration file could not be parsed",
            startupConfiguration.StartupErrorMessage,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            "Input profile",
            startupConfiguration.StartupErrorMessage,
            StringComparison.Ordinal
        );
        Assert.True(startupConfiguration.BootRomOptions.DmgBootRom.IsEmpty);
        AssertInputConfigIsValid(startupConfiguration.InputConfig);
    }

    [Fact]
    public void Load_ReadsBootRomFilesFromConfig()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);

        Directory.CreateDirectory(tempDirectory.Path);
        File.WriteAllBytes(
            Path.Combine(tempDirectory.Path, "dmg.bin"),
            CreateBootRom(BootRomOptions.DmgBootRomSize, 0xD0)
        );
        File.WriteAllBytes(
            Path.Combine(tempDirectory.Path, "cgb.bin"),
            CreateBootRom(BootRomOptions.CgbBootRomSize, 0xC0)
        );
        File.WriteAllBytes(
            Path.Combine(tempDirectory.Path, "sgb.bin"),
            CreateBootRom(BootRomOptions.SgbBootRomSize, 0x50)
        );
        File.WriteAllText(configPath, CreateConfig("dmg.bin", "cgb.bin", "sgb.bin"));

        var startupConfiguration = StartupConfigurationLoader.Load(configPath, NullLogger.Instance);

        Assert.Null(startupConfiguration.StartupErrorMessage);
        Assert.Equal(
            BootRomOptions.DmgBootRomSize,
            startupConfiguration.BootRomOptions.DmgBootRom.Length
        );
        Assert.Equal(
            BootRomOptions.CgbBootRomSize,
            startupConfiguration.BootRomOptions.CgbBootRom.Length
        );
        Assert.Equal(
            BootRomOptions.SgbBootRomSize,
            startupConfiguration.BootRomOptions.SgbBootRom.Length
        );
        Assert.Equal(0xD0, startupConfiguration.BootRomOptions.DmgBootRom.Span[0]);
        Assert.Equal(0xC0, startupConfiguration.BootRomOptions.CgbBootRom.Span[0]);
        Assert.Equal(0x50, startupConfiguration.BootRomOptions.SgbBootRom.Span[0]);
    }

    [Fact]
    public void Load_UsesActiveInputProfileFromMultipleProfiles()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);

        Directory.CreateDirectory(tempDirectory.Path);
        File.WriteAllText(
            configPath,
            """
            {
              "input": {
                "version": 1,
                "activeProfile": "alternate",
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
                  "alternate": {
                    "keyboard": [
                      { "button": "up", "key": "Up" },
                      { "button": "down", "key": "Down" },
                      { "button": "left", "key": "Left" },
                      { "button": "right", "key": "Right" },
                      { "button": "a", "key": "J" },
                      { "button": "b", "key": "K" },
                      { "button": "start", "key": "Enter" },
                      { "button": "select", "key": "Back" }
                    ]
                  }
                }
              },
              "bootRoms": {}
            }
            """
        );

        var startupConfiguration = StartupConfigurationLoader.Load(configPath, NullLogger.Instance);

        Assert.Null(startupConfiguration.StartupErrorMessage);
        Assert.Equal("alternate", startupConfiguration.InputConfig.ActiveProfile);
        Assert.Equal(2, startupConfiguration.InputConfig.Profiles.Count);
        var inputMap = InputMap.FromConfig(startupConfiguration.InputConfig);
        Assert.Equal(8, inputMap.Bindings.Count);
        Assert.Contains(
            inputMap.Bindings,
            binding => binding.Button is JoypadButton.B && binding.Key is Key.K
        );
    }

    [Fact]
    public void Load_ReadsEmulationFastForwardConfig()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);

        Directory.CreateDirectory(tempDirectory.Path);
        File.WriteAllText(
            configPath,
            """
            {
              "input": {
                "version": 1,
                "activeProfile": "default",
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
                  }
                }
              },
              "emulation": {
                "fastForwardEnabled": true,
                "fastForwardSpeed": "eight"
              },
              "bootRoms": {}
            }
            """
        );

        var startupConfiguration = StartupConfigurationLoader.Load(configPath, NullLogger.Instance);

        Assert.Null(startupConfiguration.StartupErrorMessage);
        Assert.True(startupConfiguration.EmulationConfig.FastForwardEnabled);
        Assert.Equal(EmulationSpeed.Eight, startupConfiguration.EmulationConfig.FastForwardSpeed);
    }

    [Fact]
    public void SaveEmulationConfig_WritesEmulationJsonAndPreservesInputAndBootRoms()
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
                "activeProfile": "alternate",
                "profiles": {
                  "default": {
                    "keyboard": [
                      { "button": "a", "key": "Z" }
                    ]
                  },
                  "alternate": {
                    "keyboard": [
                      { "button": "b", "key": "X" }
                    ]
                  }
                }
              },
              "bootRoms": {
                "{{BootRomConfig.JsonName(HardwareModel.Dmg)}}": "old-dmg.bin"
              }
            }
            """
        );
        var service = new AppConfigurationService(
            configPath,
            NullLogger<AppConfigurationService>.Instance
        );

        service.SaveEmulationConfig(
            new EmulationConfig
            {
                FastForwardEnabled = true,
                FastForwardSpeed = EmulationSpeed.Eight,
            }
        );

        using var configJson = JsonDocument.Parse(File.ReadAllText(configPath));
        var root = configJson.RootElement;
        var emulation = root.GetProperty("emulation");
        Assert.True(emulation.GetProperty("fastForwardEnabled").GetBoolean());
        Assert.Equal("eight", emulation.GetProperty("fastForwardSpeed").GetString());
        Assert.Equal(
            "alternate",
            root.GetProperty("input").GetProperty("activeProfile").GetString()
        );
        Assert.Equal(
            "old-dmg.bin",
            root.GetProperty("bootRoms")
                .GetProperty(BootRomConfig.JsonName(HardwareModel.Dmg))
                .GetString()
        );

        var appConfig = AppConfigurationFile.Load(configPath);
        var binding = Assert.Single(appConfig.Input.Profiles["alternate"].Keyboard);
        Assert.Equal(new KeyboardInputBindingConfig("b", "X"), binding);
        Assert.Equal("old-dmg.bin", appConfig.BootRoms.DmgPath);
    }

    [Fact]
    public void Load_ReportsInvalidBootRomSizeAndFallsBackToEmptyBootRoms()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);

        Directory.CreateDirectory(tempDirectory.Path);
        File.WriteAllBytes(Path.Combine(tempDirectory.Path, "dmg.bin"), new byte[255]);
        File.WriteAllText(configPath, CreateConfig("dmg.bin", null, null));

        var startupConfiguration = StartupConfigurationLoader.Load(configPath, NullLogger.Instance);

        Assert.Contains(
            "DMG boot ROM must be 256 bytes",
            startupConfiguration.StartupErrorMessage,
            StringComparison.Ordinal
        );
        Assert.True(startupConfiguration.BootRomOptions.DmgBootRom.IsEmpty);
        Assert.True(startupConfiguration.BootRomOptions.CgbBootRom.IsEmpty);
        Assert.True(startupConfiguration.BootRomOptions.SgbBootRom.IsEmpty);
    }

    [Fact]
    public void Load_ReportsMissingBootRomFileAndFallsBackToEmptyBootRoms()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);

        Directory.CreateDirectory(tempDirectory.Path);
        File.WriteAllText(
            configPath,
            CreateConfig("missing-dmg.bin", cgbBootRomPath: null, sgbBootRomPath: null)
        );

        var startupConfiguration = StartupConfigurationLoader.Load(configPath, NullLogger.Instance);

        Assert.Contains(
            "DMG boot ROM file could not be read",
            startupConfiguration.StartupErrorMessage,
            StringComparison.Ordinal
        );
        Assert.True(startupConfiguration.BootRomOptions.DmgBootRom.IsEmpty);
        Assert.True(startupConfiguration.BootRomOptions.CgbBootRom.IsEmpty);
        Assert.True(startupConfiguration.BootRomOptions.SgbBootRom.IsEmpty);
    }

    [Fact]
    public void InputValidation_RejectsEmptyActiveKeyboardProfile()
    {
        InputConfig config = new()
        {
            Profiles = new Dictionary<string, InputProfileConfig>(StringComparer.Ordinal)
            {
                [InputConfig.DefaultProfileName] = new(),
            },
        };

        var validation = InputConfigValidator.Validate(config);

        Assert.NotEmpty(validation);
        Assert.Contains(
            validation,
            error => error.Contains("exactly 8 keyboard bindings", StringComparison.Ordinal)
        );
    }

    private static byte[] CreateBootRom(int length, byte marker)
    {
        var bytes = new byte[length];
        bytes[0] = marker;
        return bytes;
    }

    private static string CreateConfig(
        string? dmgBootRomPath,
        string? cgbBootRomPath,
        string? sgbBootRomPath
    )
    {
        var bootRomProperties = new List<string>();
        AddBootRomProperty(HardwareModel.Dmg, dmgBootRomPath);
        AddBootRomProperty(HardwareModel.Cgb, cgbBootRomPath);
        AddBootRomProperty(HardwareModel.Sgb, sgbBootRomPath);
        var bootRomsJson = string.Join("," + Environment.NewLine, bootRomProperties);

        return $$"""
            {
              "input": {
                "version": 1,
                "activeProfile": "{{InputConfig.DefaultProfileName}}",
                "profiles": {
                  "{{InputConfig.DefaultProfileName}}": {
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
                  }
                }
              },
              "bootRoms": {
            {{bootRomsJson}}
              }
            }
            """;

        void AddBootRomProperty(HardwareModel model, string? path)
        {
            if (path is not null)
            {
                bootRomProperties.Add(
                    $"""    "{BootRomConfig.JsonName(model)}": {JsonSerializer.Serialize(path)}"""
                );
            }
        }
    }

    private static void AssertInputConfigIsValid(InputConfig config)
    {
        var validation = InputConfigValidator.Validate(config);

        Assert.False(validation.Count != 0, string.Join(Environment.NewLine, validation));
    }
}
