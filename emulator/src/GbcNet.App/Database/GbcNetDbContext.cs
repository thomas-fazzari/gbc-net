// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Configuration;
using GbcNet.App.Database.Configurations;
using GbcNet.App.Database.Entities;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GbcNet.App.Database;

internal sealed class GbcNetDbContext : DbContext
{
    private readonly TimeProvider _timeProvider;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Properties<Enum>().HaveConversion<string>();

    public GbcNetDbContext(DbContextOptions<GbcNetDbContext> options, TimeProvider timeProvider)
        : base(options)
    {
        _timeProvider = timeProvider;
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    public DbSet<LibraryRom> Roms => Set<LibraryRom>();
    public DbSet<StoredCheatCode> CheatCodes => Set<StoredCheatCode>();

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .ApplyConfiguration(new LibraryRomConfiguration())
            .ApplyConfiguration(new StoredCheatCodeConfiguration());
    }

    private void StampLibraryEntries()
    {
        var timestamp = _timeProvider.GetUtcNow();

        foreach (var entry in ChangeTracker.Entries<LibraryRom>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.StampCreated(timestamp);
                    entry.Entity.StampUpdated(timestamp);
                    break;
                case EntityState.Modified:
                    entry.Entity.StampUpdated(timestamp);
                    break;
            }
        }
    }
}

[UsedImplicitly]
internal sealed class GbcNetDbContextFactory : IDesignTimeDbContextFactory<GbcNetDbContext>
{
    public GbcNetDbContext CreateDbContext(string[] args)
    {
        var databasePath = args.Length > 0 ? args[0] : UserDataPaths.LibraryDatabasePath;
        var options = SqliteDbContextOptions
            .Configure(new DbContextOptionsBuilder<GbcNetDbContext>(), databasePath)
            .Options;
        return new GbcNetDbContext(options, TimeProvider.System);
    }
}
