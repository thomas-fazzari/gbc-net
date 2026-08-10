// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using ErrorOr;
using GbcNet.App;
using GbcNet.App.Configuration;
using GbcNet.App.Configuration.Sections.Audio;
using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.App.Configuration.Sections.Input;
using GbcNet.App.Configuration.Sections.Library;
using GbcNet.Core;
using GbcNet.Core.Hardware;
using Microsoft.Extensions.Logging.Abstractions;

namespace GbcNet.Tests.Integration.Configuration;

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

        service.LoadBootRomConfig().Should().Be(new BootRomConfig("dmg.bin", "cgb.bin", "sgb.bin"));
    }

    [Fact]
    public void LoadSettings_LoadsIndependentInputSections()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        var input = CreateStrictInput("SpeedRun");
        input.Keyboard.ActiveProfile = "SpeedRun";
        input.Gamepad.ActiveProfile = InputConfig.DefaultProfileName;
        var audio = new AudioConfig(47, Muted: true);
        AppConfigurationFile.Save(
            configPath,
            new AppConfig
            {
                BootRoms = new BootRomConfig("dmg.bin", "cgb.bin"),
                Input = input,
                Audio = audio,
            },
            NullLogger.Instance
        );
        var service = CreateService(configPath);

        var settings = service.LoadSettings();

        settings.BootRoms.Should().Be(new BootRomConfig("dmg.bin", "cgb.bin"));
        settings.Audio.Should().Be(audio);
        settings.Input.Keyboard.ActiveProfile.Should().Be("SpeedRun");
        settings.Input.Gamepad.ActiveProfile.Should().Be(InputConfig.DefaultProfileName);
        settings.Input.Keyboard.Profiles.ContainsKey("SpeedRun").Should().BeTrue();
        settings.Input.Gamepad.Profiles.ContainsKey("SpeedRun").Should().BeTrue();
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
                Audio = new AudioConfig(25, Muted: true),
            },
            NullLogger.Instance
        );
        var service = CreateService(configPath);

        service.SaveSettings(
            new SettingsConfig(
                new BootRomConfig("new-dmg.bin", "new-cgb.bin", "new-sgb.bin"),
                CreateStrictInput("SpeedRun")
            )
            {
                Audio = new AudioConfig(75, Muted: true),
            }
        );

        var appConfig = AppConfigurationFile.Load(configPath);

        appConfig
            .BootRoms.Should()
            .Be(new BootRomConfig("new-dmg.bin", "new-cgb.bin", "new-sgb.bin"));
        appConfig.Emulation.FastForwardEnabled.Should().BeTrue();
        appConfig.Audio.Should().Be(new AudioConfig(75, Muted: true));
        service.LoadSettings().Audio.Should().Be(new AudioConfig(75, Muted: true));
        appConfig.Input.Gamepad.ActiveProfile.Should().Be("SpeedRun");
        appConfig.Input.Keyboard.Profiles.ContainsKey("SpeedRun").Should().BeTrue();
        appConfig.Input.Gamepad.Profiles.ContainsKey("SpeedRun").Should().BeTrue();
    }

    [Fact]
    public void SaveSettings_InvalidInputReturnsValidationErrorAndLeavesFileUntouched()
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

        var result = service.SaveSettings(
            new SettingsConfig(new BootRomConfig("new-dmg.bin"), invalidInput)
        );

        result.IsError.Should().BeTrue();
        var error = result.Errors.Should().ContainSingle().Which;
        error.Type.Should().Be(ErrorType.Validation);
        error.Code.Should().Be(AppConfigurationService.InvalidInputErrorCode);
        File.ReadAllBytes(configPath).Should().Equal(originalBytes);
    }

    [Fact]
    public void SaveSettingsAndSaveAudioConfig_InvalidAudioReturnValidationErrorsAndLeaveFileUntouched()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();

        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        var originalConfig = AppConfigurationFile.CreateDefault();
        AppConfigurationFile.Save(configPath, originalConfig, NullLogger.Instance);
        var originalBytes = File.ReadAllBytes(configPath);
        var service = CreateService(configPath);
        var invalidAudio = new AudioConfig(101, Muted: false);

        var settingsResult = service.SaveSettings(
            new SettingsConfig(
                new BootRomConfig("new-dmg.bin"),
                AppConfigurationFile.CreateDefaultInputConfig()
            )
            {
                Audio = invalidAudio,
            }
        );
        var audioResult = service.SaveAudioConfig(invalidAudio);

        settingsResult.IsError.Should().BeTrue();
        var settingsError = settingsResult.Errors.Should().ContainSingle().Which;
        settingsError.Type.Should().Be(ErrorType.Validation);
        settingsError.Code.Should().Be(AppConfigurationService.InvalidAudioVolumeErrorCode);
        audioResult.IsError.Should().BeTrue();
        var audioError = audioResult.Errors.Should().ContainSingle().Which;
        audioError.Type.Should().Be(ErrorType.Validation);
        audioError.Code.Should().Be(AppConfigurationService.InvalidAudioVolumeErrorCode);
        File.ReadAllBytes(configPath).Should().Equal(originalBytes);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{\"input\":{\"version\":1,\"activeProfile\":\"default\",\"profiles\":{}}}")]
    public void SaveSettings_WhenExistingConfigIsMalformedOrOld_ReplacesItWithV2(
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

        saved.BootRoms.Should().Be(new BootRomConfig("replacement-dmg.bin"));
        InputConfigValidator.Validate(saved.Input).Should().BeEmpty();
        input.GetProperty("version").GetInt32().Should().Be(2);
        input.TryGetProperty("keyboard", out _).Should().BeTrue();
        input.TryGetProperty("gamepad", out _).Should().BeTrue();
        input.TryGetProperty("activeProfile", out _).Should().BeFalse();
        File.Exists(configPath + ".tmp").Should().BeFalse();
    }

    [Fact]
    public void SaveSettings_WhenExistingConfigCannotBeRead_PreservesItAndOriginalFailure()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        var originalConfig = AppConfigurationFile.CreateDefault();
        originalConfig.BootRoms = new BootRomConfig("original-dmg.bin");
        AppConfigurationFile.Save(configPath, originalConfig, NullLogger.Instance);
        var originalBytes = File.ReadAllBytes(configPath);
        var service = CreateService(configPath);

        ConfigurationException exception;
        using (File.Open(configPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            exception = FluentActions
                .Invoking(() =>
                    service.SaveSettings(
                        new SettingsConfig(
                            new BootRomConfig("replacement-dmg.bin"),
                            AppConfigurationFile.CreateDefaultInputConfig()
                        )
                    )
                )
                .Should()
                .ThrowExactly<ConfigurationException>()
                .Which;
        }

        exception.InnerException.Should().BeOfType<IOException>();
        File.ReadAllBytes(configPath).Should().Equal(originalBytes);
        File.Exists(configPath + ".tmp").Should().BeFalse();
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

        var result = service.SaveSettings(
            new SettingsConfig(
                new BootRomConfig("missing-dmg.bin", "new-cgb.bin"),
                CreateStrictInput("SpeedRun")
            )
        );

        var saved = AppConfigurationFile.Load(configPath);
        result.IsError.Should().BeFalse();
        result.Value.Should().ContainSingle();
        saved.BootRoms.Should().Be(new BootRomConfig("current-dmg.bin", "new-cgb.bin"));
        saved.Input.Keyboard.ActiveProfile.Should().Be("SpeedRun");
        saved.Input.Gamepad.ActiveProfile.Should().Be("SpeedRun");
    }

    [Fact]
    public void SaveSettings_BootRomPathIdentityFollowsPlatformFileSystem()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        Directory.CreateDirectory(tempDirectory.Path);
        File.WriteAllBytes(
            Path.Combine(tempDirectory.Path, "CURRENT-DMG.bin"),
            BootRomTestFactory.CreateDmg(marker: 0xD0)
        );
        AppConfigurationFile.Save(
            configPath,
            new AppConfig
            {
                BootRoms = new BootRomConfig("CURRENT-DMG.bin"),
                Input = AppConfigurationFile.CreateDefaultInputConfig(),
            },
            NullLogger.Instance
        );
        var service = CreateService(configPath);

        var result = service.SaveSettings(
            new SettingsConfig(
                new BootRomConfig("current-dmg.bin"),
                AppConfigurationFile.CreateDefaultInputConfig()
            )
        );

        var savedPath = AppConfigurationFile.Load(configPath).BootRoms.DmgPath;
        var comparison = FileUtils.GetFileSystemPathComparison();
        if (comparison is StringComparison.OrdinalIgnoreCase)
        {
            result.Value.Should().BeEmpty();
            savedPath.Should().Be("current-dmg.bin");
        }
        else
        {
            result.Value.Should().ContainSingle();
            savedPath.Should().Be("CURRENT-DMG.bin");
        }
    }

    [Fact]
    public void SaveLibraryConfig_ThenLoading_PreservesListViewMode()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        Directory.CreateDirectory(tempDirectory.Path);
        var service = CreateService(configPath);

        service.SaveLibraryConfig(new LibraryConfig { ViewMode = LibraryViewMode.List });

        var saved = AppConfigurationFile.Load(configPath);

        saved.Library.ViewMode.Should().Be(LibraryViewMode.List);
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

        options.DmgBootRom.Length.Should().Be(BootRomOptions.DmgBootRomSize);
        options.CgbBootRom.Length.Should().Be(BootRomOptions.CgbBootRomSize);
        options.SgbBootRom.Length.Should().Be(BootRomOptions.SgbBootRomSize);
        options.DmgBootRom.Span[0].Should().Be(0xD0);
        options.CgbBootRom.Span[0].Should().Be(0xC0);
        options.SgbBootRom.Span[0].Should().Be(0x50);
    }

    [Fact]
    public void BootRomConfig_MapsKnownModelsAndRejectsUnsupportedModel()
    {
        var config = new BootRomConfig("dmg.bin", "cgb.bin", "sgb.bin");

        config.GetPath(HardwareModel.Dmg).Should().Be("dmg.bin");
        config.GetPath(HardwareModel.Cgb).Should().Be("cgb.bin");
        config.GetPath(HardwareModel.Sgb).Should().Be("sgb.bin");
        BootRomConfig.Size(HardwareModel.Dmg).Should().Be(BootRomOptions.DmgBootRomSize);
        BootRomConfig.Size(HardwareModel.Cgb).Should().Be(BootRomOptions.CgbBootRomSize);
        BootRomConfig.Size(HardwareModel.Sgb).Should().Be(BootRomOptions.SgbBootRomSize);
        FluentActions
            .Invoking(() => config.GetPath((HardwareModel)int.MaxValue))
            .Should()
            .ThrowExactly<ArgumentOutOfRangeException>();
        FluentActions
            .Invoking(() => BootRomConfig.Size((HardwareModel)int.MaxValue))
            .Should()
            .ThrowExactly<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void LoadBootRomConfig_ThrowsForUnknownBootRomProperty()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        Directory.CreateDirectory(tempDirectory.Path);
        File.WriteAllText(configPath, """{ "bootRoms": { "invalid": "boot.bin" } }""");
        var service = CreateService(configPath);

        var exception = FluentActions
            .Invoking(service.LoadBootRomConfig)
            .Should()
            .ThrowExactly<ConfigurationException>()
            .Which;

        exception.Message.Should().Contain("could not be parsed");
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
