// Copyright (C) 2026 thomas-fazzari, Fournux
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using GbcNet.App.Audio;
using GbcNet.App.Configuration;
using GbcNet.App.Configuration.Sections.Audio;
using GbcNet.App.Configuration.Sections.Library;
using GbcNet.App.Emulation;
using GbcNet.App.Input;
using GbcNet.App.Library;
using GbcNet.App.Menus;
using GbcNet.App.Shell.Chrome;
using Microsoft.Extensions.Logging;

namespace GbcNet.App.Shell;

/// <summary>
/// Wires <see cref="MainMenu"/> callbacks to the session/presenter/services and
/// keeps menu check-state in sync with audio, fullscreen, and visibility state.
/// </summary>
internal sealed class MainWindowMenuAdapter(
    MainMenu mainMenu,
    Window window,
    EmulationSessionPresenter emulationSession,
    GamepadManager gamepadManager,
    IAudioOutput audioOutput,
    AppConfigurationService configurationService,
    StatusBarPresenter statusBar,
    ShellOperationRunner operationRunner,
    MenuBarVisibilityController menuBarVisibility,
    StatusBarVisibilityController statusBarVisibility,
    ILogger<MainWindowMenuAdapter> logger
)
{
    // Set by Configure() before any menu interaction.
    private AudioConfig _audioConfig = null!;

    public void Configure(
        EmulationView emulationView,
        AudioConfig audioConfig,
        ConfigurationPresenter configurationPresenter
    )
    {
        _audioConfig = audioConfig;
        ApplyAudioConfig(_audioConfig);

        mainMenu.AttachNativeMenu(window);
        mainMenu.OpenRom = () =>
            operationRunner.Run(() => emulationSession.OpenRomAsync(window.StorageProvider));
        mainMenu.RefreshRecentRoms = emulationSession.SyncRecentRoms;
        mainMenu.OpenRecentRom = path =>
            operationRunner.Run(() =>
                emulationSession.OpenRecentRomAsync(window.StorageProvider, path)
            );
        mainMenu.Close = () => operationRunner.Run(emulationSession.StopAsync);
        mainMenu.OpenConfiguration = () =>
            operationRunner.Run(() => configurationPresenter.OpenAsync(window));
        mainMenu.OpenConfigurationFileLocation = () =>
            operationRunner.Run(configurationPresenter.OpenConfigurationDirectoryAsync);
        mainMenu.OpenLogFileLocation = () =>
            operationRunner.Run(ConfigurationPresenter.OpenLogDirectoryAsync);
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
        mainMenu.ToggleMenuBar = menuBarVisibility.Toggle;
        mainMenu.ToggleStatusBar = statusBarVisibility.Toggle;
        mainMenu.OpenGitHubRepository = () => operationRunner.Run(OpenGitHubRepositoryAsync);
        window.Activated += (_, _) => gamepadManager.SetGameplayEnabled(enabled: true);
        window.Deactivated += (_, _) => gamepadManager.SetGameplayEnabled(enabled: false);
        SyncMenuState();
        emulationSession.SyncMenuState();
        emulationSession.SyncRecentRoms();
    }

    public void SyncMenuState()
    {
        mainMenu.SetFullscreenState(isFullscreen: window.WindowState is WindowState.FullScreen);
        mainMenu.SetMuteState(isMuted: _audioConfig.Muted);
        menuBarVisibility.Apply();
        statusBarVisibility.Apply();
    }

    public void SyncFullscreenState(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property != Window.WindowStateProperty)
        {
            return;
        }

        menuBarVisibility.Apply();
        mainMenu.SetFullscreenState(isFullscreen: window.WindowState is WindowState.FullScreen);
    }

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
            configurationService.SaveAudioConfig(_audioConfig);
        }
        catch (ConfigurationException exception)
        {
            MainWindowLog.AudioSettingsSaveFailed(logger, exception);
            statusBar.ShowError(exception.Message);
        }
    }

    public void SaveLibraryViewMode(LibraryView libraryView, LibraryViewMode viewMode)
    {
        try
        {
            configurationService.SaveLibraryConfig(new LibraryConfig { ViewMode = viewMode });
        }
        catch (ConfigurationException exception)
        {
            MainWindowLog.LibrarySettingsSaveFailed(logger, exception);
            libraryView.ShowError(exception.Message);
        }
    }

    private void ToggleFullscreen()
    {
        window.WindowState =
            window.WindowState is WindowState.FullScreen
                ? WindowState.Normal
                : WindowState.FullScreen;
    }

    private static Task OpenGitHubRepositoryAsync()
    {
        using var process =
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = "https://github.com/thomas-fazzari/gbc-net",
                    UseShellExecute = true,
                }
            ) ?? throw new InvalidOperationException("GitHub repository could not be opened.");

        return Task.CompletedTask;
    }
}
