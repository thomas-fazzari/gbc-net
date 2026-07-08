// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App;
using GbcNet.App.Configuration;
using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.Core;

namespace GbcNet.Tests.App.ConfigurationService;

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
    public void LoadBootRomConfig_ThrowsForInvalidBootRomSection()
    {
        var tempDirectory = TestDirectories.GetTemporaryDirectoryPath();
        var configPath = Path.Combine(tempDirectory, UserDataPaths.ConfigFileName);

        try
        {
            Directory.CreateDirectory(tempDirectory);
            File.WriteAllText(
                configPath,
                """
                boot_roms {
                  invalid "boot.bin"
                }
                """
            );
            var service = new AppConfigurationService(configPath);

            var exception = Assert.Throws<ConfigurationException>(() =>
                service.LoadBootRomConfig()
            );

            Assert.Contains("does not allow child", exception.Message, StringComparison.Ordinal);
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
