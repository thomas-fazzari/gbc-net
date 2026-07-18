// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Configuration.Sections.Emulation;
using GbcNet.App.Configuration.Sections.Input;
using GbcNet.Core;
using Microsoft.Extensions.Logging;

namespace GbcNet.App.Configuration;

internal sealed record StartupConfiguration(
    InputConfig InputConfig,
    EmulationConfig EmulationConfig,
    BootRomOptions BootRomOptions,
    string ConfigPath,
    string? StartupErrorMessage
);

/// <summary>
/// Resolves startup configuration.
/// </summary>
internal static class StartupConfigurationLoader
{
    public static StartupConfiguration Load(string configPath, ILogger logger)
    {
        var startupErrors = new List<string>();
        var configDirectoryPath = Path.GetDirectoryName(configPath) ?? Environment.CurrentDirectory;
        var appConfig = LoadConfig(configPath, logger, startupErrors);
        var inputConfig = appConfig.Input;

        var bootRomOptions = AppConfigurationService.LoadBootRomOptions(
            appConfig.BootRoms,
            configDirectoryPath,
            startupErrors
        );

        var validation = InputConfigValidator.Validate(inputConfig);

        if (validation.Count != 0)
        {
            startupErrors.AddRange(validation);
            inputConfig = AppConfigurationFile.CreateDefaultInputConfig();
            ValidateFallbackConfig(inputConfig);
        }

        if (startupErrors.Count != 0)
        {
            StartupConfigurationLoaderLog.FallbacksApplied(logger, startupErrors.Count);
        }

        return new StartupConfiguration(
            inputConfig,
            appConfig.Emulation,
            bootRomOptions,
            configPath,
            startupErrors.Count == 0 ? null : string.Join(Environment.NewLine, startupErrors)
        );
    }

    private static AppConfig LoadConfig(
        string configPath,
        ILogger logger,
        List<string> startupErrors
    )
    {
        try
        {
            return AppConfigurationFile.LoadOrCreate(configPath, logger);
        }
        catch (ConfigurationException exception)
        {
            startupErrors.Add(exception.Message);
            return AppConfigurationFile.CreateDefault();
        }
    }

    private static void ValidateFallbackConfig(InputConfig inputConfig)
    {
        var validation = InputConfigValidator.Validate(inputConfig);

        if (validation.Count != 0)
        {
            throw new InvalidOperationException(
                $"Default input config is invalid:{Environment.NewLine}{string.Join(separator: Environment.NewLine, values: validation)}"
            );
        }
    }
}

internal static partial class StartupConfigurationLoaderLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Startup configuration required {ErrorCount} fallback(s)."
    )]
    internal static partial void FallbacksApplied(ILogger logger, int errorCount);
}
