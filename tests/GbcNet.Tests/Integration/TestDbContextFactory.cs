// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GbcNet.Tests.Integration;

/// <summary>
/// SQLite-backed <see cref="IDbContextFactory{GbcNetDbContext}"/> for integration tests.
/// </summary>
internal sealed class TestDbContextFactory : IDbContextFactory<GbcNetDbContext>
{
    private readonly DbContextOptions<GbcNetDbContext> _options;
    private readonly Action? _beforeCreate;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates a factory for the SQLite database at <paramref name="databasePath"/>.
    /// </summary>
    /// <param name="databasePath">The SQLite database file used by every created context.</param>
    /// <param name="interceptor">An optional EF Core interceptor.</param>
    /// <param name="beforeCreate">An optional callback invoked before each context is created.</param>
    /// <param name="timeProvider">The time source, or the system provider when omitted.</param>
    public TestDbContextFactory(
        string databasePath,
        IInterceptor? interceptor = null,
        Action? beforeCreate = null,
        TimeProvider? timeProvider = null
    )
    {
        var builder = SqliteDbContextOptions.Configure(
            new DbContextOptionsBuilder<GbcNetDbContext>(),
            databasePath
        );
        if (interceptor is not null)
        {
            builder.AddInterceptors(interceptor);
        }

        _options = builder.Options;
        _beforeCreate = beforeCreate;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Runs the optional callback and creates a configured context.
    /// </summary>
    /// <returns>A new context for the configured SQLite file.</returns>
    public GbcNetDbContext CreateDbContext()
    {
        _beforeCreate?.Invoke();
        return new(_options, _timeProvider);
    }
}
