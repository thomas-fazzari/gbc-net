using System.Diagnostics;
using Avalonia.Controls;
using GbcNet.App.Chrome;
using GbcNet.App.Configuration.Sections.BootRom;
using GbcNet.App.Shell;
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
        var bootRomConfig = configurationService.LoadBootRomConfig();

        if (bootRomConfig.IsFailed)
        {
            statusBar.ShowError(ResultErrors.Format(bootRomConfig.Errors));
            return;
        }

        var savedConfig = await new SettingsWindow(bootRomConfig.Value)
            .ShowDialog<BootRomConfig?>(owner)
            .ConfigureAwait(true);

        if (savedConfig is null)
        {
            return;
        }

        var saved = configurationService.SaveBootRomConfig(savedConfig.Value);

        if (saved.IsFailed)
        {
            statusBar.ShowError(ResultErrors.Format(saved.Errors));
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
        var bootRomOptions = configurationService.LoadBootRomOptions();

        if (bootRomOptions.IsFailed)
        {
            setBootRomOptions(new BootRomOptions());
            statusBar.ShowError(ResultErrors.Format(bootRomOptions.Errors));
            return;
        }

        setBootRomOptions(bootRomOptions.Value);
    }
}
