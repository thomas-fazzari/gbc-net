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
    public void SaveBootRomConfigAndLoadBootRomConfig_RoundTripPaths()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);

        Directory.CreateDirectory(tempDirectory.Path);
        var service = new AppConfigurationService(
            configPath,
            NullLogger<AppConfigurationService>.Instance
        );

        service.SaveBootRomConfig(new BootRomConfig("dmg.bin", "cgb.bin", "sgb.bin"));

        var config = service.LoadBootRomConfig();

        Assert.Equal(new BootRomConfig("dmg.bin", "cgb.bin", "sgb.bin"), config);
    }

    [Fact]
    public void SaveBootRomConfig_PreservesInputProfiles()
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

        service.SaveBootRomConfig(new BootRomConfig("new-dmg.bin", "new-cgb.bin", "new-sgb.bin"));

        var appConfig = AppConfigurationFile.Load(configPath);
        Assert.Equal(
            new BootRomConfig("new-dmg.bin", "new-cgb.bin", "new-sgb.bin"),
            appConfig.BootRoms
        );
        Assert.Equal("alternate", appConfig.Input.ActiveProfile);
        Assert.Equal(2, appConfig.Input.Profiles.Count);
        var binding = Assert.Single(appConfig.Input.Profiles["alternate"].Keyboard);
        Assert.Equal(new KeyboardInputBindingConfig("b", "X"), binding);
    }

    [Fact]
    public void SaveBootRomConfig_WritesJsonThatRoundTripsEscapedPaths()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, UserDataPaths.ConfigFileName);
        const string dmgPath = "dir\\boot\"rom.bin";

        Directory.CreateDirectory(tempDirectory.Path);
        var service = new AppConfigurationService(
            configPath,
            NullLogger<AppConfigurationService>.Instance
        );

        service.SaveBootRomConfig(new BootRomConfig(dmgPath, CgbPath: "cgb.bin"));

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
        service.SaveBootRomConfig(new BootRomConfig("dmg.bin", "cgb.bin", "sgb.bin"));

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

    private static byte[] CreateBootRom(int length, byte marker)
    {
        var bytes = new byte[length];
        bytes[0] = marker;
        return bytes;
    }
}
