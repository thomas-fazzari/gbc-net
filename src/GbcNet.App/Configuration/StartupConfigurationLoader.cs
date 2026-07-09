// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

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
        var startupErrors = new List<string>();
        var configDirectoryPath = Path.GetDirectoryName(configPath) ?? Environment.CurrentDirectory;
        var appConfig = LoadConfig(configPath, startupErrors);
        var inputConfig = appConfig.Input;
        var bootRomOptions = new BootRomOptions();

        try
        {
            bootRomOptions = AppConfigurationService.LoadBootRomOptions(
                BootRomConfig.FromDictionary(appConfig.BootRoms),
                configDirectoryPath
            );
        }
        catch (ConfigurationException exception)
        {
            startupErrors.Add(ConfigurationErrors.Format(exception));
        }

        var validation = InputConfigValidator.Validate(inputConfig);

        if (validation.Count != 0)
        {
            startupErrors.AddRange(validation);
            inputConfig = AppConfigurationFile.CreateDefaultInputConfig();
            ValidateFallbackConfig(inputConfig);
        }

        return new StartupConfiguration(
            inputConfig,
            bootRomOptions,
            configPath,
            startupErrors.Count == 0 ? null : string.Join(Environment.NewLine, startupErrors)
        );
    }

    private static AppConfig LoadConfig(string configPath, List<string> startupErrors)
    {
        try
        {
            return AppConfigurationFile.LoadOrCreate(configPath);
        }
        catch (ConfigurationException exception)
        {
            startupErrors.Add(ConfigurationErrors.Format(exception));
            return AppConfigurationFile.CreateDefault();
        }
    }

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
}
