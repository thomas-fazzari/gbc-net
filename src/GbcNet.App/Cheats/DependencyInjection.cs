// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Microsoft.Extensions.DependencyInjection;

namespace GbcNet.App.Cheats;

internal static class DependencyInjection
{
    public static IServiceCollection AddCheats(this IServiceCollection services)
    {
        services.AddSingleton<GameGenieService>();
        return services;
    }
}
