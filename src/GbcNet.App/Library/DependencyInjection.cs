// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Configuration;
using GbcNet.App.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GbcNet.App.Library;

internal static class DependencyInjection
{
    public static IServiceCollection AddLibrary(this IServiceCollection services)
    {
        services.AddSingleton<LibraryService>(provider => new LibraryService(
            provider.GetRequiredService<IDbContextFactory<GbcNetDbContext>>(),
            UserDataPaths.CoverDirectoryPath,
            provider.GetRequiredService<TimeProvider>()
        ));
        return services;
    }
}
