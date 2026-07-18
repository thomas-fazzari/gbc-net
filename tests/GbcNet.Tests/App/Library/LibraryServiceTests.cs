// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using GbcNet.App.Database;
using GbcNet.App.Database.Entities;
using GbcNet.App.Library;
using GbcNet.Core.Cartridges;
using GbcNet.Tests.Cartridges;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace GbcNet.Tests.App.Library;

public sealed class LibraryServiceTests
{
    [Fact]
    public async Task RecordOpenedRomAsync_AppliesLatestSchemaMigration()
    {
        using var test = new LibraryTestContext();
        var romPath = await test.WriteRomAsync("game.gb", TestRomFactory.Create());

        await test.Library.RecordOpenedRomAsync(romPath);

        Assert.Contains(
            "InitialLibrarySchema",
            Assert.Single(GetAppliedMigrations(test.DatabasePath)),
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task RecordOpenedRomAsync_UpsertsByRomHashAndUpdatesLastKnownPath()
    {
        using var test = new LibraryTestContext();
        var rom = TestRomFactory.Create();
        var firstPath = await test.WriteRomAsync("first.gb", rom);
        var secondPath = await test.WriteRomAsync("second.gb", rom);

        await test.Library.RecordOpenedRomAsync(firstPath);
        test.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        await test.Library.RecordOpenedRomAsync(secondPath);

        var entry = Assert.Single(test.Library.GetRoms(limit: 10));
        Assert.Equal(Path.GetFullPath(secondPath), entry.LastKnownPath);
        Assert.Equal("second.gb", entry.FileName);
        Assert.Equal("TEST ROM", entry.CartridgeTitle);
        Assert.Equal(2, entry.LaunchCount);
        Assert.Equal(new DateTimeOffset(2026, 6, 27, 12, 0, 0, TimeSpan.Zero), entry.AddedAt);
        Assert.Equal(new DateTimeOffset(2026, 6, 27, 12, 1, 0, TimeSpan.Zero), entry.LastOpenedAt);
        Assert.Null(entry.CoverPath);
    }

    [Fact]
    public async Task RecordOpenedRomAsync_ReplacesPreviousHashForSamePathAndRemovesManagedCover()
    {
        using var test = new LibraryTestContext();
        var romPath = await test.WriteRomAsync("game.gb", TestRomFactory.Create());
        await test.Library.RecordOpenedRomAsync(romPath);
        var oldRomHash = Assert.Single(test.Library.GetRoms(limit: 10)).RomHash;
        var sourceImagePath = await test.WriteImageAsync("old-cover.png", [0x10, 0x11, 0x12]);
        test.Library.AssignCoverImage(oldRomHash, sourceImagePath);
        var oldCoverPath =
            Assert.Single(test.Library.GetRoms(limit: 10)).CoverPath
            ?? throw new InvalidOperationException("Cover path was not stored.");
        await test.WriteRomAsync(
            "game.gb",
            TestRomFactory.Create(bytes => "SECOND ROM"u8.CopyTo(bytes.AsSpan(0x0134)))
        );

        Assert.True(File.Exists(oldCoverPath));
        await test.Library.RecordOpenedRomAsync(romPath);

        var entry = Assert.Single(test.Library.GetRoms(limit: 10));
        Assert.Equal(Path.GetFullPath(romPath), entry.LastKnownPath);
        Assert.Equal("SECOND ROM", entry.CartridgeTitle);
        Assert.Equal(1, entry.LaunchCount);
        Assert.Null(entry.CoverPath);
        Assert.NotEqual(oldRomHash, entry.RomHash);
        Assert.False(File.Exists(oldCoverPath));
    }

    [Fact]
    public void RecordOpenedRom_UsesProvidedRomBytesAndHeader()
    {
        using var test = new LibraryTestContext();
        var rom = TestRomFactory.Create(bytes => "MEMORY ROM"u8.CopyTo(bytes.AsSpan(0x0134)));
        var cartridge = TestRomFactory.LoadCartridge(rom);
        var path = Path.Combine(Path.GetDirectoryName(test.DatabasePath)!, "memory.gb");

        test.Library.RecordLoadedRom(path, rom, cartridge.Header);

        var entry = Assert.Single(test.Library.GetRoms(limit: 10));
        Assert.Equal(Path.GetFullPath(path), entry.LastKnownPath);
        Assert.Equal("MEMORY ROM", entry.CartridgeTitle);
    }

    [Fact]
    public void SaveChanges_PreservesExplicitTimestampsWithoutTimeProvider()
    {
        using var test = new LibraryTestContext();
        var addedAt = new DateTimeOffset(2026, 6, 1, 1, 2, 3, TimeSpan.Zero);
        var updatedAt = addedAt.AddMinutes(5);
        var openedAt = addedAt.AddMinutes(10);
        var rom = LibraryRom.Opened(
            "manual",
            Path.Combine(test.DatabasePath, "manual.gb"),
            "manual.gb",
            cartridgeTitle: null,
            CartridgeHardwareKind.GB,
            openedAt
        );
        rom.StampCreated(addedAt);
        rom.StampUpdated(updatedAt);

        using (var db = new TestDbContextFactory(test.DatabasePath).CreateDbContext())
        {
            db.Roms.Add(rom);
            db.SaveChanges();
        }

        using var readDb = new TestDbContextFactory(test.DatabasePath).CreateDbContext();
        var saved = Assert.Single(readDb.Roms);
        Assert.Equal(addedAt, saved.AddedAt);
        Assert.Equal(updatedAt, saved.UpdatedAt);
        Assert.Equal(openedAt, saved.LastOpenedAt);
    }

    [Fact]
    public async Task SaveChangesAsync_StampsAddedAndModifiedRomsWithTimeProvider()
    {
        using var test = new LibraryTestContext();
        var firstOpenedAt = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);
        var secondOpenedAt = firstOpenedAt.AddHours(1);
        var romPath = Path.Combine(test.DatabasePath, "async.gb");
        var createdAt = test.TimeProvider.GetUtcNow();
        var rom = LibraryRom.Opened(
            "async",
            romPath,
            "async.gb",
            cartridgeTitle: null,
            CartridgeHardwareKind.GB,
            firstOpenedAt
        );

        var createDb = new TestDbContextFactory(
            test.DatabasePath,
            test.TimeProvider
        ).CreateDbContext();
        await using (createDb)
        {
            createDb.Roms.Add(rom);
            await createDb.SaveChangesAsync(
                acceptAllChangesOnSuccess: true,
                TestContext.Current.CancellationToken
            );
        }

        test.TimeProvider.Advance(TimeSpan.FromMinutes(2));
        var modifiedAt = test.TimeProvider.GetUtcNow();
        var updateDb = new TestDbContextFactory(
            test.DatabasePath,
            test.TimeProvider
        ).CreateDbContext();
        await using (updateDb)
        {
            var saved = await updateDb
                .Roms.AsTracking()
                .SingleAsync(
                    entry => entry.RomHash == "async",
                    TestContext.Current.CancellationToken
                );
            saved.RecordOpen(
                romPath,
                "async.gb",
                cartridgeTitle: null,
                CartridgeHardwareKind.GB,
                secondOpenedAt
            );
            await updateDb.SaveChangesAsync(
                acceptAllChangesOnSuccess: true,
                TestContext.Current.CancellationToken
            );
        }

        var readDb = new TestDbContextFactory(test.DatabasePath).CreateDbContext();
        await using (readDb)
        {
            var persisted = Assert.Single(readDb.Roms);
            Assert.Equal(createdAt, persisted.AddedAt);
            Assert.Equal(modifiedAt, persisted.UpdatedAt);
            Assert.Equal(secondOpenedAt, persisted.LastOpenedAt);
            Assert.Equal(2, persisted.LaunchCount);
        }
    }

    [Fact]
    public async Task RemoveRomPath_RemovesEntryByLastKnownPathAndAssignedManagedCover()
    {
        using var test = new LibraryTestContext();
        var romPath = await test.WriteRomAsync("game.gb", TestRomFactory.Create());
        await test.Library.RecordOpenedRomAsync(romPath);
        var romHash = Assert.Single(test.Library.GetRoms(limit: 10)).RomHash;
        var sourceImagePath = await test.WriteImageAsync("cover.png", [0x13, 0x14, 0x15]);
        test.Library.AssignCoverImage(romHash, sourceImagePath);
        var coverPath =
            Assert.Single(test.Library.GetRoms(limit: 10)).CoverPath
            ?? throw new InvalidOperationException("Cover path was not stored.");

        Assert.True(File.Exists(coverPath));
        test.Library.RemoveRomPath(romPath);

        Assert.Empty(test.Library.GetRoms(limit: 10));
        Assert.False(File.Exists(coverPath));
    }

    [Fact]
    public void GetRoms_OrdersByUtcTimestampText()
    {
        using var test = new LibraryTestContext();
        InsertLibraryEntry(
            test.DatabasePath,
            "older",
            "older.gb",
            "2026-10-25T00:30:00.0000000+00:00"
        );
        InsertLibraryEntry(
            test.DatabasePath,
            "newer",
            "newer.gb",
            "2026-10-25T01:15:00.0000000+00:00"
        );

        var entries = test.Library.GetRoms(limit: 10);

        Assert.Collection(
            entries,
            entry => Assert.Equal("newer.gb", entry.FileName),
            entry => Assert.Equal("older.gb", entry.FileName)
        );
    }

    [Fact]
    public void GetRoms_SearchTextMatchesCartridgeTitleOrFileName()
    {
        using var test = new LibraryTestContext();
        InsertLibraryEntry(
            test.DatabasePath,
            "title-match",
            "plain.gb",
            "2026-06-27T12:03:00.0000000+00:00",
            cartridgeTitle: "Metroid Fusion"
        );
        InsertLibraryEntry(
            test.DatabasePath,
            "file-match",
            "fusion-file.gb",
            "2026-06-27T12:02:00.0000000+00:00",
            cartridgeTitle: "Puzzle"
        );
        InsertLibraryEntry(
            test.DatabasePath,
            "miss",
            "other.gb",
            "2026-06-27T12:01:00.0000000+00:00",
            cartridgeTitle: "Puzzle"
        );

        var entries = test.Library.GetRoms(new LibraryQuery(SearchText: "fusion"), limit: 10);

        Assert.Collection(
            entries,
            entry => Assert.Equal("plain.gb", entry.FileName),
            entry => Assert.Equal("fusion-file.gb", entry.FileName)
        );
    }

    [Theory]
    [InlineData((int)LibraryHardwareFilter.Gb, "gb.gb")]
    [InlineData((int)LibraryHardwareFilter.Gbc, "gbc.gb")]
    [InlineData((int)LibraryHardwareFilter.Sgb, "sgb.gb")]
    public void GetRoms_HardwareFilterReturnsOnlyMatchingKind(int hardware, string expectedFileName)
    {
        using var test = new LibraryTestContext();
        InsertLibraryEntry(
            test.DatabasePath,
            "gb",
            "gb.gb",
            "2026-06-27T12:03:00.0000000+00:00",
            hardwareKind: CartridgeHardwareKind.GB
        );
        InsertLibraryEntry(
            test.DatabasePath,
            "gbc",
            "gbc.gb",
            "2026-06-27T12:02:00.0000000+00:00",
            hardwareKind: CartridgeHardwareKind.GBC
        );
        InsertLibraryEntry(
            test.DatabasePath,
            "sgb",
            "sgb.gb",
            "2026-06-27T12:01:00.0000000+00:00",
            hardwareKind: CartridgeHardwareKind.SGB
        );

        var entry = Assert.Single(
            test.Library.GetRoms(
                new LibraryQuery(Hardware: (LibraryHardwareFilter)hardware),
                limit: 10
            )
        );

        Assert.Equal(expectedFileName, entry.FileName);
    }

    [Theory]
    [InlineData((int)LibraryCoverFilter.WithCover, "covered.gb")]
    [InlineData((int)LibraryCoverFilter.MissingCover, "missing-cover.gb")]
    public void GetRoms_CoverFilterReturnsOnlyMatchingEntries(int cover, string expectedFileName)
    {
        using var test = new LibraryTestContext();
        InsertLibraryEntry(
            test.DatabasePath,
            "covered",
            "covered.gb",
            "2026-06-27T12:02:00.0000000+00:00",
            coverPath: Path.Combine(test.CoverDirectoryPath, "covered.png")
        );
        InsertLibraryEntry(
            test.DatabasePath,
            "missing-cover",
            "missing-cover.gb",
            "2026-06-27T12:01:00.0000000+00:00"
        );

        var entry = Assert.Single(
            test.Library.GetRoms(new LibraryQuery(Cover: (LibraryCoverFilter)cover), limit: 10)
        );

        Assert.Equal(expectedFileName, entry.FileName);
    }

    [Fact]
    public void GetRoms_TitleSortOrdersByDisplayTitleAscending()
    {
        using var test = new LibraryTestContext();
        InsertSortEntries(test.DatabasePath);

        var entries = test.Library.GetRoms(
            new LibraryQuery(Sort: LibrarySortMode.Title),
            limit: 10
        );

        Assert.Collection(
            entries,
            entry => Assert.Equal("alpha.gb", entry.FileName),
            entry => Assert.Equal("charlie.gb", entry.FileName),
            entry => Assert.Equal("delta.gb", entry.FileName)
        );
    }

    [Fact]
    public void GetRoms_MostPlayedSortOrdersByLaunchCountDescending()
    {
        using var test = new LibraryTestContext();
        InsertSortEntries(test.DatabasePath);

        var entries = test.Library.GetRoms(
            new LibraryQuery(Sort: LibrarySortMode.MostPlayed),
            limit: 10
        );

        Assert.Collection(
            entries,
            entry => Assert.Equal("alpha.gb", entry.FileName),
            entry => Assert.Equal("delta.gb", entry.FileName),
            entry => Assert.Equal("charlie.gb", entry.FileName)
        );
    }

    [Fact]
    public void GetRoms_RecentlyAddedSortOrdersByAddedTimestampDescending()
    {
        using var test = new LibraryTestContext();
        InsertSortEntries(test.DatabasePath);

        var entries = test.Library.GetRoms(
            new LibraryQuery(Sort: LibrarySortMode.RecentlyAdded),
            limit: 10
        );

        Assert.Collection(
            entries,
            entry => Assert.Equal("charlie.gb", entry.FileName),
            entry => Assert.Equal("delta.gb", entry.FileName),
            entry => Assert.Equal("alpha.gb", entry.FileName)
        );
    }

    [Fact]
    public async Task AssignCoverImage_CopiesFileAndStoresCoverPath()
    {
        using var test = new LibraryTestContext();
        var romPath = await test.WriteRomAsync("game.gb", TestRomFactory.Create());
        await test.Library.RecordOpenedRomAsync(romPath);
        var romHash = Assert.Single(test.Library.GetRoms(limit: 10)).RomHash;
        byte[] imageBytes = [0x89, 0x50, 0x4E, 0x47];
        var sourceImagePath = await test.WriteImageAsync("cover.PNG", imageBytes);

        test.Library.AssignCoverImage(romHash, sourceImagePath);

        var entry = Assert.Single(test.Library.GetRoms(limit: 10));
        var coverPath =
            entry.CoverPath ?? throw new InvalidOperationException("Cover path was not stored.");
        Assert.StartsWith(
            test.CoverDirectoryPath + Path.DirectorySeparatorChar,
            coverPath,
            StringComparison.Ordinal
        );
        Assert.EndsWith(".png", coverPath, StringComparison.Ordinal);
        Assert.Equal(
            imageBytes,
            await File.ReadAllBytesAsync(coverPath, TestContext.Current.CancellationToken)
        );
        Assert.Equal(
            [coverPath],
            Directory.GetFiles(test.CoverDirectoryPath, "*", SearchOption.TopDirectoryOnly)
        );
    }

    [Fact]
    public async Task AssignCoverImage_ReplacesManagedCoverAndRemovesPreviousCopy()
    {
        using var test = new LibraryTestContext();
        var romPath = await test.WriteRomAsync("game.gb", TestRomFactory.Create());
        await test.Library.RecordOpenedRomAsync(romPath);
        var romHash = Assert.Single(test.Library.GetRoms(limit: 10)).RomHash;

        var firstSourcePath = await test.WriteImageAsync("first.png", [0x01, 0x02]);
        test.Library.AssignCoverImage(romHash, firstSourcePath);
        var firstCoverPath =
            Assert.Single(test.Library.GetRoms(limit: 10)).CoverPath
            ?? throw new InvalidOperationException("Cover path was not stored.");
        var secondSourcePath = await test.WriteImageAsync("second.png", [0x03, 0x04]);

        test.Library.AssignCoverImage(romHash, secondSourcePath);

        var secondCoverPath =
            Assert.Single(test.Library.GetRoms(limit: 10)).CoverPath
            ?? throw new InvalidOperationException("Cover path was not stored.");
        Assert.NotEqual(firstCoverPath, secondCoverPath);
        Assert.False(File.Exists(firstCoverPath));
        Assert.Equal(
            [secondCoverPath],
            Directory.GetFiles(test.CoverDirectoryPath, "*", SearchOption.TopDirectoryOnly)
        );
    }

    [Theory]
    [InlineData("cover")]
    [InlineData("cover.unsafe!")]
    [InlineData("cover.abcdefghijklmnop")]
    [InlineData("cover.ç")]
    public async Task AssignCoverImage_RejectsUnsafeImageExtension(string imageFileName)
    {
        using var test = new LibraryTestContext();
        var romPath = await test.WriteRomAsync("game.gb", TestRomFactory.Create());
        await test.Library.RecordOpenedRomAsync(romPath);
        var romHash = Assert.Single(test.Library.GetRoms(limit: 10)).RomHash;
        var sourceImagePath = await test.WriteImageAsync(imageFileName, [0x01]);

        Assert.Equal(
            "Cover image file name has no safe extension.",
            Assert
                .Throws<InvalidOperationException>(() =>
                    test.Library.AssignCoverImage(romHash, sourceImagePath)
                )
                .Message
        );
    }

    [Fact]
    public async Task RecordOpenedRomAsync_PreservesCoverPathWhenUpsertingSameRom()
    {
        using var test = new LibraryTestContext();
        var rom = TestRomFactory.Create();
        var firstPath = await test.WriteRomAsync("first.gb", rom);
        await test.Library.RecordOpenedRomAsync(firstPath);
        var romHash = Assert.Single(test.Library.GetRoms(limit: 10)).RomHash;
        var sourceImagePath = await test.WriteImageAsync("cover.png", [0x01, 0x02, 0x03]);
        test.Library.AssignCoverImage(romHash, sourceImagePath);
        var coverPath =
            Assert.Single(test.Library.GetRoms(limit: 10)).CoverPath
            ?? throw new InvalidOperationException("Cover path was not stored.");
        var secondPath = await test.WriteRomAsync("second.gb", rom);

        Assert.Equal(coverPath, await test.Library.RecordOpenedRomAsync(secondPath));

        var entry = Assert.Single(test.Library.GetRoms(limit: 10));
        Assert.Equal(coverPath, entry.CoverPath);
        Assert.True(File.Exists(coverPath));
    }

    [Fact]
    public async Task ClearCover_NullsCoverPathAndRemovesManagedCopy()
    {
        using var test = new LibraryTestContext();
        var romPath = await test.WriteRomAsync("game.gb", TestRomFactory.Create());
        await test.Library.RecordOpenedRomAsync(romPath);
        var romHash = Assert.Single(test.Library.GetRoms(limit: 10)).RomHash;
        var sourceImagePath = await test.WriteImageAsync("cover.png", [0x04, 0x05, 0x06]);
        test.Library.AssignCoverImage(romHash, sourceImagePath);
        var coverPath =
            Assert.Single(test.Library.GetRoms(limit: 10)).CoverPath
            ?? throw new InvalidOperationException("Cover path was not stored.");

        test.Library.ClearCover(romHash);

        Assert.Null(Assert.Single(test.Library.GetRoms(limit: 10)).CoverPath);
        Assert.False(File.Exists(coverPath));
        Assert.Empty(
            Directory.GetFiles(test.CoverDirectoryPath, "*", SearchOption.TopDirectoryOnly)
        );
    }

    [Fact]
    public async Task AssignCoverImage_FailedDatabaseUpdatePreservesPreviousCover()
    {
        using var test = new LibraryTestContext();
        var romPath = await test.WriteRomAsync("game.gb", TestRomFactory.Create());
        await test.Library.RecordOpenedRomAsync(romPath);
        var romHash = Assert.Single(test.Library.GetRoms(limit: 10)).RomHash;
        byte[] oldBytes = [0x10, 0x11, 0x12];
        var oldSourcePath = await test.WriteImageAsync("old.png", oldBytes);
        test.Library.AssignCoverImage(romHash, oldSourcePath);
        var oldCoverPath =
            Assert.Single(test.Library.GetRoms(limit: 10)).CoverPath
            ?? throw new InvalidOperationException("Cover path was not stored.");
        var newSourcePath = await test.WriteImageAsync("new.png", [0x20, 0x21, 0x22]);
        var failingLibrary = new LibraryService(
            new FailingDbContextFactory(test.DatabasePath, test.TimeProvider),
            test.CoverDirectoryPath,
            NullLogger<LibraryService>.Instance,
            test.TimeProvider
        );

        Assert.Equal(
            "Test database failure.",
            Assert
                .Throws<InvalidOperationException>(() =>
                    failingLibrary.AssignCoverImage(romHash, newSourcePath)
                )
                .Message
        );

        Assert.Equal(oldCoverPath, Assert.Single(test.Library.GetRoms(limit: 10)).CoverPath);
        Assert.Equal(
            oldBytes,
            await File.ReadAllBytesAsync(oldCoverPath, TestContext.Current.CancellationToken)
        );
        Assert.Equal(
            [oldCoverPath],
            Directory.GetFiles(test.CoverDirectoryPath, "*", SearchOption.TopDirectoryOnly)
        );
    }

    [Fact]
    public async Task AssignCoverImage_MissingSourcePreservesPreviousCover()
    {
        using var test = new LibraryTestContext();
        var romPath = await test.WriteRomAsync("game.gb", TestRomFactory.Create());
        await test.Library.RecordOpenedRomAsync(romPath);
        var romHash = Assert.Single(test.Library.GetRoms(limit: 10)).RomHash;
        byte[] oldBytes = [0x40, 0x41, 0x42];
        var oldSourcePath = await test.WriteImageAsync("old.png", oldBytes);
        test.Library.AssignCoverImage(romHash, oldSourcePath);
        var oldCoverPath =
            Assert.Single(test.Library.GetRoms(limit: 10)).CoverPath
            ?? throw new InvalidOperationException("Cover path was not stored.");
        var missingSourcePath = Path.Combine(Path.GetDirectoryName(oldSourcePath)!, "missing.png");

        Assert.Throws<InvalidOperationException>(() =>
            test.Library.AssignCoverImage(romHash, missingSourcePath)
        );

        Assert.Equal(oldCoverPath, Assert.Single(test.Library.GetRoms(limit: 10)).CoverPath);
        Assert.Equal(
            oldBytes,
            await File.ReadAllBytesAsync(oldCoverPath, TestContext.Current.CancellationToken)
        );
        Assert.Equal(
            [oldCoverPath],
            Directory.GetFiles(test.CoverDirectoryPath, "*", SearchOption.TopDirectoryOnly)
        );
    }

    [Fact]
    public async Task ClearCover_FailedDatabaseUpdatePreservesPreviousCover()
    {
        using var test = new LibraryTestContext();
        var romPath = await test.WriteRomAsync("game.gb", TestRomFactory.Create());
        await test.Library.RecordOpenedRomAsync(romPath);
        var romHash = Assert.Single(test.Library.GetRoms(limit: 10)).RomHash;
        var sourceImagePath = await test.WriteImageAsync("cover.png", [0x30, 0x31, 0x32]);
        test.Library.AssignCoverImage(romHash, sourceImagePath);
        var coverPath =
            Assert.Single(test.Library.GetRoms(limit: 10)).CoverPath
            ?? throw new InvalidOperationException("Cover path was not stored.");
        var failingLibrary = new LibraryService(
            new FailingDbContextFactory(test.DatabasePath, test.TimeProvider),
            test.CoverDirectoryPath,
            NullLogger<LibraryService>.Instance,
            test.TimeProvider
        );

        Assert.Equal(
            "Test database failure.",
            Assert
                .Throws<InvalidOperationException>(() => failingLibrary.ClearCover(romHash))
                .Message
        );

        Assert.Equal(coverPath, Assert.Single(test.Library.GetRoms(limit: 10)).CoverPath);
        Assert.True(File.Exists(coverPath));
    }

    [Fact]
    public async Task ClearCover_PreservesFileReferencedByAnotherRom()
    {
        using var test = new LibraryTestContext();
        var romPath = await test.WriteRomAsync("game.gb", TestRomFactory.Create());
        await test.Library.RecordOpenedRomAsync(romPath);
        var firstEntry = Assert.Single(test.Library.GetRoms(limit: 10));
        var sourceImagePath = await test.WriteImageAsync("cover.png", [0x50, 0x51, 0x52]);
        test.Library.AssignCoverImage(firstEntry.RomHash, sourceImagePath);
        var coverPath =
            Assert.Single(test.Library.GetRoms(limit: 10)).CoverPath
            ?? throw new InvalidOperationException("Cover path was not stored.");
        InsertLibraryEntry(
            test.DatabasePath,
            "shared-cover-rom",
            "shared.gb",
            "2026-06-27T12:01:00.0000000+00:00",
            coverPath: coverPath
        );

        test.Library.ClearCover(firstEntry.RomHash);

        var entries = test.Library.GetRoms(limit: 10);
        Assert.Null(
            Assert
                .Single(
                    entries,
                    entry =>
                        string.Equals(entry.RomHash, firstEntry.RomHash, StringComparison.Ordinal)
                )
                .CoverPath
        );
        Assert.Equal(
            coverPath,
            Assert
                .Single(
                    entries,
                    entry =>
                        string.Equals(entry.RomHash, "shared-cover-rom", StringComparison.Ordinal)
                )
                .CoverPath
        );
        Assert.True(File.Exists(coverPath));
        Assert.Equal(
            [coverPath],
            Directory.GetFiles(test.CoverDirectoryPath, "*", SearchOption.TopDirectoryOnly)
        );
    }

    [Fact]
    public async Task CoverOperations_MissingRomHashReturnsFailure()
    {
        using var test = new LibraryTestContext();
        var sourceImagePath = await test.WriteImageAsync("cover.png", [0x07, 0x08, 0x09]);

        Assert.Equal(
            "ROM not found: missing",
            Assert
                .Throws<InvalidOperationException>(() =>
                    test.Library.AssignCoverImage("missing", sourceImagePath)
                )
                .Message
        );
        Assert.Equal(
            "ROM not found: missing",
            Assert
                .Throws<InvalidOperationException>(() => test.Library.ClearCover("missing"))
                .Message
        );
    }

    private static string[] GetAppliedMigrations(string databasePath)
    {
        using var db = new TestDbContextFactory(databasePath).CreateDbContext();
        return [.. db.Database.GetAppliedMigrations()];
    }

    private static void InsertLibraryEntry(
        string databasePath,
        string romHash,
        string fileName,
        string lastOpenedAt,
        string? cartridgeTitle = null,
        string? addedAt = null,
        int launchCount = 1,
        string? coverPath = null,
        CartridgeHardwareKind hardwareKind = CartridgeHardwareKind.GB
    )
    {
        var lastOpened = DateTimeOffset.Parse(lastOpenedAt, CultureInfo.InvariantCulture);
        var rom = LibraryRom.Opened(
            romHash,
            Path.Combine(databasePath, fileName),
            fileName,
            cartridgeTitle,
            hardwareKind,
            lastOpened
        );
        var createdAt = DateTimeOffset.Parse(addedAt ?? lastOpenedAt, CultureInfo.InvariantCulture);
        rom.StampCreated(createdAt);
        rom.StampUpdated(createdAt);
        for (var i = 1; i < launchCount; i++)
        {
            rom.RecordOpen(
                Path.Combine(databasePath, fileName),
                fileName,
                cartridgeTitle,
                hardwareKind,
                lastOpened
            );
        }

        rom.SetCoverPath(coverPath);
        using var db = new TestDbContextFactory(databasePath).CreateDbContext();
        db.Roms.Add(rom);
        db.SaveChanges();
    }

    private static void InsertSortEntries(string databasePath)
    {
        InsertLibraryEntry(
            databasePath,
            "delta",
            "delta.gb",
            "2026-06-27T12:03:00.0000000+00:00",
            cartridgeTitle: "Delta",
            addedAt: "2026-06-27T12:02:00.0000000+00:00",
            launchCount: 3
        );
        InsertLibraryEntry(
            databasePath,
            "alpha",
            "alpha.gb",
            "2026-06-27T12:01:00.0000000+00:00",
            cartridgeTitle: "alpha",
            addedAt: "2026-06-27T12:00:00.0000000+00:00",
            launchCount: 5
        );
        InsertLibraryEntry(
            databasePath,
            "charlie",
            "charlie.gb",
            "2026-06-27T12:02:00.0000000+00:00",
            cartridgeTitle: "Charlie",
            addedAt: "2026-06-27T12:04:00.0000000+00:00",
            launchCount: 1
        );
    }

    private sealed class LibraryTestContext : IDisposable
    {
        public LibraryTestContext()
        {
            Directory.CreateDirectory(DirectoryPath);
            var dbContextFactory = new TestDbContextFactory(DatabasePath, TimeProvider);
            using var db = dbContextFactory.CreateDbContext();
            db.Database.Migrate();
            Library = new LibraryService(
                dbContextFactory,
                CoverDirectoryPath,
                NullLogger<LibraryService>.Instance,
                TimeProvider
            );
        }

        private TestDirectories.TemporaryDirectory TemporaryDirectory { get; } =
            TestDirectories.CreateTemporaryDirectory();

        private string DirectoryPath => TemporaryDirectory.Path;

        public string DatabasePath => Path.Combine(DirectoryPath, "gbcnet.sqlite");

        public string CoverDirectoryPath => Path.Combine(DirectoryPath, "covers");

        public TestTimeProvider TimeProvider { get; } =
            new(new DateTimeOffset(2026, 6, 27, 12, 0, 0, TimeSpan.Zero));

        public LibraryService Library { get; }

        public async Task<string> WriteRomAsync(string fileName, byte[] rom)
        {
            var path = Path.Combine(DirectoryPath, fileName);
            await File.WriteAllBytesAsync(path, rom, TestContext.Current.CancellationToken)
                .ConfigureAwait(false);
            return path;
        }

        public async Task<string> WriteImageAsync(string fileName, byte[] image)
        {
            var path = Path.Combine(DirectoryPath, fileName);
            await File.WriteAllBytesAsync(path, image, TestContext.Current.CancellationToken)
                .ConfigureAwait(false);
            return path;
        }

        public void Dispose() => TemporaryDirectory.Dispose();
    }

    private sealed class TestDbContextFactory(
        string databasePath,
        TimeProvider? timeProvider = null
    ) : IDbContextFactory<GbcNetDbContext>
    {
        private readonly DbContextOptions<GbcNetDbContext> _options =
            new DbContextOptionsBuilder<GbcNetDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;

        public GbcNetDbContext CreateDbContext() => new(_options, timeProvider);
    }

    private sealed class FailingDbContextFactory(
        string databasePath,
        TimeProvider? timeProvider = null
    ) : IDbContextFactory<GbcNetDbContext>
    {
        private readonly DbContextOptions<GbcNetDbContext> _options =
            new DbContextOptionsBuilder<GbcNetDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .AddInterceptors(FailingSaveChangesInterceptor.Instance)
                .Options;

        public GbcNetDbContext CreateDbContext() => new(_options, timeProvider);
    }

    private sealed class FailingSaveChangesInterceptor : SaveChangesInterceptor
    {
        public static FailingSaveChangesInterceptor Instance { get; } = new();

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result
        ) => throw new InvalidOperationException("Test database failure.");
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private static readonly TimeZoneInfo _localZone = TimeZoneInfo.CreateCustomTimeZone(
            "Test Local",
            TimeSpan.FromHours(2),
            "Test Local",
            "Test Local"
        );

        private DateTimeOffset _utcNow = utcNow;

        public void Advance(TimeSpan elapsed) => _utcNow += elapsed;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public override TimeZoneInfo LocalTimeZone => _localZone;
    }
}
