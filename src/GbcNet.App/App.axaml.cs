// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using GbcNet.App.Configuration;
using GbcNet.App.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace GbcNet.App;

internal sealed class GbcNetApplication : Application
{
    private ServiceProvider? _services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            using var startupLoggerFactory = LoggerFactory.Create(static builder =>
                builder.AddDebug().AddSerilog()
            );

            var startupConfiguration = StartupConfigurationLoader.Load(
                UserDataPaths.ConfigFilePath,
                startupLoggerFactory.CreateLogger(
                    "GbcNet.App.Configuration.StartupConfigurationLoader"
                )
            );
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

    private static void MigrateDatabase(IServiceProvider services)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(UserDataPaths.LibraryDatabasePath) ?? ".");
        using var db = services
            .GetRequiredService<IDbContextFactory<GbcNetDbContext>>()
            .CreateDbContext();
        db.Database.Migrate();
    }
}
