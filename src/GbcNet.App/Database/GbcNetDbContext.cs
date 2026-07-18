// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Configuration;
using GbcNet.App.Database.Configurations;
using GbcNet.App.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GbcNet.App.Database;

internal sealed class GbcNetDbContext : DbContext
{
    private readonly TimeProvider? _timeProvider;

    public GbcNetDbContext(
        DbContextOptions<GbcNetDbContext> options,
        TimeProvider? timeProvider = null
    )
        : base(options)
    {
        _timeProvider = timeProvider;
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    public DbSet<LibraryRom> Roms => Set<LibraryRom>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampLibraryEntries();
        return base.SaveChanges(acceptAllChangesOnSuccess: acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default
    )
    {
        StampLibraryEntries();
        return base.SaveChangesAsync(
            acceptAllChangesOnSuccess: acceptAllChangesOnSuccess,
            cancellationToken
        );
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfiguration(new LibraryRomConfiguration());

    private void StampLibraryEntries()
    {
        if (_timeProvider is null)
        {
            return;
        }

        var timestamp = _timeProvider.GetUtcNow();
        foreach (var entry in ChangeTracker.Entries<LibraryRom>())
        {
            if (entry.State is EntityState.Added)
            {
                entry.Entity.StampCreated(timestamp);
                entry.Entity.StampUpdated(timestamp);
            }
            else if (entry.State is EntityState.Modified)
            {
                entry.Entity.StampUpdated(timestamp);
            }
        }
    }
}

internal sealed class GbcNetDbContextFactory : IDesignTimeDbContextFactory<GbcNetDbContext>
{
    public GbcNetDbContext CreateDbContext(string[] args)
    {
        var databasePath = args.Length > 0 ? args[0] : UserDataPaths.LibraryDatabasePath;
        var options = new DbContextOptionsBuilder<GbcNetDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
        return new GbcNetDbContext(options);
    }
}

internal static class GbcNetDbConstants
{
    public const string CaseInsensitiveCollation = "NOCASE";
}
