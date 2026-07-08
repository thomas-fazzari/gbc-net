// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GbcNet.App.Database;

internal static class DependencyInjection
{
    [DependencyInjectionModule]
    public static IServiceCollection AddDatabase(this IServiceCollection services) =>
        services.AddDatabase(UserDataPaths.LibraryDatabasePath);

    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        string databasePath,
        TimeProvider? timeProvider = null
    )
    {
        services.AddSingleton(timeProvider ?? TimeProvider.System);
        services.AddSingleton<IDbContextFactory<GbcNetDbContext>>(provider =>
        {
            var options = new DbContextOptionsBuilder<GbcNetDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;
            return new RuntimeDbContextFactory(
                options,
                provider.GetRequiredService<TimeProvider>()
            );
        });
        return services;
    }

    private sealed class RuntimeDbContextFactory(
        DbContextOptions<GbcNetDbContext> options,
        TimeProvider timeProvider
    ) : IDbContextFactory<GbcNetDbContext>
    {
        public GbcNetDbContext CreateDbContext() => new(options, timeProvider);
    }
}
