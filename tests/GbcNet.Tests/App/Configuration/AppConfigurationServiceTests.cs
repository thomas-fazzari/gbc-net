// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using GbcNet.App.Configuration;
using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.App.Configuration.Sections.Input;
using GbcNet.Core;
using GbcNet.Core.Hardware;

namespace GbcNet.Tests.App.Configuration;

public sealed class AppConfigurationServiceTests
{
    [Fact]
    public void SaveBootRomConfigAndLoadBootRomConfig_RoundTripPaths()
    {
        var tempDirectory = TestDirectories.GetTemporaryDirectoryPath();
        var configPath = Path.Combine(tempDirectory, UserDataPaths.ConfigFileName);

        try
        {
            Directory.CreateDirectory(tempDirectory);
            var service = new AppConfigurationService(configPath);

            service.SaveBootRomConfig(new BootRomConfig("dmg.bin", "cgb.bin", "sgb.bin"));

            var config = service.LoadBootRomConfig();

            Assert.Equal(new BootRomConfig("dmg.bin", "cgb.bin", "sgb.bin"), config);
        }
        finally
        {
            TestDirectories.DeleteDirectoryIfExists(tempDirectory);
        }
    }

    [Fact]
    public void SaveBootRomConfig_PreservesInputProfiles()
    {
        var tempDirectory = TestDirectories.GetTemporaryDirectoryPath();
        var configPath = Path.Combine(tempDirectory, UserDataPaths.ConfigFileName);

        try
        {
            Directory.CreateDirectory(tempDirectory);
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
            var service = new AppConfigurationService(configPath);

            service.SaveBootRomConfig(
                new BootRomConfig("new-dmg.bin", "new-cgb.bin", "new-sgb.bin")
            );

            var appConfig = AppConfigurationFile.Load(configPath);
            Assert.Equal(
                new BootRomConfig("new-dmg.bin", "new-cgb.bin", "new-sgb.bin"),
                BootRomConfig.FromDictionary(appConfig.BootRoms)
            );
            Assert.Equal("alternate", appConfig.Input.ActiveProfile);
            Assert.Equal(2, appConfig.Input.Profiles.Count);
            var binding = Assert.Single(appConfig.Input.Profiles["alternate"].Keyboard);
            Assert.Equal(new KeyboardInputBindingConfig("b", "X"), binding);
        }
        finally
        {
            TestDirectories.DeleteDirectoryIfExists(tempDirectory);
        }
    }

    [Fact]
    public void SaveBootRomConfig_WritesJsonThatRoundTripsEscapedPaths()
    {
        var tempDirectory = TestDirectories.GetTemporaryDirectoryPath();
        var configPath = Path.Combine(tempDirectory, UserDataPaths.ConfigFileName);
        const string dmgPath = "dir\\boot\"rom.bin";

        try
        {
            Directory.CreateDirectory(tempDirectory);
            var service = new AppConfigurationService(configPath);

            service.SaveBootRomConfig(new BootRomConfig(dmgPath, CgbPath: "cgb.bin"));

            using var json = JsonDocument.Parse(File.ReadAllText(configPath));
            Assert.Equal(
                dmgPath,
                json.RootElement.GetProperty("bootRoms")
                    .GetProperty(BootRomConfig.JsonName(HardwareModel.Dmg))
                    .GetString()
            );
            Assert.Equal(
                new BootRomConfig(dmgPath, CgbPath: "cgb.bin"),
                service.LoadBootRomConfig()
            );
        }
        finally
        {
            TestDirectories.DeleteDirectoryIfExists(tempDirectory);
        }
    }

    [Fact]
    public void LoadBootRomOptions_ResolvesRelativePathsFromConfigDirectory()
    {
        var tempDirectory = TestDirectories.GetTemporaryDirectoryPath();
        var configPath = Path.Combine(tempDirectory, UserDataPaths.ConfigFileName);

        try
        {
            Directory.CreateDirectory(tempDirectory);
            File.WriteAllBytes(
                Path.Combine(tempDirectory, "dmg.bin"),
                CreateBootRom(BootRomOptions.DmgBootRomSize, marker: 0xD0)
            );
            File.WriteAllBytes(
                Path.Combine(tempDirectory, "cgb.bin"),
                CreateBootRom(BootRomOptions.CgbBootRomSize, marker: 0xC0)
            );
            File.WriteAllBytes(
                Path.Combine(tempDirectory, "sgb.bin"),
                CreateBootRom(BootRomOptions.SgbBootRomSize, marker: 0x50)
            );
            var service = new AppConfigurationService(configPath);
            service.SaveBootRomConfig(new BootRomConfig("dmg.bin", "cgb.bin", "sgb.bin"));

            var options = service.LoadBootRomOptions();

            Assert.Equal(BootRomOptions.DmgBootRomSize, options.DmgBootRom.Length);
            Assert.Equal(BootRomOptions.CgbBootRomSize, options.CgbBootRom.Length);
            Assert.Equal(BootRomOptions.SgbBootRomSize, options.SgbBootRom.Length);
            Assert.Equal(0xD0, options.DmgBootRom.Span[0]);
            Assert.Equal(0xC0, options.CgbBootRom.Span[0]);
            Assert.Equal(0x50, options.SgbBootRom.Span[0]);
        }
        finally
        {
            TestDirectories.DeleteDirectoryIfExists(tempDirectory);
        }
    }

    [Fact]
    public void LoadBootRomConfig_ThrowsForUnknownBootRomProperty()
    {
        var tempDirectory = TestDirectories.GetTemporaryDirectoryPath();
        var configPath = Path.Combine(tempDirectory, UserDataPaths.ConfigFileName);

        try
        {
            Directory.CreateDirectory(tempDirectory);
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
            var service = new AppConfigurationService(configPath);

            var exception = Assert.Throws<ConfigurationException>(() =>
                service.LoadBootRomConfig()
            );

            Assert.Contains("could not be parsed", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            TestDirectories.DeleteDirectoryIfExists(tempDirectory);
        }
    }

    private static byte[] CreateBootRom(int length, byte marker)
    {
        var bytes = new byte[length];
        bytes[0] = marker;
        return bytes;
    }
}
