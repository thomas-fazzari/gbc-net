// Copyright (C) 2026 thomas-fazzari, Fournux
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using GbcNet.App.Configuration;
using GbcNet.App.Configuration.Sections.Audio;
using GbcNet.App.Configuration.Sections.Library;
using GbcNet.App.Emulation;
using GbcNet.App.Input;
using GbcNet.App.Shell.Chrome;
using Microsoft.Extensions.Logging;

namespace GbcNet.App.Shell;

/// <summary>
/// Wires <see cref="MainMenu"/> callbacks to the session, presenters, and services.
/// </summary>
internal sealed class MainWindowMenuAdapter(
    MainMenu mainMenu,
    Window window,
    EmulationSessionPresenter emulationSession,
    GamepadManager gamepadManager,
    IAudioOutput audioOutput,
    AudioConfig audioConfig,
    AppConfigurationService configurationService,
    ShellPresenter shell,
    ShellOperationRunner operationRunner,
    ILogger<MainWindowMenuAdapter> logger
)
{
    private AudioConfig _audioConfig = audioConfig;

    public void Configure(
        EmulationView emulationView,
        ConfigurationPresenter configurationPresenter
    )
    {
        ApplyAudioConfig(_audioConfig);

        mainMenu.AttachToWindow(window);
        mainMenu.OpenRom = () =>
            operationRunner.Run(() => emulationSession.OpenRomAsync(window.StorageProvider));
        mainMenu.Close = () => operationRunner.Run(emulationSession.StopAsync);
        mainMenu.OpenConfiguration = () =>
            operationRunner.Run(() => configurationPresenter.OpenAsync(window));
        mainMenu.OpenConfigurationFileLocation = () =>
            operationRunner.Run(configurationPresenter.OpenConfigurationDirectory);
        mainMenu.OpenLogFileLocation = () =>
            operationRunner.Run(ConfigurationPresenter.OpenLogDirectory);
        mainMenu.TogglePause = emulationSession.TogglePause;
        mainMenu.Reset = () => operationRunner.Run(emulationSession.ResetAsync);
        mainMenu.OpenCheats = () =>
            operationRunner.Run(async () =>
            {
                var gameplayEnabled = gamepadManager.GameplayEnabled;
                gamepadManager.SetGameplayEnabled(enabled: false);
                try
                {
                    await emulationSession.OpenCheatsAsync(window);
                }
                finally
                {
                    gamepadManager.SetGameplayEnabled(enabled: gameplayEnabled);
                    Dispatcher.UIThread.Post(() => emulationView.Focus(), DispatcherPriority.Input);
                }
            });
        mainMenu.ToggleMute = ToggleMute;
        mainMenu.SaveState = slotIndex =>
            operationRunner.Run(() => emulationSession.SaveStateAsync(slotIndex));
        mainMenu.LoadState = slotIndex =>
            operationRunner.Run(() => emulationSession.LoadStateAsync(slotIndex));
        mainMenu.ToggleFastForward = emulationSession.ToggleFastForward;
        mainMenu.SetFastForwardSpeed = speed =>
        {
            emulationSession.SetFastForwardSpeed(speed);
            Dispatcher.UIThread.Post(() => emulationView.Focus(), DispatcherPriority.Input);
        };
        mainMenu.ToggleFullscreen = ToggleFullscreen;
        mainMenu.OpenGitHubRepository = () => operationRunner.Run(OpenGitHubRepository);
        window.Activated += (_, _) => gamepadManager.SetGameplayEnabled(enabled: true);
        window.Deactivated += (_, _) => gamepadManager.SetGameplayEnabled(enabled: false);
        SyncMenuState();
        emulationSession.SyncMenuState();
    }

    public void SyncMenuState() => mainMenu.SetMuteState(isMuted: _audioConfig.Muted);

    public void ApplyAudioConfig(AudioConfig config)
    {
        _audioConfig = config;
        audioOutput.SetVolume(config.VolumePercent, config.Muted);
        mainMenu.SetMuteState(isMuted: config.Muted);
    }

    public void ToggleMute()
    {
        ApplyAudioConfig(_audioConfig with { Muted = !_audioConfig.Muted });
        try
        {
            var result = configurationService.SaveAudioConfig(_audioConfig);
            if (result.IsError)
            {
                shell.ShowError(result.FirstError.Description);
            }
        }
        catch (ConfigurationException exception)
        {
            MainWindowLog.AudioSettingsSaveFailed(logger, exception);
            shell.ShowError(exception.Message);
        }
    }

    public void SaveLibraryViewMode(LibraryViewMode viewMode)
    {
        try
        {
            configurationService.SaveLibraryConfig(new LibraryConfig { ViewMode = viewMode });
        }
        catch (ConfigurationException exception)
        {
            MainWindowLog.LibrarySettingsSaveFailed(logger, exception);
            shell.ShowError(exception.Message);
        }
    }

    private void ToggleFullscreen()
    {
        window.WindowState =
            window.WindowState is WindowState.FullScreen
                ? WindowState.Normal
                : WindowState.FullScreen;
    }

    private static void OpenGitHubRepository()
    {
        using var process =
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = "https://github.com/thomas-fazzari/gbc-net",
                    UseShellExecute = true,
                }
            ) ?? throw new InvalidOperationException("GitHub repository could not be opened.");
    }
}
