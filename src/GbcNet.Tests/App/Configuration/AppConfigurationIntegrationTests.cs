using GbcNet.App;
using GbcNet.App.Configuration;
using GbcNet.App.Input;
using GbcNet.App.Input.Configuration;
using GbcNet.Core;
using GbcNet.Core.Joypad;
using Microsoft.Extensions.Options;

namespace GbcNet.Tests.App.Configuration;

public sealed class AppConfigurationIntegrationTests
{
    [Fact]
    public void Load_CreatesDefaultConfigFileAndBuildsInputConfiguration()
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "gbc-net-tests",
            Guid.NewGuid().ToString("N")
        );
        var configPath = Path.Combine(tempDirectory, ApplicationDirectoryNames.ConfigFile);

        try
        {
            var startupConfiguration = StartupConfigurationLoader.Load(
                new InputOptionsValidator(),
                configPath
            );

            Assert.Null(startupConfiguration.StartupMessage);
            Assert.True(File.Exists(configPath));
            Assert.Contains(
                "input version=1",
                File.ReadAllText(configPath),
                StringComparison.Ordinal
            );
            Assert.Contains("boot_roms", File.ReadAllText(configPath), StringComparison.Ordinal);
            AssertInputOptionsAreValid(startupConfiguration.InputOptions);

            var inputConfiguration = InputConfiguration.FromOptions(
                startupConfiguration.InputOptions
            );

            Assert.Equal(8, inputConfiguration.Bindings.Count);
            Assert.Contains(
                inputConfiguration.Bindings,
                binding =>
                    binding
                        is { Button: JoypadButton.A, Input.DeviceKind: InputDeviceKind.Keyboard }
                    && string.Equals(binding.Input.Code, "Z", StringComparison.Ordinal)
            );
            Assert.Contains(
                inputConfiguration.Bindings,
                binding =>
                    binding
                        is {
                            Button: JoypadButton.Start,
                            Input.DeviceKind: InputDeviceKind.Keyboard,
                        }
            );
            Assert.True(startupConfiguration.GameBoyOptions.DmgBootRom.IsEmpty);
            Assert.True(startupConfiguration.GameBoyOptions.CgbBootRom.IsEmpty);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void Load_ReadsBootRomFilesFromConfig()
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "gbc-net-tests",
            Guid.NewGuid().ToString("N")
        );
        var configPath = Path.Combine(tempDirectory, ApplicationDirectoryNames.ConfigFile);

        try
        {
            Directory.CreateDirectory(tempDirectory);
            File.WriteAllBytes(
                Path.Combine(tempDirectory, "dmg.bin"),
                CreateBootRom(GameBoyOptions.DmgBootRomSize, 0xD0)
            );
            File.WriteAllBytes(
                Path.Combine(tempDirectory, "cgb.bin"),
                CreateBootRom(GameBoyOptions.CgbBootRomSize, 0xC0)
            );
            File.WriteAllText(configPath, CreateConfig("dmg.bin", "cgb.bin"));

            var startupConfiguration = StartupConfigurationLoader.Load(
                new InputOptionsValidator(),
                configPath
            );

            Assert.Null(startupConfiguration.StartupMessage);
            Assert.Equal(
                GameBoyOptions.DmgBootRomSize,
                startupConfiguration.GameBoyOptions.DmgBootRom.Length
            );
            Assert.Equal(
                GameBoyOptions.CgbBootRomSize,
                startupConfiguration.GameBoyOptions.CgbBootRom.Length
            );
            Assert.Equal(0xD0, startupConfiguration.GameBoyOptions.DmgBootRom.Span[0]);
            Assert.Equal(0xC0, startupConfiguration.GameBoyOptions.CgbBootRom.Span[0]);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void Load_ReportsInvalidBootRomSizeAndFallsBackToEmptyBootRomOptions()
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "gbc-net-tests",
            Guid.NewGuid().ToString("N")
        );
        var configPath = Path.Combine(tempDirectory, ApplicationDirectoryNames.ConfigFile);

        try
        {
            Directory.CreateDirectory(tempDirectory);
            File.WriteAllBytes(Path.Combine(tempDirectory, "dmg.bin"), new byte[255]);
            File.WriteAllText(configPath, CreateConfig("dmg.bin", null));

            var startupConfiguration = StartupConfigurationLoader.Load(
                new InputOptionsValidator(),
                configPath
            );

            Assert.Contains(
                "DMG boot ROM must be 256 bytes",
                startupConfiguration.StartupMessage,
                StringComparison.Ordinal
            );
            Assert.True(startupConfiguration.GameBoyOptions.DmgBootRom.IsEmpty);
            Assert.True(startupConfiguration.GameBoyOptions.CgbBootRom.IsEmpty);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void Load_ReportsMissingBootRomFileAndFallsBackToEmptyBootRomOptions()
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "gbc-net-tests",
            Guid.NewGuid().ToString("N")
        );
        var configPath = Path.Combine(tempDirectory, ApplicationDirectoryNames.ConfigFile);

        try
        {
            Directory.CreateDirectory(tempDirectory);
            File.WriteAllText(configPath, CreateConfig("missing-dmg.bin", null));

            var startupConfiguration = StartupConfigurationLoader.Load(
                new InputOptionsValidator(),
                configPath
            );

            Assert.Contains(
                "DMG boot ROM file could not be read",
                startupConfiguration.StartupMessage,
                StringComparison.Ordinal
            );
            Assert.True(startupConfiguration.GameBoyOptions.DmgBootRom.IsEmpty);
            Assert.True(startupConfiguration.GameBoyOptions.CgbBootRom.IsEmpty);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private static byte[] CreateBootRom(int length, byte marker)
    {
        var bytes = new byte[length];
        bytes[0] = marker;
        return bytes;
    }

    private static string CreateConfig(string? dmgBootRomPath, string? cgbBootRomPath)
    {
        var bootRomLines = new List<string>();
        if (dmgBootRomPath is not null)
        {
            bootRomLines.Add("  dmg \"" + dmgBootRomPath + "\"");
        }

        if (cgbBootRomPath is not null)
        {
            bootRomLines.Add("  cgb \"" + cgbBootRomPath + "\"");
        }

        return $$"""
            input version=1 {
              profile "default" {
                keyboard {
                  bind "up" key="Up"
                  bind "down" key="Down"
                  bind "left" key="Left"
                  bind "right" key="Right"
                  bind "a" key="Z"
                  bind "b" key="X"
                  bind "start" key="Enter"
                  bind "select" key="Back"
                }
              }
            }

            boot_roms {
            {{string.Join(Environment.NewLine, bootRomLines)}}
            }
            """;
    }

    private static void AssertInputOptionsAreValid(InputOptions options)
    {
        var validation = new InputOptionsValidator().Validate(Options.DefaultName, options);

        Assert.False(
            validation.Failed,
            string.Join(Environment.NewLine, validation.Failures ?? [])
        );
    }
}
