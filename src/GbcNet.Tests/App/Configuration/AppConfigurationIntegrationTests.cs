using Avalonia.Input;
using GbcNet.App;
using GbcNet.App.Configuration;
using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.App.Configuration.Sections.Input;
using GbcNet.App.Input;
using GbcNet.Core;
using GbcNet.Core.Joypad;

namespace GbcNet.Tests.App.Configuration;

public sealed class AppConfigurationIntegrationTests
{
    [Fact]
    public void Load_CreatesDefaultConfigFileAndBuildsInputMap()
    {
        var tempDirectory = TestDirectories.GetTemporaryDirectoryPath();
        var configPath = Path.Combine(tempDirectory, UserDataPaths.ConfigFileName);

        try
        {
            var startupConfiguration = StartupConfigurationLoader.Load(configPath);

            Assert.Null(startupConfiguration.StartupErrorMessage);
            Assert.True(File.Exists(configPath));
            var configText = File.ReadAllText(configPath);
            Assert.Contains(
                $"{InputConfigSchema.InputNodeName} {InputConfigSchema.VersionPropertyName}=1",
                configText,
                StringComparison.Ordinal
            );
            Assert.Contains(
                BootRomConfigSchema.BootRomNodeName,
                configText,
                StringComparison.Ordinal
            );
            AssertInputConfigIsValid(startupConfiguration.InputConfig);

            var inputMap = InputMap.FromConfig(startupConfiguration.InputConfig);

            Assert.Equal(8, inputMap.Bindings.Count);
            Assert.Contains(
                inputMap.Bindings,
                binding => binding is { Button: JoypadButton.A, Key: Key.Z }
            );
            Assert.Contains(
                inputMap.Bindings,
                binding => binding is { Button: JoypadButton.Start, Key: Key.Enter }
            );
            Assert.True(startupConfiguration.BootRomOptions.DmgBootRom.IsEmpty);
            Assert.True(startupConfiguration.BootRomOptions.CgbBootRom.IsEmpty);
        }
        finally
        {
            TestDirectories.DeleteDirectoryIfExists(tempDirectory);
        }
    }

    [Fact]
    public void Load_ReadsBootRomFilesFromConfig()
    {
        var tempDirectory = TestDirectories.GetTemporaryDirectoryPath();
        var configPath = Path.Combine(tempDirectory, UserDataPaths.ConfigFileName);

        try
        {
            Directory.CreateDirectory(tempDirectory);
            File.WriteAllBytes(
                Path.Combine(tempDirectory, "dmg.bin"),
                CreateBootRom(BootRomOptions.DmgBootRomSize, 0xD0)
            );
            File.WriteAllBytes(
                Path.Combine(tempDirectory, "cgb.bin"),
                CreateBootRom(BootRomOptions.CgbBootRomSize, 0xC0)
            );
            File.WriteAllText(configPath, CreateConfig("dmg.bin", "cgb.bin"));

            var startupConfiguration = StartupConfigurationLoader.Load(configPath);

            Assert.Null(startupConfiguration.StartupErrorMessage);
            Assert.Equal(
                BootRomOptions.DmgBootRomSize,
                startupConfiguration.BootRomOptions.DmgBootRom.Length
            );
            Assert.Equal(
                BootRomOptions.CgbBootRomSize,
                startupConfiguration.BootRomOptions.CgbBootRom.Length
            );
            Assert.Equal(0xD0, startupConfiguration.BootRomOptions.DmgBootRom.Span[0]);
            Assert.Equal(0xC0, startupConfiguration.BootRomOptions.CgbBootRom.Span[0]);
        }
        finally
        {
            TestDirectories.DeleteDirectoryIfExists(tempDirectory);
        }
    }

