// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GbcNet.App.Saves;

internal static class DependencyInjection
{
    public static IServiceCollection AddSaves(this IServiceCollection services)
    {
        services.AddSingleton(new CartridgeBatterySaveFileService(UserDataPaths.SaveDirectoryPath));
        services.AddSingleton(provider => new SaveStateFileService(
            UserDataPaths.SaveStateDirectoryPath,
            provider.GetRequiredService<ILogger<SaveStateFileService>>()
        ));
        return services;
    }
}
