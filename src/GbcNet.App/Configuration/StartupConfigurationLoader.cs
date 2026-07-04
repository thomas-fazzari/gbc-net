// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using FluentResults;
using GbcNet.App.Common;
using GbcNet.App.Configuration.Kdl;
using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.App.Configuration.Sections.Input;
using GbcNet.Core;

namespace GbcNet.App.Configuration;

internal sealed record StartupConfiguration(
    InputConfig InputConfig,
    BootRomOptions BootRomOptions,
    string ConfigPath,
    string? StartupErrorMessage
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

        var loadedBootRomOptions = document.IsSuccess
            ? BootRomConfigReader.ReadBootRomOptions(
                document.Value,
                Path.GetDirectoryName(configPath) ?? Environment.CurrentDirectory
            )
            : Result.Ok(new BootRomOptions());

        var inputConfig = loadedInputConfig.IsSuccess
            ? loadedInputConfig.Value
            : LoadTemplateInputConfig();

        var bootRomOptions = loadedBootRomOptions.IsSuccess
            ? loadedBootRomOptions.Value
            : new BootRomOptions();

        var startupErrors = new List<string>();

        AddErrors(startupErrors, loadedInputConfig);
        AddErrors(startupErrors, loadedBootRomOptions);

        var validation = InputConfigValidator.Validate(inputConfig);

        if (validation.Count == 0)
        {
            return new StartupConfiguration(
                inputConfig,
                bootRomOptions,
                configPath,
                ToStartupErrorMessage(startupErrors)
            );
        }

        startupErrors.AddRange(validation);
        inputConfig = LoadTemplateInputConfig();

        ValidateFallbackConfig(inputConfig);

        return new StartupConfiguration(
            inputConfig,
            bootRomOptions,
            configPath,
            ToStartupErrorMessage(startupErrors)
        );
    }

    private static void AddErrors<T>(List<string> startupErrors, Result<T> result)
    {
        if (result.IsFailed)
        {
            startupErrors.Add(ResultErrors.Format(result.Errors));
        }
    }

    private static string? ToStartupErrorMessage(List<string> startupErrors) =>
        startupErrors.Count == 0 ? null : string.Join(Environment.NewLine, startupErrors);

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
            throw new InvalidOperationException(ResultErrors.Format(template.Errors));
        }

        var inputConfig = InputConfigReader.Read(template.Value);

        return inputConfig.IsSuccess
            ? inputConfig.Value
            : throw new InvalidOperationException(ResultErrors.Format(inputConfig.Errors));
    }
}
