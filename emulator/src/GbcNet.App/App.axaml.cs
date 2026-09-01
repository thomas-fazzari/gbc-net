// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GbcNet.App.Configuration;
using GbcNet.App.Infrastructure.Persistence;
using GbcNet.App.Infrastructure.Storage;
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

    private static void MigrateDatabase(IServiceProvider services) =>
        DatabaseMigrator.Migrate(
            services.GetRequiredService<IDbContextFactory<GbcNetDbContext>>(),
            UserDataPaths.LibraryDatabasePath,
            services
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("GbcNet.App.Infrastructure.Persistence.DatabaseMigrator")
        );
}
