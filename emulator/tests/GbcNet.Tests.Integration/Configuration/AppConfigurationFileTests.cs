// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using GbcNet.App.Configuration;
using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.App.Configuration.Sections.Library;
using GbcNet.Core.Hardware;
using Microsoft.Extensions.Logging.Abstractions;

namespace GbcNet.Tests.Integration.Configuration;

public sealed class AppConfigurationFileTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("""{"input":null,"emulation":null,"library":null}""")]
    public void Load_MissingOrNullRootSectionsUsesDefaults(string json)
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        Directory.CreateDirectory(tempDirectory.Path);
        File.WriteAllText(configPath, json);

        var config = AppConfigurationFile.Load(configPath);

        config.Should().BeEquivalentTo(AppConfigurationFile.CreateDefault());
    }

    [Fact]
    public void Load_MalformedJsonPreservesParseErrorContract()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        Directory.CreateDirectory(tempDirectory.Path);
        File.WriteAllText(configPath, "{");

        var exception = FluentActions
            .Invoking(() => AppConfigurationFile.Load(configPath))
            .Should()
            .ThrowExactly<ConfigurationException>()
            .Which;

        exception.Message.Should().StartWith("Configuration file could not be parsed:");
        exception.InnerException.Should().BeOfType<JsonException>();
    }

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
        File.Exists(configPath + ".tmp").Should().BeFalse();

        using var json = JsonDocument.Parse(File.ReadAllText(configPath));
        json.RootElement.GetProperty("bootRoms")
            .GetProperty(BootRomConfig.JsonName(HardwareModel.Dmg))
            .GetString()
            .Should()
            .Be(dmgPath);
        AppConfigurationFile.Load(configPath).BootRoms.Should().Be(bootRoms);
    }

    [Fact]
    public void Save_WritesJsonThatRoundTripsLibraryViewMode()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        var config = AppConfigurationFile.CreateDefault();
        config.Library.ViewMode = LibraryViewMode.List;

        AppConfigurationFile.Save(configPath, config, NullLogger.Instance);

        using var json = JsonDocument.Parse(File.ReadAllText(configPath));
        json.RootElement.GetProperty("library")
            .GetProperty("viewMode")
            .GetString()
            .Should()
            .Be("list");
        AppConfigurationFile.Load(configPath).Library.ViewMode.Should().Be(LibraryViewMode.List);
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

        input.GetProperty("version").GetInt32().Should().Be(2);
        input.TryGetProperty("activeProfile", out _).Should().BeFalse();
        input.TryGetProperty("profiles", out _).Should().BeFalse();
        input.EnumerateObject().Count().Should().Be(3);
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

        FluentActions
            .Invoking(() =>
                AppConfigurationFile.Save(configPath, replacementConfig, NullLogger.Instance)
            )
            .Should()
            .ThrowExactly<ConfigurationException>();

        File.ReadAllBytes(configPath).Should().Equal(originalBytes);
        AppConfigurationFile.Load(configPath).BootRoms.Should().Be(originalConfig.BootRoms);
        Directory.Exists(temporaryPath).Should().BeTrue();
    }

    private static void AssertSectionHasDefaultProfile(JsonElement section, string bindingsProperty)
    {
        section.EnumerateObject().Count().Should().Be(2);
        section.GetProperty("activeProfile").GetString().Should().Be("default");
        var profile = section.GetProperty("profiles").GetProperty("default");
        profile.EnumerateObject().Should().ContainSingle();
        profile.GetProperty(bindingsProperty).ValueKind.Should().Be(JsonValueKind.Array);
    }
}
