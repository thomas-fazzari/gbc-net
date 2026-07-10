// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GbcNet.App.Database;

internal static class DependencyInjection
{
    public static IServiceCollection AddDatabase(this IServiceCollection services) =>
        services.AddDatabase(UserDataPaths.LibraryDatabasePath);

    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        string databasePath,
        TimeProvider? timeProvider = null
    )
    {
        services.AddSingleton(timeProvider ?? TimeProvider.System);
        services.AddDbContextFactory<GbcNetDbContext>(
            (_, options) => options.UseSqlite($"Data Source={databasePath}")
        );
        return services;
    }
}
