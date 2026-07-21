// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Configuration.Sections.Audio;
using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.App.Configuration.Sections.Emulation;
using GbcNet.App.Configuration.Sections.Input;
using GbcNet.Core;
using GbcNet.Core.Hardware;
using Microsoft.Extensions.Logging;

namespace GbcNet.App.Configuration;

internal sealed class AppConfigurationService(
    string configPath,
    ILogger<AppConfigurationService> logger
)
{
    public BootRomConfig LoadBootRomConfig() =>
        AppConfigurationFile.LoadOrCreate(configPath, logger).BootRoms;

    public SettingsConfig LoadSettings()
    {
        var appConfig = AppConfigurationFile.LoadOrCreate(configPath, logger);
        return new SettingsConfig(appConfig.BootRoms, appConfig.Input) { Audio = appConfig.Audio };
    }

    public BootRomOptions LoadBootRomOptions(ICollection<string>? errors = null) =>
        LoadBootRomOptions(
            LoadBootRomConfig(),
            configDirectoryPath: Path.GetDirectoryName(configPath) ?? Environment.CurrentDirectory,
            errors
        );

    public IReadOnlyList<string> SaveSettings(SettingsConfig settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var validation = InputConfigValidator.Validate(settings.Input);
        if (validation.Count != 0)
        {
            throw new ConfigurationException(string.Join(Environment.NewLine, validation));
        }

        ValidateAudioConfig(settings.Audio);

        var bootRomErrors = new List<string>();

        AppConfig appConfig;
        try
        {
            appConfig = AppConfigurationFile.LoadOrCreate(configPath, logger);
        }
        catch (ConfigurationException exception)
        {
            AppConfigurationServiceLog.ConfigurationReadFailed(logger, exception);
            appConfig = AppConfigurationFile.CreateDefault();
        }

        var configDirectoryPath = Path.GetDirectoryName(configPath) ?? Environment.CurrentDirectory;
        appConfig.BootRoms = new BootRomConfig(
            DmgPath: ResolveBootRomPath(
                proposedPath: settings.BootRoms.DmgPath,
                currentPath: appConfig.BootRoms.DmgPath,
                HardwareModel.Dmg,
                configDirectoryPath: configDirectoryPath,
                bootRomErrors
            ),
            CgbPath: ResolveBootRomPath(
                proposedPath: settings.BootRoms.CgbPath,
                currentPath: appConfig.BootRoms.CgbPath,
                HardwareModel.Cgb,
                configDirectoryPath: configDirectoryPath,
                bootRomErrors
            ),
            SgbPath: ResolveBootRomPath(
                proposedPath: settings.BootRoms.SgbPath,
                currentPath: appConfig.BootRoms.SgbPath,
                HardwareModel.Sgb,
                configDirectoryPath: configDirectoryPath,
                bootRomErrors
            )
        );
        appConfig.Input = settings.Input;
        appConfig.Audio = settings.Audio;

        AppConfigurationFile.Save(configPath, appConfig, logger);
        return bootRomErrors;
    }

    public void SaveEmulationConfig(EmulationConfig config)
    {
        var appConfig = AppConfigurationFile.LoadOrCreate(configPath, logger);
        appConfig.Emulation = config;
        AppConfigurationFile.Save(configPath, appConfig, logger);
    }

    public void SaveAudioConfig(AudioConfig config)
    {
        ValidateAudioConfig(config);

        var appConfig = AppConfigurationFile.LoadOrCreate(configPath, logger);
        appConfig.Audio = config;
        AppConfigurationFile.Save(configPath, appConfig, logger);
    }

    private static void ValidateAudioConfig(AudioConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (!AudioConfig.IsValidVolume(config.VolumePercent))
        {
            throw new ConfigurationException("Audio volume must be between 0 and 100 percent.");
        }
    }

    internal static BootRomOptions LoadBootRomOptions(
        BootRomConfig config,
        string configDirectoryPath,
        ICollection<string>? errors = null
    ) =>
        new()
        {
            DmgBootRom = ReadBootRom(
                path: config.GetPath(HardwareModel.Dmg),
                HardwareModel.Dmg,
                configDirectoryPath: configDirectoryPath,
                errors
            ),
            CgbBootRom = ReadBootRom(
                path: config.GetPath(HardwareModel.Cgb),
                HardwareModel.Cgb,
                configDirectoryPath: configDirectoryPath,
                errors
            ),
            SgbBootRom = ReadBootRom(
                path: config.GetPath(HardwareModel.Sgb),
                HardwareModel.Sgb,
                configDirectoryPath: configDirectoryPath,
                errors
            ),
        };

    private static ReadOnlyMemory<byte> ReadBootRom(
        string? path,
        HardwareModel model,
        string configDirectoryPath,
        ICollection<string>? errors
    )
    {
        try
        {
            return ReadBootRomFile(path: path, model, configDirectoryPath: configDirectoryPath);
        }
        catch (ConfigurationException exception) when (errors is not null)
        {
            errors.Add(exception.Message);
            return default;
        }
    }

    private static ReadOnlyMemory<byte> ReadBootRomFile(
        string? path,
        HardwareModel model,
        string configDirectoryPath
    )
    {
        var label = BootRomConfig.DisplayName(model);
        var expectedLength = BootRomOptions.SizeDescription(model);
        if (path is null)
        {
            return default;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ConfigurationException($"{label} boot ROM path must not be empty.");
        }

        byte[] bytes;
        try
        {
            var resolvedPath = Path.IsPathFullyQualified(path)
                ? path
                : Path.GetFullPath(Path.Combine(path1: configDirectoryPath, path2: path));
            bytes = File.ReadAllBytes(resolvedPath);
        }
        catch (Exception exception) when (IsExpectedPathException(exception))
        {
            throw new ConfigurationException(
                $"{label} boot ROM file could not be read: {exception.Message}"
            );
        }

        return BootRomOptions.IsValidSize(model, bytes.Length)
            ? bytes
            : throw new ConfigurationException(
                $"{label} boot ROM must be {expectedLength} bytes, but was {bytes.Length} bytes."
            );
    }

    private static string? ResolveBootRomPath(
        string? proposedPath,
        string? currentPath,
        HardwareModel model,
        string configDirectoryPath,
        List<string> errors
    )
    {
        try
        {
            _ = ReadBootRomFile(
                path: proposedPath,
                model,
                configDirectoryPath: configDirectoryPath
            );
            return proposedPath;
        }
        catch (ConfigurationException exception)
        {
            errors.Add(exception.Message);
        }

        if (
            string.Equals(
                proposedPath,
                currentPath,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return null;
        }

        try
        {
            _ = ReadBootRomFile(path: currentPath, model, configDirectoryPath: configDirectoryPath);
            return currentPath;
        }
        catch (ConfigurationException)
        {
            return null;
        }
    }

    private static bool IsExpectedPathException(Exception exception) =>
        exception
            is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException;
}

internal static partial class AppConfigurationServiceLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Existing configuration could not be read before saving; defaults will be used."
    )]
    internal static partial void ConfigurationReadFailed(ILogger logger, Exception exception);
}
