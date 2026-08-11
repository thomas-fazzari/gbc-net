// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using GbcNet.App.Configuration;
using GbcNet.App.Configuration.Sections.Appearance;
using GbcNet.App.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GbcNet.App;

internal sealed class GbcNetApplication : Application
{
    private ServiceProvider? _services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            using var startupLoggerFactory = LoggerFactory.Create(
                DependencyInjection.ConfigureLogging
            );

            var startupConfiguration = StartupConfigurationLoader.Load(
                UserDataPaths.ConfigFilePath,
                startupLoggerFactory.CreateLogger(
                    "GbcNet.App.Configuration.StartupConfigurationLoader"
                )
            );
            ApplyTheme(startupConfiguration.AppearanceConfig.Theme);
            _services = DependencyInjection.BuildServiceProvider(startupConfiguration);
            MigrateDatabase(_services);
            desktop.MainWindow = _services.GetRequiredService<MainWindow>();
            desktop.Exit += (_, _) =>
            {
                _services?.Dispose();
                _services = null;
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    internal static ThemeVariant GetThemeVariant(ThemeMode theme) =>
        theme switch
        {
            ThemeMode.System => ThemeVariant.Default,
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, message: null),
        };

    internal static void ApplyTheme(ThemeMode theme)
    {
        if (Current is { } application)
        {
            application.RequestedThemeVariant = GetThemeVariant(theme);
        }
    }

    private static void MigrateDatabase(IServiceProvider services) =>
        DatabaseMigrator.Migrate(
            services.GetRequiredService<IDbContextFactory<GbcNetDbContext>>(),
            UserDataPaths.LibraryDatabasePath,
            services
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("GbcNet.App.Database.DatabaseMigrator")
        );
}
