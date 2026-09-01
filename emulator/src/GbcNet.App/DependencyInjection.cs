// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Cheats;
using GbcNet.App.Configuration;
using GbcNet.App.Emulation;
using GbcNet.App.Infrastructure.Audio;
using GbcNet.App.Infrastructure.Persistence;
using GbcNet.App.Infrastructure.Storage;
using GbcNet.App.Input;
using GbcNet.App.Library;
using GbcNet.App.Saves;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace GbcNet.App;

internal static class DependencyInjection
{
    public static ServiceProvider BuildServiceProvider(StartupConfiguration startupConfiguration)
    {
        var services = new ServiceCollection();
        services.AddLogging(ConfigureLogging);
        services.AddSingleton(startupConfiguration);

        services.AddSingleton(provider => new AppConfigurationService(
            startupConfiguration.ConfigPath,
            provider.GetRequiredService<ILogger<AppConfigurationService>>()
        ));

        AddAppServices(services);

        services.AddTransient<MainWindow>();
        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }
        );
    }

    private static void AddAppServices(IServiceCollection services)
    {
        // Audio
        services.AddSingleton<IAudioOutput, SdlAudioOutput>();

        // Database
        services.AddSingleton(TimeProvider.System);
        services.AddDbContextFactory<GbcNetDbContext>(
            (_, options) =>
                SqliteDbContextOptions.Configure(options, UserDataPaths.LibraryDatabasePath)
        );

        // Input
        services.AddSingleton(provider =>
            InputMap.FromConfig(provider.GetRequiredService<StartupConfiguration>().InputConfig)
        );

        // Library
        services.AddSingleton(provider => new LibraryService(
            provider.GetRequiredService<IDbContextFactory<GbcNetDbContext>>(),
            UserDataPaths.CoverDirectoryPath,
            provider.GetRequiredService<ILogger<LibraryService>>(),
            provider.GetRequiredService<TimeProvider>()
        ));

        // Saves
        services.AddSingleton(provider => new CartridgeBatterySaveFileService(
            UserDataPaths.SaveDirectoryPath,
            provider.GetRequiredService<ILogger<CartridgeBatterySaveFileService>>()
        ));
        services.AddSingleton(provider => new SaveStateFileService(
            UserDataPaths.SaveStateDirectoryPath,
            provider.GetRequiredService<ILogger<SaveStateFileService>>()
        ));

        // Cheats
        services.AddSingleton<CheatCodeService>();
    }

    internal static void ConfigureLogging(ILoggingBuilder builder)
    {
#if DEBUG
        builder.AddDebug();
#endif
        builder.AddSerilog();
    }
}
