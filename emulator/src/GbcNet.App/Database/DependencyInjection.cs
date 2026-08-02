// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GbcNet.App.Database;

internal static class DependencyInjection
{
    public static IServiceCollection AddDatabase(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddDbContextFactory<GbcNetDbContext>(
            (_, options) =>
                SqliteDbContextOptions.Configure(options, UserDataPaths.LibraryDatabasePath)
        );
        return services;
    }
}
