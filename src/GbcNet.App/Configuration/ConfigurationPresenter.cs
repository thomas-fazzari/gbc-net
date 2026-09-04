// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using Avalonia.Controls;
using ErrorOr;
using GbcNet.App.Configuration.Sections.Audio;
using GbcNet.App.Configuration.Sections.Input;
using GbcNet.App.Infrastructure.Configuration;
using GbcNet.App.Infrastructure.Storage;
using GbcNet.App.Input;
using GbcNet.App.Shell.Chrome;
using GbcNet.Core;
using Microsoft.Extensions.Logging;

namespace GbcNet.App.Configuration;

internal sealed class ConfigurationPresenter(
    AppConfigurationService configurationService,
    string configPath,
    ShellPresenter shell,
    Action<BootRomOptions> setBootRomOptions,
    Action<InputConfig> applyInputConfig,
    Action<AudioConfig> applyAudioConfig,
    GamepadManager gamepadManager,
    ILogger<ConfigurationPresenter> logger,
    ILogger settingsLogger
)
{
    public async Task OpenAsync(Window owner)
    {
        SettingsConfig settings;
        try
        {
            settings = LoadSettingsDraft();
        }
        catch (ConfigurationException exception)
        {
            ConfigurationPresenterLog.LoadFailed(logger, exception);
            shell.ShowError(exception.Message);
            var defaults = AppConfigurationFile.CreateDefault();
            settings = new SettingsConfig(defaults.BootRoms, defaults.Input)
            {
                Audio = defaults.Audio,
            };
        }

        var gameplayEnabled = gamepadManager.GameplayEnabled;
        gamepadManager.SetGameplayEnabled(enabled: false);

        SettingsConfig? savedConfig;
        try
        {
            savedConfig = await new SettingsWindow(
                settings,
                gamepadManager,
                settingsLogger
            ).ShowDialog<SettingsConfig?>(owner);
        }
        finally
        {
            gamepadManager.SetGameplayEnabled(enabled: gameplayEnabled);
        }

        if (savedConfig is null)
        {
            return;
        }

        SaveAndApply(savedConfig);
    }

    public void OpenConfigurationDirectory() =>
        OpenDirectory(
            Path.GetDirectoryName(configPath),
            "Configuration file location could not be opened."
        );

    public static void OpenLogDirectory() =>
        OpenDirectory(
            Path.GetDirectoryName(UserDataPaths.LogFilePath),
            "Log file location could not be opened."
        );

    private static void OpenDirectory(string? directoryPath, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new InvalidOperationException(errorMessage);
        }

        Directory.CreateDirectory(directoryPath);

        using var process =
            Process.Start(new ProcessStartInfo { FileName = directoryPath, UseShellExecute = true })
            ?? throw new InvalidOperationException(errorMessage);
    }

    private void SaveAndApply(SettingsConfig settings)
    {
        ErrorOr<IReadOnlyList<string>> result;
        try
        {
            result = configurationService.SaveSettings(settings);
        }
        catch (ConfigurationException exception)
        {
            ConfigurationPresenterLog.SaveFailed(logger, exception);
            shell.ShowError(exception.Message);
            return;
        }

        if (result.IsError)
        {
            shell.ShowError(result.FirstError.Description);
            return;
        }

        applyInputConfig(settings.Input);
        applyAudioConfig(settings.Audio);
        ReloadBootRomOptions();

        if (result.Value.Count != 0)
        {
            shell.ShowError(string.Join(Environment.NewLine, result.Value));
        }
    }

    private SettingsConfig LoadSettingsDraft()
    {
        var settings = configurationService.LoadSettings();

        var errors = InputConfigValidator.Validate(settings.Input);
        if (errors.Count == 0)
        {
            return settings;
        }

        shell.ShowError(string.Join(Environment.NewLine, errors));
        return settings with { Input = AppConfigurationFile.CreateDefaultInputConfig() };
    }

    private void ReloadBootRomOptions()
    {
        var errors = new List<string>();
        try
        {
            setBootRomOptions(configurationService.LoadBootRomOptions(errors));
        }
        catch (ConfigurationException exception)
        {
            errors.Add(exception.Message);
            setBootRomOptions(new BootRomOptions());
        }

        if (errors.Count != 0)
        {
            shell.ShowError(string.Join(Environment.NewLine, errors));
        }
    }
}

internal static partial class ConfigurationPresenterLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Settings could not be loaded. Defaults will be shown."
    )]
    internal static partial void LoadFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Settings could not be saved.")]
    internal static partial void SaveFailed(ILogger logger, Exception exception);
}
