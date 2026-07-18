// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Audio;
using GbcNet.App.Configuration;
using GbcNet.App.Database;
using GbcNet.App.Input;
using GbcNet.App.Library;
using GbcNet.App.Saves;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace GbcNet.App;

internal static class DependencyInjection
{
    public static ServiceProvider BuildServiceProvider(StartupConfiguration startupConfiguration)
    {
        var services = new ServiceCollection();
        services.AddLogging(static builder => builder.AddDebug().AddSerilog());
        services.AddSingleton(startupConfiguration);

        services.AddSingleton(provider => new AppConfigurationService(
            startupConfiguration.ConfigPath,
            provider.GetRequiredService<ILogger<AppConfigurationService>>()
        ));

        services.AddAudio();
        services.AddDatabase();
        services.AddInput();
        services.AddLibrary();
        services.AddSaves();

        services.AddTransient<MainWindow>();
        return services.BuildServiceProvider();
    }
}
