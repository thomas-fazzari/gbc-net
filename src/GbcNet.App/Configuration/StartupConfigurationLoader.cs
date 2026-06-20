using FluentResults;
using GbcNet.App.Configuration.Kdl;
using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.App.Configuration.Sections.Input;
using GbcNet.Core;

namespace GbcNet.App.Configuration;

internal sealed record StartupConfiguration(
    InputOptions InputOptions,
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

        var loadedInputOptions = document.IsSuccess
            ? InputOptionsReader.Read(document.Value)
            : document.ToResult<InputOptions>();

        var loadedGameBoyOptions = document.IsSuccess
            ? BootRomOptionsReader.Read(
                document.Value,
                Path.GetDirectoryName(configPath) ?? Environment.CurrentDirectory
            )
            : Result.Ok(new GameBoyOptions());

        var inputOptions = loadedInputOptions.IsSuccess
            ? loadedInputOptions.Value
            : LoadTemplateInputOptions();

        var gameBoyOptions = loadedGameBoyOptions.IsSuccess
            ? loadedGameBoyOptions.Value
            : new GameBoyOptions();

        var startupMessages = new List<string>();

        AddErrors(startupMessages, loadedInputOptions);
        AddErrors(startupMessages, loadedGameBoyOptions);

        var validation = InputOptionsValidator.Validate(inputOptions);

        if (validation.Count == 0)
        {
            return new StartupConfiguration(
                inputOptions,
                gameBoyOptions,
                configPath,
                ToStartupMessage(startupMessages)
            );
        }

        startupMessages.AddRange(validation);
        inputOptions = LoadTemplateInputOptions();

        ValidateFallbackOptions(inputOptions);

        return new StartupConfiguration(
            inputOptions,
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

    private static void ValidateFallbackOptions(InputOptions inputOptions)
    {
        var validation = InputOptionsValidator.Validate(inputOptions);

        if (validation.Count != 0)
        {
            throw new InvalidOperationException(
                $"Default input options are invalid:{Environment.NewLine}{string.Join(Environment.NewLine, validation)}"
            );
        }
    }

    private static InputOptions LoadTemplateInputOptions()
    {
        var template = KdlConfigurationFile.LoadTemplate();

        if (template.IsFailed)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, template.Errors.Select(error => error.Message))
            );
        }

        var inputOptions = InputOptionsReader.Read(template.Value);

        return inputOptions.IsSuccess
            ? inputOptions.Value
            : throw new InvalidOperationException(
                string.Join(Environment.NewLine, inputOptions.Errors.Select(error => error.Message))
            );
    }
}
