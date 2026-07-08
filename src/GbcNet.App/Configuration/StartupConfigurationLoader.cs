// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Configuration.Kdl;
using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.App.Configuration.Sections.Input;
using GbcNet.Core;
using KdlSharp;

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
        var startupErrors = new List<string>();
        var bootRomOptions = new BootRomOptions();

        KdlDocument? document = null;
        try
        {
            document = KdlConfigurationFile.LoadOrCreate(configPath);
        }
        catch (ConfigurationException exception)
        {
            startupErrors.Add(ConfigurationErrors.Format(exception));
        }

        InputConfig inputConfig;
        if (document is null)
        {
            inputConfig = LoadTemplateInputConfig();
        }
        else
        {
            try
            {
                inputConfig = InputConfigReader.Read(document);
            }
            catch (ConfigurationException exception)
            {
                startupErrors.Add(ConfigurationErrors.Format(exception));
                inputConfig = LoadTemplateInputConfig();
            }

            try
            {
                bootRomOptions = BootRomConfigReader.ReadBootRomOptions(
                    document,
                    Path.GetDirectoryName(configPath) ?? Environment.CurrentDirectory
                );
            }
            catch (ConfigurationException exception)
            {
                startupErrors.Add(ConfigurationErrors.Format(exception));
            }
        }

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
        try
        {
            return InputConfigReader.Read(KdlConfigurationFile.LoadTemplate());
        }
        catch (ConfigurationException exception)
        {
            throw new InvalidOperationException(ConfigurationErrors.Format(exception), exception);
        }
    }
}
