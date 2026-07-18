// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using Avalonia.Controls;
using GbcNet.App.Configuration.Sections.Input;
using GbcNet.App.Input;
using GbcNet.App.Shell.Chrome;
using GbcNet.Core;
using Microsoft.Extensions.Logging;

namespace GbcNet.App.Configuration;

internal sealed class ConfigurationPresenter(
    AppConfigurationService configurationService,
    string configPath,
    StatusBarPresenter statusBar,
    Action<BootRomOptions> setBootRomOptions,
    Action<InputConfig> applyInputConfig,
    GamepadManager gamepadManager,
    ILogger<ConfigurationPresenter> logger
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
            statusBar.ShowError(exception.Message);
            var defaults = AppConfigurationFile.CreateDefault();
            settings = new SettingsConfig(defaults.BootRoms, defaults.Input);
        }

        var gameplayEnabled = gamepadManager.GameplayEnabled;
        gamepadManager.SetGameplayEnabled(enabled: false);

        SettingsConfig? savedConfig;
        try
        {
            savedConfig = await new SettingsWindow(
                settings,
                gamepadManager
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

    public Task OpenConfigurationDirectoryAsync() =>
        OpenDirectoryAsync(
            Path.GetDirectoryName(configPath),
            "Configuration file location could not be opened."
        );

    public static Task OpenLogDirectoryAsync() =>
        OpenDirectoryAsync(
            Path.GetDirectoryName(UserDataPaths.LogFilePath),
            "Log file location could not be opened."
        );

    private static Task OpenDirectoryAsync(string? directoryPath, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new InvalidOperationException(errorMessage);
        }

        Directory.CreateDirectory(directoryPath);

        using var process =
            Process.Start(new ProcessStartInfo { FileName = directoryPath, UseShellExecute = true })
            ?? throw new InvalidOperationException(errorMessage);

        return Task.CompletedTask;
    }

    private void SaveAndApply(SettingsConfig settings)
    {
        IReadOnlyList<string> bootRomErrors;
        try
        {
            bootRomErrors = configurationService.SaveSettings(settings);
        }
        catch (ConfigurationException exception)
        {
            ConfigurationPresenterLog.SaveFailed(logger, exception);
            statusBar.ShowError(exception.Message);
            return;
        }

        applyInputConfig(settings.Input);
        ReloadBootRomOptions();

        if (bootRomErrors.Count != 0)
        {
            statusBar.ShowError(string.Join(Environment.NewLine, bootRomErrors));
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

        statusBar.ShowError(string.Join(Environment.NewLine, errors));
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
            statusBar.ShowError(string.Join(Environment.NewLine, errors));
        }
    }
}

internal static partial class ConfigurationPresenterLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Settings could not be loaded; defaults will be shown."
    )]
    internal static partial void LoadFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Settings could not be saved.")]
    internal static partial void SaveFailed(ILogger logger, Exception exception);
}
