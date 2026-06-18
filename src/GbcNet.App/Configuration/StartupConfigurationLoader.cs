using FluentResults;
using GbcNet.App.Input.Configuration;
using KdlSharp;
using Microsoft.Extensions.Options;

namespace GbcNet.App.Configuration;

internal sealed record StartupConfiguration(InputOptions InputOptions, string? StartupMessage);

/// <summary>
/// Resolves startup configuration.
/// </summary>
internal static class StartupConfigurationLoader
{
    public static StartupConfiguration Load(
        IValidateOptions<InputOptions> inputOptionsValidator,
        string configPath
    )
    {
        var document = KdlConfigurationFile.LoadOrCreate(configPath);
        var loadedOptions = document.IsSuccess
            ? KdlInputOptionsReader.Read(document.Value)
            : document.ToResult<InputOptions>();

        var inputOptions = loadedOptions.IsSuccess
            ? loadedOptions.Value
            : LoadTemplateInputOptions();
        var startupMessage = loadedOptions.IsFailed
            ? string.Join(Environment.NewLine, loadedOptions.Errors.Select(error => error.Message))
            : null;

        var validation = inputOptionsValidator.Validate(Options.DefaultName, inputOptions);

        if (!validation.Failed)
        {
            return new StartupConfiguration(inputOptions, startupMessage);
        }

        startupMessage = string.Join(Environment.NewLine, validation.Failures);
        inputOptions = LoadTemplateInputOptions();
        ValidateFallbackOptions(inputOptionsValidator, inputOptions);

        return new StartupConfiguration(inputOptions, startupMessage);
    }

    private static void ValidateFallbackOptions(
        IValidateOptions<InputOptions> validator,
        InputOptions inputOptions
    )
    {
        var validation = validator.Validate(Options.DefaultName, inputOptions);

        if (validation.Failed)
        {
            throw new InvalidOperationException(
                $"Default input options are invalid:{Environment.NewLine}{string.Join(Environment.NewLine, validation.Failures)}"
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

        var inputOptions = KdlInputOptionsReader.Read(template.Value);

        return inputOptions.IsSuccess
            ? inputOptions.Value
            : throw new InvalidOperationException(
                string.Join(Environment.NewLine, inputOptions.Errors.Select(error => error.Message))
            );
    }
}
