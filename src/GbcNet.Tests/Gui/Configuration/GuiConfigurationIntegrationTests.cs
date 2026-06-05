using GbcNet.Core.Joypad;
using GbcNet.Gui.Configuration;
using GbcNet.Gui.Input;
using GbcNet.Gui.Input.Configuration;
using Microsoft.Extensions.Options;

namespace GbcNet.Tests.Gui.Configuration;

public sealed class GuiConfigurationIntegrationTests
{
    [Fact]
    public void Load_CreatesDefaultConfigFileAndBuildsInputConfiguration()
    {
        string tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "gbc-net-tests",
            Guid.NewGuid().ToString("N")
        );
        string configPath = Path.Combine(tempDirectory, KdlConfigurationFile.FileName);

        try
        {
            StartupConfiguration startupConfiguration = StartupConfigurationLoader.Load(
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
        ValidateOptionsResult validation = new InputOptionsValidator().Validate(
            Options.DefaultName,
            options
        );

        Assert.False(
            validation.Failed,
            string.Join(Environment.NewLine, validation.Failures ?? [])
        );
    }
}
