// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GbcNet.App.Input;

internal static class DependencyInjection
{
    public static IServiceCollection AddInput(this IServiceCollection services)
    {
        services.AddSingleton(provider =>
            InputMap.FromConfig(provider.GetRequiredService<StartupConfiguration>().InputConfig)
        );
        return services;
    }
}
