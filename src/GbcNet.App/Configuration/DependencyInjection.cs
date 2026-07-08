// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Microsoft.Extensions.DependencyInjection;

namespace GbcNet.App.Configuration;

internal static class DependencyInjection
{
    [DependencyInjectionModule]
    public static IServiceCollection AddConfiguration(this IServiceCollection services)
    {
        services.AddSingleton(provider => new AppConfigurationService(
            provider.GetRequiredService<StartupConfiguration>().ConfigPath
        ));
        return services;
    }
}
