using GbcNet.App;
using GbcNet.App.Configuration;
using GbcNet.App.Input;
using GbcNet.App.Input.Configuration;
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
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
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
