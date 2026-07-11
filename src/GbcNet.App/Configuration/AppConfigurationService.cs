// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.App.Configuration.Sections.Emulation;
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

    public BootRomOptions LoadBootRomOptions() =>
        LoadBootRomOptions(
            LoadBootRomConfig(),
            Path.GetDirectoryName(configPath) ?? Environment.CurrentDirectory
        );

    public void SaveBootRomConfig(BootRomConfig config)
    {
        var appConfig = AppConfigurationFile.LoadOrCreate(configPath, logger);
        appConfig.BootRoms = config;
        AppConfigurationFile.Save(configPath, appConfig, logger);
    }

    public void SaveEmulationConfig(EmulationConfig config)
    {
        var appConfig = AppConfigurationFile.LoadOrCreate(configPath, logger);
        appConfig.Emulation = config;
        AppConfigurationFile.Save(configPath, appConfig, logger);
    }

    internal static BootRomOptions LoadBootRomOptions(
        BootRomConfig config,
        string configDirectoryPath
    ) =>
        new()
        {
            DmgBootRom = ReadBootRom(config, HardwareModel.Dmg, configDirectoryPath),
            CgbBootRom = ReadBootRom(config, HardwareModel.Cgb, configDirectoryPath),
            SgbBootRom = ReadBootRom(config, HardwareModel.Sgb, configDirectoryPath),
        };

    private static ReadOnlyMemory<byte> ReadBootRom(
        BootRomConfig config,
        HardwareModel model,
        string configDirectoryPath
    )
    {
        var path = config.GetPath(model);
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
                : Path.GetFullPath(Path.Combine(configDirectoryPath, path));
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

    private static bool IsExpectedPathException(Exception exception) =>
        exception
            is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException;
}
