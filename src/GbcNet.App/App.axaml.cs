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

namespace GbcNet.App;

internal sealed class GbcNetApplication : Application, IDisposable
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
            var startupConfiguration = StartupConfigurationLoader.Load(
                UserDataPaths.ConfigFilePath
            );
            _services = DependencyInjection.BuildServiceProvider(startupConfiguration);
            MigrateDatabase(_services);
            desktop.MainWindow = _services.GetRequiredService<MainWindow>();
            desktop.Exit += (_, _) => DisposeServices();
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

    public void Dispose()
    {
        DisposeServices();
    }

    private void DisposeServices()
    {
        _services?.Dispose();
        _services = null;
    }
}
