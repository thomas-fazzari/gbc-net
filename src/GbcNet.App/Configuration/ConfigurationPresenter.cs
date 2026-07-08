// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using Avalonia.Controls;
using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.App.Shell.Chrome;
using GbcNet.Core;

namespace GbcNet.App.Configuration;

internal sealed class ConfigurationPresenter(
    AppConfigurationService configurationService,
    string configPath,
    StatusBarPresenter statusBar,
    Action<BootRomOptions> setBootRomOptions
)
{
    public async Task OpenAsync(Window owner)
    {
        BootRomConfig bootRomConfig;
        try
        {
            bootRomConfig = configurationService.LoadBootRomConfig();
        }
        catch (ConfigurationException exception)
        {
            statusBar.ShowError(ConfigurationErrors.Format(exception));
            return;
        }

        var savedConfig = await new SettingsWindow(bootRomConfig)
            .ShowDialog<BootRomConfig?>(owner)
            .ConfigureAwait(true);

        if (savedConfig is null)
        {
            return;
        }

        try
        {
            configurationService.SaveBootRomConfig(savedConfig.Value);
        }
        catch (ConfigurationException exception)
        {
            statusBar.ShowError(ConfigurationErrors.Format(exception));
            return;
        }

        ReloadBootRomOptions();
    }

    public Task OpenConfigurationDirectoryAsync()
    {
        var directoryPath = Path.GetDirectoryName(configPath);

        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new InvalidOperationException("Configuration file path has no directory.");
        }

        Directory.CreateDirectory(directoryPath);

        using var process =
            Process.Start(new ProcessStartInfo { FileName = directoryPath, UseShellExecute = true })
            ?? throw new InvalidOperationException(
                "Configuration file location could not be opened."
            );

        return Task.CompletedTask;
    }

    private void ReloadBootRomOptions()
    {
        try
        {
            setBootRomOptions(configurationService.LoadBootRomOptions());
        }
        catch (ConfigurationException exception)
        {
            setBootRomOptions(new BootRomOptions());
            statusBar.ShowError(ConfigurationErrors.Format(exception));
        }
    }
}