    [Fact]
    public void Load_ReportsInvalidBootRomSizeAndFallsBackToEmptyBootRoms()
    {
        var tempDirectory = TestDirectories.GetTemporaryDirectoryPath();
        var configPath = Path.Combine(tempDirectory, UserDataPaths.ConfigFileName);

        try
        {
            Directory.CreateDirectory(tempDirectory);
            File.WriteAllBytes(Path.Combine(tempDirectory, "dmg.bin"), new byte[255]);
            File.WriteAllText(configPath, CreateConfig("dmg.bin", null));

            var startupConfiguration = StartupConfigurationLoader.Load(configPath);

            Assert.Contains(
                "DMG boot ROM must be 256 bytes",
                startupConfiguration.StartupErrorMessage,
                StringComparison.Ordinal
            );
            Assert.True(startupConfiguration.BootRomOptions.DmgBootRom.IsEmpty);
            Assert.True(startupConfiguration.BootRomOptions.CgbBootRom.IsEmpty);
        }
        finally
        {
            TestDirectories.DeleteDirectoryIfExists(tempDirectory);
        }
    }

    [Fact]
    public void Load_ReportsMissingBootRomFileAndFallsBackToEmptyBootRoms()
    {
        var tempDirectory = TestDirectories.GetTemporaryDirectoryPath();
        var configPath = Path.Combine(tempDirectory, UserDataPaths.ConfigFileName);

        try
        {
            Directory.CreateDirectory(tempDirectory);
            File.WriteAllText(configPath, CreateConfig("missing-dmg.bin", null));

            var startupConfiguration = StartupConfigurationLoader.Load(configPath);

            Assert.Contains(
                "DMG boot ROM file could not be read",
                startupConfiguration.StartupErrorMessage,
                StringComparison.Ordinal
            );
            Assert.True(startupConfiguration.BootRomOptions.DmgBootRom.IsEmpty);
            Assert.True(startupConfiguration.BootRomOptions.CgbBootRom.IsEmpty);
        }
        finally
        {
            TestDirectories.DeleteDirectoryIfExists(tempDirectory);
        }
    }

    [Fact]
    public void InputValidation_RejectsEmptyActiveKeyboardProfile()
    {
        InputConfig config = new()
        {
            Profiles = new Dictionary<string, InputProfileConfig>(StringComparer.Ordinal)
            {
                [InputConfigSchema.DefaultProfileName] = new(),
            },
        };

        var validation = InputConfigValidator.Validate(config);

        Assert.NotEmpty(validation);
        Assert.Contains(
            "must contain at least one keyboard binding",
            validation.Single(),
            StringComparison.Ordinal
        );
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
            bootRomLines.Add(
                "  " + BootRomConfigSchema.DmgNodeName + " \"" + dmgBootRomPath + "\""
            );
        }

        if (cgbBootRomPath is not null)
        {
            bootRomLines.Add(
                "  " + BootRomConfigSchema.CgbNodeName + " \"" + cgbBootRomPath + "\""
            );
        }

        return $$"""
            {{InputConfigSchema.InputNodeName}} {{InputConfigSchema.VersionPropertyName}}=1 {
              {{InputConfigSchema.ProfileNodeName}} "{{InputConfigSchema.DefaultProfileName}}" {
                {{InputConfigSchema.KeyboardNodeName}} {
                  {{InputConfigSchema.BindNodeName}} "up" {{InputConfigSchema.KeyPropertyName}}="Up"
                  {{InputConfigSchema.BindNodeName}} "down" {{InputConfigSchema.KeyPropertyName}}="Down"
                  {{InputConfigSchema.BindNodeName}} "left" {{InputConfigSchema.KeyPropertyName}}="Left"
                  {{InputConfigSchema.BindNodeName}} "right" {{InputConfigSchema.KeyPropertyName}}="Right"
                  {{InputConfigSchema.BindNodeName}} "a" {{InputConfigSchema.KeyPropertyName}}="Z"
                  {{InputConfigSchema.BindNodeName}} "b" {{InputConfigSchema.KeyPropertyName}}="X"
                  {{InputConfigSchema.BindNodeName}} "start" {{InputConfigSchema.KeyPropertyName}}="Enter"
                  {{InputConfigSchema.BindNodeName}} "select" {{InputConfigSchema.KeyPropertyName}}="Back"
                }
              }
            }

            {{BootRomConfigSchema.BootRomNodeName}} {
            {{string.Join(Environment.NewLine, bootRomLines)}}
            }
            """;
    }

    private static void AssertInputConfigIsValid(InputConfig config)
    {
        var validation = InputConfigValidator.Validate(config);

        Assert.False(validation.Count != 0, string.Join(Environment.NewLine, validation));
    }
}
