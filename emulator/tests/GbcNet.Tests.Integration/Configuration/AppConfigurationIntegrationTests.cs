// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using Avalonia.Input;
using GbcNet.App.Configuration;
using GbcNet.App.Configuration.Sections.Appearance;
using GbcNet.App.Configuration.Sections.Audio;
using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.App.Configuration.Sections.Emulation;
using GbcNet.App.Configuration.Sections.Input;
using GbcNet.App.Configuration.Sections.Library;
using GbcNet.App.Emulation;
using GbcNet.App.Input;
using GbcNet.Core;
using GbcNet.Core.Joypad;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog.Extensions.Logging;

namespace GbcNet.Tests.Integration.Configuration;

public sealed class AppConfigurationIntegrationTests
{
    [Fact]
    public void Load_CreatesDefaultV2JsonConfigFile()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);

        var startupConfiguration = StartupConfigurationLoader.Load(configPath, NullLogger.Instance);

        startupConfiguration.StartupErrorMessage.Should().BeNull();
        using var configJson = JsonDocument.Parse(File.ReadAllText(configPath));
        var input = configJson.RootElement.GetProperty("input");
        input.GetProperty("version").GetInt32().Should().Be(2);
        input.TryGetProperty("keyboard", out var keyboard).Should().BeTrue();
        input.TryGetProperty("gamepad", out var gamepad).Should().BeTrue();
        input.TryGetProperty("activeProfile", out _).Should().BeFalse();
        input.TryGetProperty("profiles", out _).Should().BeFalse();
        keyboard.GetProperty("activeProfile").GetString().Should().Be("default");
        gamepad.GetProperty("activeProfile").GetString().Should().Be("default");
        configJson
            .RootElement.GetProperty("library")
            .GetProperty("viewMode")
            .GetString()
            .Should()
            .Be("grid");
        configJson
            .RootElement.GetProperty("appearance")
            .GetProperty("theme")
            .GetString()
            .Should()
            .Be("system");
        startupConfiguration.AppearanceConfig.Theme.Should().Be(ThemeMode.System);
        startupConfiguration.LibraryConfig.ViewMode.Should().Be(LibraryViewMode.Grid);
    }

    [Fact]
    public void Load_OldConfigWithoutAudioUsesDefaultAudio()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        Directory.CreateDirectory(tempDirectory.Path);
        File.WriteAllText(configPath, """{"emulation":{"fastForwardEnabled":true}}""");

        var startupConfiguration = StartupConfigurationLoader.Load(configPath, NullLogger.Instance);

        startupConfiguration.StartupErrorMessage.Should().BeNull();
        startupConfiguration.AppearanceConfig.Theme.Should().Be(ThemeMode.System);
        startupConfiguration.AudioConfig.Should().Be(new AudioConfig());
        startupConfiguration.EmulationConfig.FastForwardEnabled.Should().BeTrue();
        startupConfiguration.LibraryConfig.ViewMode.Should().Be(LibraryViewMode.Grid);
    }

    [Fact]
    public void Load_NullAppearanceUsesSystemTheme()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        Directory.CreateDirectory(tempDirectory.Path);
        File.WriteAllText(configPath, """{"appearance":null}""");

        var startupConfiguration = StartupConfigurationLoader.Load(configPath, NullLogger.Instance);

        startupConfiguration.StartupErrorMessage.Should().BeNull();
        startupConfiguration.AppearanceConfig.Theme.Should().Be(ThemeMode.System);
    }

    [Fact]
    public void Load_InvalidLibraryViewModeUsesDefaultFallback()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        Directory.CreateDirectory(tempDirectory.Path);
        File.WriteAllText(configPath, """{"library":{"viewMode":"gallery"}}""");

        var startupConfiguration = StartupConfigurationLoader.Load(configPath, NullLogger.Instance);

        startupConfiguration
            .StartupErrorMessage.Should()
            .Contain("Configuration file could not be parsed");
        startupConfiguration.LibraryConfig.ViewMode.Should().Be(LibraryViewMode.Grid);
    }

    [Fact]
    public void Load_MalformedConfigReportsParseErrorAndUsesDefaultFallback()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        Directory.CreateDirectory(tempDirectory.Path);
        File.WriteAllText(configPath, "{");

        var startupConfiguration = StartupConfigurationLoader.Load(configPath, NullLogger.Instance);

        startupConfiguration
            .StartupErrorMessage.Should()
            .Contain("Configuration file could not be parsed");
        startupConfiguration.BootRomOptions.DmgBootRom.IsEmpty.Should().BeTrue();
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

        using (var fileLogger = App.Program.CreateLogger(logFilePath))
        using (var loggerFactory = new SerilogLoggerFactory(fileLogger, dispose: false))
        {
            _ = StartupConfigurationLoader.Load(
                configPath,
                loggerFactory.CreateLogger("GbcNet.App.Configuration.StartupConfigurationLoader")
            );
        }

        var rollingLogPath = Directory
            .GetFiles(tempDirectory.Path, "gbcnet-*.log")
            .Should()
            .ContainSingle()
            .Which;
        File.ReadAllText(rollingLogPath)
            .Should()
            .Contain("Startup configuration required 1 fallback(s).");
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

        startupConfiguration.StartupErrorMessage.Should().BeNull();
        startupConfiguration
            .BootRomOptions.DmgBootRom.Length.Should()
            .Be(BootRomOptions.DmgBootRomSize);
        startupConfiguration
            .BootRomOptions.CgbBootRom.Length.Should()
            .Be(BootRomOptions.CgbBootRomSize);
        startupConfiguration
            .BootRomOptions.SgbBootRom.Length.Should()
            .Be(BootRomOptions.SgbBootRomSize);
        startupConfiguration.BootRomOptions.DmgBootRom.Span[0].Should().Be(0xD0);
        startupConfiguration.BootRomOptions.CgbBootRom.Span[0].Should().Be(0xC0);
        startupConfiguration.BootRomOptions.SgbBootRom.Span[0].Should().Be(0x50);
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

        startupConfiguration.StartupErrorMessage.Should().BeNull();
        startupConfiguration.InputConfig.Keyboard.ActiveProfile.Should().Be("alternate");
        startupConfiguration
            .InputConfig.Gamepad.ActiveProfile.Should()
            .Be(InputConfig.DefaultProfileName);
        inputMap.KeyboardBindings.Count.Should().Be(8);
        inputMap.GamepadBindings.Count.Should().Be(4);
        inputMap
            .KeyboardBindings.Should()
            .Contain(binding => binding.Button == JoypadButton.B && binding.Key == Key.K);
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

        startupConfiguration.StartupErrorMessage.Should().BeNull();
        startupConfiguration.EmulationConfig.FastForwardEnabled.Should().BeTrue();
        startupConfiguration.EmulationConfig.FastForwardSpeed.Should().Be(EmulationSpeed.Eight);
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
                Audio = new AudioConfig(27, Muted: true),
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

        appConfig.Emulation.FastForwardEnabled.Should().BeTrue();
        appConfig.Emulation.FastForwardSpeed.Should().Be(EmulationSpeed.Eight);
        appConfig.Input.Keyboard.ActiveProfile.Should().Be("alternate");
        appConfig.Input.Gamepad.ActiveProfile.Should().Be(InputConfig.DefaultProfileName);
        appConfig.BootRoms.DmgPath.Should().Be("old-dmg.bin");
        appConfig.Audio.Should().Be(new AudioConfig(27, Muted: true));

        input.GetProperty("version").GetInt32().Should().Be(2);
        input.TryGetProperty("keyboard", out _).Should().BeTrue();
        input.TryGetProperty("gamepad", out _).Should().BeTrue();
        input.TryGetProperty("activeProfile", out _).Should().BeFalse();
    }

    [Fact]
    public void SaveAudioConfig_PreservesExistingSections()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);

        AppConfigurationFile.Save(
            configPath,
            new AppConfig
            {
                Input = CreateInputWithAlternateKeyboardProfile(),
                BootRoms = new BootRomConfig("dmg.bin"),
                Emulation = new() { FastForwardEnabled = true },
            },
            NullLogger.Instance
        );

        var service = new AppConfigurationService(
            configPath,
            NullLogger<AppConfigurationService>.Instance
        );

        service.SaveAudioConfig(new AudioConfig(63, true));

        var saved = AppConfigurationFile.Load(configPath);
        saved.Audio.Should().Be(new AudioConfig(63, Muted: true));
        saved.Input.Keyboard.ActiveProfile.Should().Be("alternate");
        saved.BootRoms.DmgPath.Should().Be("dmg.bin");
        saved.Emulation.FastForwardEnabled.Should().BeTrue();
    }

    [Fact]
    public void Load_InvalidAudioFallsBackWithoutResettingOtherSections()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);

        AppConfigurationFile.Save(
            configPath,
            new AppConfig
            {
                Audio = new AudioConfig(-1, Muted: true),
                Emulation = new() { FastForwardEnabled = true },
                Input = CreateInputWithAlternateKeyboardProfile(),
            },
            NullLogger.Instance
        );

        var startupConfiguration = StartupConfigurationLoader.Load(configPath, NullLogger.Instance);

        startupConfiguration
            .StartupErrorMessage.Should()
            .Be("Audio volume must be between 0 and 100 percent.");
        startupConfiguration.AudioConfig.Should().Be(new AudioConfig());
        startupConfiguration.EmulationConfig.FastForwardEnabled.Should().BeTrue();
        startupConfiguration.InputConfig.Keyboard.ActiveProfile.Should().Be("alternate");
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

        startupConfiguration.StartupErrorMessage.Should().Contain("DMG boot ROM must be 256 bytes");
        startupConfiguration.BootRomOptions.DmgBootRom.IsEmpty.Should().BeTrue();
        startupConfiguration
            .BootRomOptions.CgbBootRom.Length.Should()
            .Be(BootRomOptions.CgbBootRomSize);
        startupConfiguration.BootRomOptions.CgbBootRom.Span[0].Should().Be(0xC0);
        startupConfiguration.BootRomOptions.SgbBootRom.IsEmpty.Should().BeTrue();
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

        startupConfiguration
            .StartupErrorMessage.Should()
            .Contain("DMG boot ROM file could not be read");
        startupConfiguration.BootRomOptions.DmgBootRom.IsEmpty.Should().BeTrue();
        startupConfiguration.BootRomOptions.CgbBootRom.IsEmpty.Should().BeTrue();
        startupConfiguration.BootRomOptions.SgbBootRom.IsEmpty.Should().BeTrue();
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

        validation
            .Should()
            .Contain(error =>
                error.Contains("Keyboard input config is malformed", StringComparison.Ordinal)
            );
        validation
            .Should()
            .Contain(error =>
                error.Contains("Gamepad input config is malformed", StringComparison.Ordinal)
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

        validation.Should().BeEmpty();
    }
}
