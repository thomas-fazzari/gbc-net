// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

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
        return new SettingsConfig(appConfig.BootRoms, appConfig.Input);
    }

    public BootRomOptions LoadBootRomOptions(ICollection<string>? errors = null) =>
        LoadBootRomOptions(
            LoadBootRomConfig(),
            Path.GetDirectoryName(configPath) ?? Environment.CurrentDirectory,
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

        var bootRomErrors = new List<string>();

        AppConfig appConfig;
        try
        {
            appConfig = AppConfigurationFile.LoadOrCreate(configPath, logger);
        }
        catch (ConfigurationException)
        {
            appConfig = AppConfigurationFile.CreateDefault();
        }
        var configDirectoryPath = Path.GetDirectoryName(configPath) ?? Environment.CurrentDirectory;
        appConfig.BootRoms = new BootRomConfig(
            ResolveBootRomPath(
                settings.BootRoms.DmgPath,
                appConfig.BootRoms.DmgPath,
                HardwareModel.Dmg,
                configDirectoryPath,
                bootRomErrors
            ),
            ResolveBootRomPath(
                settings.BootRoms.CgbPath,
                appConfig.BootRoms.CgbPath,
                HardwareModel.Cgb,
                configDirectoryPath,
                bootRomErrors
            ),
            ResolveBootRomPath(
                settings.BootRoms.SgbPath,
                appConfig.BootRoms.SgbPath,
                HardwareModel.Sgb,
                configDirectoryPath,
                bootRomErrors
            )
        );
        appConfig.Input = CopyInput(settings.Input);
        AppConfigurationFile.Save(configPath, appConfig, logger);
        return bootRomErrors;
    }

    public void SaveEmulationConfig(EmulationConfig config)
    {
        var appConfig = AppConfigurationFile.LoadOrCreate(configPath, logger);
        appConfig.Emulation = config;
        AppConfigurationFile.Save(configPath, appConfig, logger);
    }

    internal static BootRomOptions LoadBootRomOptions(
        BootRomConfig config,
        string configDirectoryPath,
        ICollection<string>? errors = null
    ) =>
        new()
        {
            DmgBootRom = ReadBootRom(
                config.GetPath(HardwareModel.Dmg),
                HardwareModel.Dmg,
                configDirectoryPath,
                errors
            ),
            CgbBootRom = ReadBootRom(
                config.GetPath(HardwareModel.Cgb),
                HardwareModel.Cgb,
                configDirectoryPath,
                errors
            ),
            SgbBootRom = ReadBootRom(
                config.GetPath(HardwareModel.Sgb),
                HardwareModel.Sgb,
                configDirectoryPath,
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
            return ReadBootRomFile(path, model, configDirectoryPath);
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
            _ = ReadBootRomFile(proposedPath, model, configDirectoryPath);
            return proposedPath;
        }
        catch (ConfigurationException exception)
        {
            errors.Add(exception.Message);
        }

        if (string.Equals(proposedPath, currentPath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            _ = ReadBootRomFile(currentPath, model, configDirectoryPath);
            return currentPath;
        }
        catch (ConfigurationException)
        {
            return null;
        }
    }

    private static InputConfig CopyInput(InputConfig input) =>
        new()
        {
            Version = input.Version,
            Keyboard = new()
            {
                ActiveProfile = input.Keyboard.ActiveProfile,
                Profiles = input.Keyboard.Profiles.ToDictionary(
                    profile => profile.Key,
                    profile => new KeyboardProfileConfig
                    {
                        Bindings =
                        [
                            .. profile.Value.Bindings.Select(
                                binding => new KeyboardInputBindingConfig(
                                    binding.ButtonName,
                                    binding.KeyName
                                )
                            ),
                        ],
                    },
                    StringComparer.OrdinalIgnoreCase
                ),
            },
            Gamepad = new()
            {
                ActiveProfile = input.Gamepad.ActiveProfile,
                Profiles = input.Gamepad.Profiles.ToDictionary(
                    profile => profile.Key,
                    profile => new GamepadProfileConfig
                    {
                        Bindings =
                        [
                            .. profile.Value.Bindings.Select(
                                binding => new GamepadInputBindingConfig(
                                    binding.ButtonName,
                                    binding.ControlName
                                )
                            ),
                        ],
                    },
                    StringComparer.OrdinalIgnoreCase
                ),
            },
        };

    private static bool IsExpectedPathException(Exception exception) =>
        exception
            is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException;
}
