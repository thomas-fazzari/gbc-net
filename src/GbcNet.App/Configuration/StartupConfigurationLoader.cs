using FluentResults;
using GbcNet.App.Configuration.Kdl;
using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.App.Configuration.Sections.Input;
using GbcNet.Core;

namespace GbcNet.App.Configuration;

internal sealed record StartupConfiguration(
    InputConfig InputConfig,
    GameBoyOptions GameBoyOptions,
    string ConfigPath,
    string? StartupMessage
);

/// <summary>
/// Resolves startup configuration.
/// </summary>
internal static class StartupConfigurationLoader
{
    public static StartupConfiguration Load(string configPath)
    {
        var document = KdlConfigurationFile.LoadOrCreate(configPath);

        var loadedInputConfig = document.IsSuccess
            ? InputConfigReader.Read(document.Value)
            : document.ToResult<InputConfig>();

        var loadedGameBoyOptions = document.IsSuccess
            ? BootRomConfigReader.ReadGameBoyOptions(
                document.Value,
                Path.GetDirectoryName(configPath) ?? Environment.CurrentDirectory
            )
            : Result.Ok(new GameBoyOptions());

        var inputConfig = loadedInputConfig.IsSuccess
            ? loadedInputConfig.Value
            : LoadTemplateInputConfig();

        var gameBoyOptions = loadedGameBoyOptions.IsSuccess
            ? loadedGameBoyOptions.Value
            : new GameBoyOptions();

        var startupMessages = new List<string>();

        AddErrors(startupMessages, loadedInputConfig);
        AddErrors(startupMessages, loadedGameBoyOptions);

        var validation = InputConfigValidator.Validate(inputConfig);

        if (validation.Count == 0)
        {
            return new StartupConfiguration(
                inputConfig,
                gameBoyOptions,
                configPath,
                ToStartupMessage(startupMessages)
            );
        }

        startupMessages.AddRange(validation);
        inputConfig = LoadTemplateInputConfig();

        ValidateFallbackConfig(inputConfig);

        return new StartupConfiguration(
            inputConfig,
            gameBoyOptions,
            configPath,
            ToStartupMessage(startupMessages)
        );
    }

    private static void AddErrors<T>(List<string> messages, Result<T> result)
    {
        if (result.IsFailed)
        {
            messages.AddRange(result.Errors.Select(error => error.Message));
        }
    }

    private static string? ToStartupMessage(List<string> messages) =>
        messages.Count == 0 ? null : string.Join(Environment.NewLine, messages);

    private static void ValidateFallbackConfig(InputConfig inputConfig)
    {
        var validation = InputConfigValidator.Validate(inputConfig);

        if (validation.Count != 0)
        {
            throw new InvalidOperationException(
                $"Default input config is invalid:{Environment.NewLine}{string.Join(Environment.NewLine, validation)}"
            );
        }
    }

    private static InputConfig LoadTemplateInputConfig()
    {
        var template = KdlConfigurationFile.LoadTemplate();

        if (template.IsFailed)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, template.Errors.Select(error => error.Message))
            );
        }

        var inputConfig = InputConfigReader.Read(template.Value);

        return inputConfig.IsSuccess
            ? inputConfig.Value
            : throw new InvalidOperationException(
                string.Join(Environment.NewLine, inputConfig.Errors.Select(error => error.Message))
            );
    }
}
