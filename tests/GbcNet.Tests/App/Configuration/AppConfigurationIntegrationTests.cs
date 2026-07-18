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
using GbcNet.Core.Joypad;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog.Extensions.Logging;

namespace GbcNet.Tests.App.Configuration;

public sealed class AppConfigurationIntegrationTests
{
    [Fact]
    public void Load_CreatesDefaultV2JsonConfigFile()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);

        var startupConfiguration = StartupConfigurationLoader.Load(configPath, NullLogger.Instance);

        Assert.Null(startupConfiguration.StartupErrorMessage);
        using var configJson = JsonDocument.Parse(File.ReadAllText(configPath));
        var input = configJson.RootElement.GetProperty("input");
        Assert.Equal(2, input.GetProperty("version").GetInt32());
        Assert.True(input.TryGetProperty("keyboard", out var keyboard));
        Assert.True(input.TryGetProperty("gamepad", out var gamepad));
        Assert.False(input.TryGetProperty("activeProfile", out _));
        Assert.False(input.TryGetProperty("profiles", out _));
        Assert.Equal("default", keyboard.GetProperty("activeProfile").GetString());
        Assert.Equal("default", gamepad.GetProperty("activeProfile").GetString());
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
        Assert.True(startupConfiguration.BootRomOptions.DmgBootRom.IsEmpty);
        AssertInputConfigIsValid(startupConfiguration.InputConfig);
    }

    [Fact]
    public void Load_MalformedConfigWritesWarningToRollingLog()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        var logFilePath = Path.Combine(tempDirectory.Path, "gbcnet-.log");
        Directory.CreateDirectory(tempDirectory.Path);
        File.WriteAllText(configPath, "{");

        using (var fileLogger = GbcNet.App.Program.CreateLogger(logFilePath))
        using (var loggerFactory = new SerilogLoggerFactory(fileLogger, dispose: false))
        {
            _ = StartupConfigurationLoader.Load(
                configPath,
                loggerFactory.CreateLogger("GbcNet.App.Configuration.StartupConfigurationLoader")
            );
        }

        var rollingLogPath = Assert.Single(Directory.GetFiles(tempDirectory.Path, "gbcnet-*.log"));
        Assert.Contains(
            "Startup configuration required 1 fallback(s).",
            File.ReadAllText(rollingLogPath),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Load_ReadsBootRomFilesFromConfig()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        Directory.CreateDirectory(tempDirectory.Path);
        File.WriteAllBytes(
            Path.Combine(tempDirectory.Path, "dmg.bin"),
            BootRomTestFactory.CreateDmg(0xD0)
        );
        File.WriteAllBytes(
            Path.Combine(tempDirectory.Path, "cgb.bin"),
            BootRomTestFactory.CreateCgb(0xC0)
        );
        File.WriteAllBytes(
            Path.Combine(tempDirectory.Path, "sgb.bin"),
            BootRomTestFactory.CreateSgb(0x50)
        );
        AppConfigurationFile.Save(
            configPath,
            CreateConfig("dmg.bin", "cgb.bin", "sgb.bin"),
            NullLogger.Instance
        );

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
    public void Load_UsesActiveKeyboardProfileAndDefaultGamepadProfile()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        var input = CreateInputWithAlternateKeyboardProfile();
        AppConfigurationFile.Save(configPath, new AppConfig { Input = input }, NullLogger.Instance);

        var startupConfiguration = StartupConfigurationLoader.Load(configPath, NullLogger.Instance);
        var inputMap = InputMap.FromConfig(startupConfiguration.InputConfig);

        Assert.Null(startupConfiguration.StartupErrorMessage);
        Assert.Equal("alternate", startupConfiguration.InputConfig.Keyboard.ActiveProfile);
        Assert.Equal(
            InputConfig.DefaultProfileName,
            startupConfiguration.InputConfig.Gamepad.ActiveProfile
        );
        Assert.Equal(8, inputMap.KeyboardBindings.Count);
        Assert.Equal(4, inputMap.GamepadBindings.Count);
        Assert.Contains(
            inputMap.KeyboardBindings,
            binding => binding.Button is JoypadButton.B && binding.Key is Key.K
        );
    }

    [Fact]
    public void Load_ReadsEmulationFastForwardConfig()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        AppConfigurationFile.Save(
            configPath,
            new AppConfig
            {
                Emulation = new()
                {
                    FastForwardEnabled = true,
                    FastForwardSpeed = EmulationSpeed.Eight,
                },
            },
            NullLogger.Instance
        );

        var startupConfiguration = StartupConfigurationLoader.Load(configPath, NullLogger.Instance);

        Assert.Null(startupConfiguration.StartupErrorMessage);
        Assert.True(startupConfiguration.EmulationConfig.FastForwardEnabled);
        Assert.Equal(EmulationSpeed.Eight, startupConfiguration.EmulationConfig.FastForwardSpeed);
    }

    [Fact]
    public void SaveEmulationConfig_PreservesV2InputAndBootRoms()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        AppConfigurationFile.Save(
            configPath,
            new AppConfig
            {
                Input = CreateInputWithAlternateKeyboardProfile(),
                BootRoms = new BootRomConfig("old-dmg.bin"),
            },
            NullLogger.Instance
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

        var appConfig = AppConfigurationFile.Load(configPath);
        using var configJson = JsonDocument.Parse(File.ReadAllText(configPath));
        var input = configJson.RootElement.GetProperty("input");

        Assert.True(appConfig.Emulation.FastForwardEnabled);
        Assert.Equal(EmulationSpeed.Eight, appConfig.Emulation.FastForwardSpeed);
        Assert.Equal("alternate", appConfig.Input.Keyboard.ActiveProfile);
        Assert.Equal(InputConfig.DefaultProfileName, appConfig.Input.Gamepad.ActiveProfile);
        Assert.Equal("old-dmg.bin", appConfig.BootRoms.DmgPath);
        Assert.Equal(2, input.GetProperty("version").GetInt32());
        Assert.True(input.TryGetProperty("keyboard", out _));
        Assert.True(input.TryGetProperty("gamepad", out _));
        Assert.False(input.TryGetProperty("activeProfile", out _));
    }

    [Fact]
    public void Load_ReportsInvalidBootRomSizeAndKeepsOtherModels()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        Directory.CreateDirectory(tempDirectory.Path);
        File.WriteAllBytes(Path.Combine(tempDirectory.Path, "dmg.bin"), new byte[255]);
        File.WriteAllBytes(
            Path.Combine(tempDirectory.Path, "cgb.bin"),
            BootRomTestFactory.CreateCgb(0xC0)
        );
        AppConfigurationFile.Save(
            configPath,
            CreateConfig("dmg.bin", "cgb.bin", sgbBootRomPath: null),
            NullLogger.Instance
        );

        var startupConfiguration = StartupConfigurationLoader.Load(configPath, NullLogger.Instance);

        Assert.Contains(
            "DMG boot ROM must be 256 bytes",
            startupConfiguration.StartupErrorMessage,
            StringComparison.Ordinal
        );
        Assert.True(startupConfiguration.BootRomOptions.DmgBootRom.IsEmpty);
        Assert.Equal(
            BootRomOptions.CgbBootRomSize,
            startupConfiguration.BootRomOptions.CgbBootRom.Length
        );
        Assert.Equal(0xC0, startupConfiguration.BootRomOptions.CgbBootRom.Span[0]);
        Assert.True(startupConfiguration.BootRomOptions.SgbBootRom.IsEmpty);
    }

    [Fact]
    public void Load_ReportsMissingBootRomFileAndFallsBackToEmptyBootRoms()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        AppConfigurationFile.Save(
            configPath,
            CreateConfig("missing-dmg.bin", cgbBootRomPath: null, sgbBootRomPath: null),
            NullLogger.Instance
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
    public void InputValidation_RejectsMalformedKeyboardAndGamepadSections()
    {
        var config = new InputConfig
        {
            Version = InputConfig.SupportedVersion,
            Keyboard = null!,
            Gamepad = null!,
        };

        var validation = InputConfigValidator.Validate(config);

        Assert.Contains(
            validation,
            error => error.Contains("Keyboard input config is malformed", StringComparison.Ordinal)
        );
        Assert.Contains(
            validation,
            error => error.Contains("Gamepad input config is malformed", StringComparison.Ordinal)
        );
    }

    private static AppConfig CreateConfig(
        string? dmgBootRomPath,
        string? cgbBootRomPath,
        string? sgbBootRomPath
    ) => new() { BootRoms = new BootRomConfig(dmgBootRomPath, cgbBootRomPath, sgbBootRomPath) };

    private static InputConfig CreateInputWithAlternateKeyboardProfile()
    {
        var input = AppConfigurationFile.CreateDefaultInputConfig();
        var defaultProfile = input.Keyboard.Profiles[InputConfig.DefaultProfileName];
        input.Keyboard = new KeyboardInputConfig
        {
            ActiveProfile = "alternate",
            Profiles = new Dictionary<string, KeyboardProfileConfig>(StringComparer.Ordinal)
            {
                [InputConfig.DefaultProfileName] = defaultProfile,
                ["alternate"] = new()
                {
                    Bindings =
                    [
                        new("Up", "Up"),
                        new("Down", "Down"),
                        new("Left", "Left"),
                        new("Right", "Right"),
                        new("A", "J"),
                        new("B", "K"),
                        new("Start", "Enter"),
                        new("Select", "Back"),
                    ],
                },
            },
        };
        return input;
    }

    private static void AssertInputConfigIsValid(InputConfig config)
    {
        var validation = InputConfigValidator.Validate(config);

        Assert.Empty(validation);
    }
}
