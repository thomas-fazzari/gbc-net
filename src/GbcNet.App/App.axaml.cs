// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using GbcNet.App.Audio;
using GbcNet.App.Configuration;
using GbcNet.App.Input;
using GbcNet.App.Library;
using GbcNet.App.Saves;
using Microsoft.Extensions.Logging;

namespace GbcNet.App;

internal sealed class GbcNetApplication : Application, IDisposable
{
    private ILoggerFactory? _loggerFactory;
    private SoundFlowAudioOutput? _audioOutput;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _loggerFactory = LoggerFactory.Create(static builder => builder.AddDebug());

            var startupConfiguration = StartupConfigurationLoader.Load(
                UserDataPaths.ConfigFilePath
            );

            var inputMap = InputMap.FromConfig(startupConfiguration.InputConfig);

            var configurationService = new AppConfigurationService(startupConfiguration.ConfigPath);

            var cartridgeSaveFileService = new CartridgeBatterySaveFileService(
                UserDataPaths.SaveDirectoryPath
            );

            LibraryService libraryService = new(
                new LibraryDatabase(UserDataPaths.LibraryDatabasePath)
            );

            _audioOutput = new SoundFlowAudioOutput(
                _loggerFactory.CreateLogger<SoundFlowAudioOutput>()
            );

            desktop.MainWindow = new MainWindow(
                inputMap,
                startupConfiguration,
                configurationService,
                cartridgeSaveFileService,
                libraryService,
                _audioOutput,
                _loggerFactory.CreateLogger<MainWindow>()
            );
            desktop.Exit += (_, _) => DisposeServices();
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void Dispose()
    {
        DisposeServices();
    }

    private void DisposeServices()
    {
        _audioOutput?.Dispose();
        _loggerFactory?.Dispose();
        _audioOutput = null;
        _loggerFactory = null;
    }
}
