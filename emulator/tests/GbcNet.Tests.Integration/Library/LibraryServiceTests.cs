// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using ErrorOr;
using GbcNet.App.Database.Entities;
using GbcNet.App.Library;
using GbcNet.App.Saves;
using GbcNet.App.Sorting;
using GbcNet.Core.Cartridges;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GbcNet.Tests.Integration.Library;

public sealed class LibraryServiceTests
{
    [Fact]
    public async Task RecordLoadedRom_UpsertsByRomHashAndUpdatesLastKnownPath()
    {
        using var test = new LibraryTestContext();
        var rom = TestRomFactory.Create();
        var firstPath = await test.WriteRomAsync("first.gb", rom);
        var secondPath = await test.WriteRomAsync("second.gb", rom);

        await test.RecordRomFromFileAsync(firstPath);
        test.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        await test.RecordRomFromFileAsync(secondPath);

        var entry = test.Library.GetRoms(limit: 10).Should().ContainSingle().Which;
        entry.LastKnownPath.Should().Be(Path.GetFullPath(secondPath));
        entry.FileName.Should().Be("second.gb");
        entry.CartridgeTitle.Should().Be("TEST ROM");
        entry.LaunchCount.Should().Be(2);
        entry.AddedAt.Should().Be(new DateTimeOffset(2026, 6, 27, 12, 0, 0, TimeSpan.Zero));
        entry.LastOpenedAt.Should().Be(new DateTimeOffset(2026, 6, 27, 12, 1, 0, TimeSpan.Zero));
        entry.CoverPath.Should().BeNull();
    }

    [Fact]
    public async Task RecordLoadedRom_ReplacesPreviousHashForSamePathAndRemovesManagedCover()
    {
        using var test = new LibraryTestContext();
        var romPath = await test.WriteRomAsync("game.gb", TestRomFactory.Create());
        await test.RecordRomFromFileAsync(romPath);
        var oldRomHash = test.Library.GetRoms(limit: 10).Should().ContainSingle().Which.RomHash;
        var sourceImagePath = await test.WriteImageAsync("old-cover.png", [0x10, 0x11, 0x12]);
        test.Library.AssignCoverImage(oldRomHash, sourceImagePath);
        var oldCoverPath =
            test.Library.GetRoms(limit: 10).Should().ContainSingle().Which.CoverPath
            ?? throw new InvalidOperationException("Cover path was not stored.");
        await test.WriteRomAsync(
            "game.gb",
            TestRomFactory.Create(bytes => "SECOND ROM"u8.CopyTo(bytes.AsSpan(0x0134)))
        );

        File.Exists(oldCoverPath).Should().BeTrue();
        await test.RecordRomFromFileAsync(romPath);

        var entry = test.Library.GetRoms(limit: 10).Should().ContainSingle().Which;
        entry.LastKnownPath.Should().Be(Path.GetFullPath(romPath));
        entry.CartridgeTitle.Should().Be("SECOND ROM");
        entry.LaunchCount.Should().Be(1);
        entry.CoverPath.Should().BeNull();
        entry.RomHash.Should().NotBe(oldRomHash);
        File.Exists(oldCoverPath).Should().BeFalse();
    }

    [Fact]
    public void RecordLoadedRom_UsesProvidedRomBytesAndHeader()
    {
        using var test = new LibraryTestContext();
        var rom = TestRomFactory.Create(bytes => "MEMORY ROM"u8.CopyTo(bytes.AsSpan(0x0134)));
        var cartridge = TestRomFactory.LoadCartridge(rom);
        var identity = RomStorageIdentity.Create(cartridge.Header.Title, rom);
        var path = Path.Combine(Path.GetDirectoryName(test.DatabasePath)!, "memory.gb");

        test.Library.RecordLoadedRom(path, identity.HashHex, rom, cartridge.Header);

        var entry = test.Library.GetRoms(limit: 10).Should().ContainSingle().Which;
        entry.RomHash.Should().Be(identity.HashHex);
        entry.LastKnownPath.Should().Be(Path.GetFullPath(path));
        entry.CartridgeTitle.Should().Be("MEMORY ROM");
    }

    [Fact]
    public void RecordPlayTime_AccumulatesDurationForLoadedRom()
    {
        using var test = new LibraryTestContext();
        var rom = TestRomFactory.Create();
        var cartridge = TestRomFactory.LoadCartridge(rom);
        var identity = RomStorageIdentity.Create(cartridge.Header.Title, rom);
        var path = Path.Combine(Path.GetDirectoryName(test.DatabasePath)!, "time.gb");

        test.Library.RecordLoadedRom(path, identity.HashHex, rom, cartridge.Header);
        test.Library.RecordPlayTime(
            identity.HashHex,
            TimeSpan.FromHours(2) + TimeSpan.FromMinutes(3)
        );
        test.Library.RecordPlayTime(identity.HashHex, TimeSpan.FromSeconds(45));

        var entry = test.Library.GetRoms(limit: 10).Should().ContainSingle().Which;
        entry
            .PlayTime.Should()
            .Be(TimeSpan.FromHours(2) + TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(45));
    }

    [Fact]
    public void NoIntroCatalog_ReturnsCanonicalTitleAndRegions()
    {
        var metadata = NoIntroCatalog.Get("DD6E952B730C4BD85F8734156D43A2616B68C053");

        metadata.Should().NotBeNull();
        metadata.Title.Should().Be("007 - The World Is Not Enough");
        metadata.Regions.Should().Be(NoIntroRegion.Usa | NoIntroRegion.Europe);
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
            "0000000000000000000000000000000000000000",
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
        var saved = readDb.Roms.Should().ContainSingle().Which;
        saved.AddedAt.Should().Be(addedAt);
        saved.UpdatedAt.Should().Be(updatedAt);
        saved.LastOpenedAt.Should().Be(openedAt);
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
            "0000000000000000000000000000000000000000",
            firstOpenedAt
        );

        var createDb = new TestDbContextFactory(
            test.DatabasePath,
            timeProvider: test.TimeProvider
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
            timeProvider: test.TimeProvider
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
                "0000000000000000000000000000000000000000",
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
            var persisted = readDb.Roms.Should().ContainSingle().Which;
            persisted.AddedAt.Should().Be(createdAt);
            persisted.UpdatedAt.Should().Be(modifiedAt);
            persisted.LastOpenedAt.Should().Be(secondOpenedAt);
            persisted.LaunchCount.Should().Be(2);
        }
    }

    [Fact]
    public async Task RemoveRomPath_RemovesEntryByLastKnownPathAndAssignedManagedCover()
    {
        using var test = new LibraryTestContext();
        var romPath = await test.WriteRomAsync("game.gb", TestRomFactory.Create());
        await test.RecordRomFromFileAsync(romPath);
        var romHash = test.Library.GetRoms(limit: 10).Should().ContainSingle().Which.RomHash;
        var sourceImagePath = await test.WriteImageAsync("cover.png", [0x13, 0x14, 0x15]);
        test.Library.AssignCoverImage(romHash, sourceImagePath);
        var coverPath =
            test.Library.GetRoms(limit: 10).Should().ContainSingle().Which.CoverPath
            ?? throw new InvalidOperationException("Cover path was not stored.");

        File.Exists(coverPath).Should().BeTrue();
        test.Library.RemoveRomPath(romPath);

        test.Library.GetRoms(limit: 10).Should().BeEmpty();
        File.Exists(coverPath).Should().BeFalse();
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

        entries
            .Should()
            .SatisfyRespectively(
                entry => entry.FileName.Should().Be("newer.gb"),
                entry => entry.FileName.Should().Be("older.gb")
            );
    }

    [Fact]
    public void GetRoms_DbOnlyQueryUsesOrdinalIgnoreCaseFileNameTieBreakerBeforeLimit()
    {
        using var test = new LibraryTestContext();
        InsertLibraryEntry(
            test.DatabasePath,
            "upper-b",
            "B.gb",
            "2026-06-27T12:00:00.0000000+00:00"
        );
        InsertLibraryEntry(
            test.DatabasePath,
            "lower-a",
            "a.gb",
            "2026-06-27T12:00:00.0000000+00:00"
        );

        var entry = test.Library.GetRoms(limit: 1).Should().ContainSingle().Which;

        entry.FileName.Should().Be("a.gb");
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

        entries
            .Should()
            .SatisfyRespectively(
                entry => entry.FileName.Should().Be("plain.gb"),
                entry => entry.FileName.Should().Be("fusion-file.gb")
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

        var entry = test
            .Library.GetRoms(new LibraryQuery(Hardware: (LibraryHardwareFilter)hardware), limit: 10)
            .Should()
            .ContainSingle()
            .Which;

        entry.FileName.Should().Be(expectedFileName);
    }

    [Theory]
    [InlineData((int)LibraryRegionFilter.Japan, "japan.gb")]
    [InlineData((int)LibraryRegionFilter.Usa, "usa-europe.gb")]
    [InlineData((int)LibraryRegionFilter.Other, "france.gb")]
    public void GetRoms_RegionFilterReturnsOnlyMatchingEntries(
        int regionFilter,
        string expectedFileName
    )
    {
        using var test = new LibraryTestContext();
        InsertLibraryEntry(
            test.DatabasePath,
            "japan",
            "japan.gb",
            "2026-06-27T12:03:00.0000000+00:00",
            noIntroHash: "00369C42D2C4BE0506901B64F7D5424538574CE0"
        );
        InsertLibraryEntry(
            test.DatabasePath,
            "usa-europe",
            "usa-europe.gb",
            "2026-06-27T12:02:00.0000000+00:00",
            noIntroHash: "00D76805E1EF3FE0EB5E8FC045CC22DECFBE216B"
        );
        InsertLibraryEntry(
            test.DatabasePath,
            "france",
            "france.gb",
            "2026-06-27T12:01:00.0000000+00:00",
            noIntroHash: "07A0E1C0DDDE6371DBAF25FD016BDC77C0ECA090"
        );

        var entry = test
            .Library.GetRoms(new LibraryQuery(Region: (LibraryRegionFilter)regionFilter), limit: 10)
            .Should()
            .ContainSingle()
            .Which;

        entry.FileName.Should().Be(expectedFileName);
    }

    [Fact]
    public void GetRoms_TitleSortOrdersByDisplayTitleAscending()
    {
        using var test = new LibraryTestContext();
        InsertSortEntries(test.DatabasePath);

        var entries = test.Library.GetRoms(
            new LibraryQuery(Sort: LibrarySortField.Title),
            limit: 10
        );

        entries
            .Should()
            .SatisfyRespectively(
                entry => entry.FileName.Should().Be("alpha.gb"),
                entry => entry.FileName.Should().Be("charlie.gb"),
                entry => entry.FileName.Should().Be("delta.gb")
            );
    }

    [Fact]
    public void GetRoms_TitleSortOrdersByDisplayTitleDescendingWhenExplicit()
    {
        using var test = new LibraryTestContext();
        InsertSortEntries(test.DatabasePath);

        var entries = test.Library.GetRoms(
            new LibraryQuery(Sort: LibrarySortField.Title, Direction: SortDirection.Descending),
            limit: 10
        );

        entries
            .Should()
            .SatisfyRespectively(
                entry => entry.FileName.Should().Be("delta.gb"),
                entry => entry.FileName.Should().Be("charlie.gb"),
                entry => entry.FileName.Should().Be("alpha.gb")
            );
    }

    [Fact]
    public void GetRoms_MostPlayedSortOrdersByLaunchCountDescending()
    {
        using var test = new LibraryTestContext();
        InsertSortEntries(test.DatabasePath);

        var entries = test.Library.GetRoms(
            new LibraryQuery(Sort: LibrarySortField.MostPlayed),
            limit: 10
        );

        entries
            .Should()
            .SatisfyRespectively(
                entry => entry.FileName.Should().Be("alpha.gb"),
                entry => entry.FileName.Should().Be("delta.gb"),
                entry => entry.FileName.Should().Be("charlie.gb")
            );
    }

    [Fact]
    public void GetRoms_RecentlyAddedSortOrdersByAddedTimestampDescending()
    {
        using var test = new LibraryTestContext();
        InsertSortEntries(test.DatabasePath);

        var entries = test.Library.GetRoms(
            new LibraryQuery(Sort: LibrarySortField.RecentlyAdded),
            limit: 10
        );

        entries
            .Should()
            .SatisfyRespectively(
                entry => entry.FileName.Should().Be("charlie.gb"),
                entry => entry.FileName.Should().Be("delta.gb"),
                entry => entry.FileName.Should().Be("alpha.gb")
            );
    }

    [Fact]
    public void GetRoms_MostTimePlayedSortOrdersByPlayTimeDescending()
    {
        using var test = new LibraryTestContext();
        InsertSortEntries(test.DatabasePath);

        var entries = test.Library.GetRoms(
            new LibraryQuery(Sort: LibrarySortField.MostTimePlayed),
            limit: 10
        );

        entries
            .Should()
            .SatisfyRespectively(
                entry => entry.FileName.Should().Be("alpha.gb"),
                entry => entry.FileName.Should().Be("delta.gb"),
                entry => entry.FileName.Should().Be("charlie.gb")
            );
    }

    [Fact]
    public async Task AssignCoverImage_CopiesFileAndStoresCoverPath()
    {
        using var test = new LibraryTestContext();
        var romPath = await test.WriteRomAsync("game.gb", TestRomFactory.Create());
        await test.RecordRomFromFileAsync(romPath);
        var romHash = test.Library.GetRoms(limit: 10).Should().ContainSingle().Which.RomHash;
        byte[] imageBytes = [0x89, 0x50, 0x4E, 0x47];
        var sourceImagePath = await test.WriteImageAsync("cover.PNG", imageBytes);

        test.Library.AssignCoverImage(romHash, sourceImagePath);

        var entry = test.Library.GetRoms(limit: 10).Should().ContainSingle().Which;
        var coverPath =
            entry.CoverPath ?? throw new InvalidOperationException("Cover path was not stored.");
        coverPath.Should().StartWith(test.CoverDirectoryPath + Path.DirectorySeparatorChar);
        coverPath.Should().EndWith(".png");
        (await File.ReadAllBytesAsync(coverPath, TestContext.Current.CancellationToken))
            .Should()
            .Equal(imageBytes);
        Directory
            .GetFiles(test.CoverDirectoryPath, "*", SearchOption.TopDirectoryOnly)
            .Should()
            .Equal(coverPath);
    }

    [Fact]
    public async Task AssignCoverImage_ReplacesManagedCoverAndRemovesPreviousCopy()
    {
        using var test = new LibraryTestContext();
        var romPath = await test.WriteRomAsync("game.gb", TestRomFactory.Create());
        await test.RecordRomFromFileAsync(romPath);
        var romHash = test.Library.GetRoms(limit: 10).Should().ContainSingle().Which.RomHash;

        var firstSourcePath = await test.WriteImageAsync("first.png", [0x01, 0x02]);
        test.Library.AssignCoverImage(romHash, firstSourcePath);
        var firstCoverPath =
            test.Library.GetRoms(limit: 10).Should().ContainSingle().Which.CoverPath
            ?? throw new InvalidOperationException("Cover path was not stored.");
        var secondSourcePath = await test.WriteImageAsync("second.png", [0x03, 0x04]);

        test.Library.AssignCoverImage(romHash, secondSourcePath);

        var secondCoverPath =
            test.Library.GetRoms(limit: 10).Should().ContainSingle().Which.CoverPath
            ?? throw new InvalidOperationException("Cover path was not stored.");
        secondCoverPath.Should().NotBe(firstCoverPath);
        File.Exists(firstCoverPath).Should().BeFalse();
        Directory
            .GetFiles(test.CoverDirectoryPath, "*", SearchOption.TopDirectoryOnly)
            .Should()
            .Equal(secondCoverPath);
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
        await test.RecordRomFromFileAsync(romPath);
        var romHash = test.Library.GetRoms(limit: 10).Should().ContainSingle().Which.RomHash;
        var sourceImagePath = await test.WriteImageAsync(imageFileName, [0x01]);

        var result = test.Library.AssignCoverImage(romHash, sourceImagePath);

        result.IsError.Should().BeTrue();
        var error = result.Errors.Should().ContainSingle().Which;
        error.Type.Should().Be(ErrorType.Validation);
        error.Code.Should().Be(LibraryService.UnsupportedCoverErrorCode);
    }

    [Fact]
    public async Task RecordLoadedRom_PreservesCoverPathWhenUpsertingSameRom()
    {
        using var test = new LibraryTestContext();
        var rom = TestRomFactory.Create();
        var firstPath = await test.WriteRomAsync("first.gb", rom);
        await test.RecordRomFromFileAsync(firstPath);
        var romHash = test.Library.GetRoms(limit: 10).Should().ContainSingle().Which.RomHash;
        var sourceImagePath = await test.WriteImageAsync("cover.png", [0x01, 0x02, 0x03]);
        test.Library.AssignCoverImage(romHash, sourceImagePath);
        var coverPath =
            test.Library.GetRoms(limit: 10).Should().ContainSingle().Which.CoverPath
            ?? throw new InvalidOperationException("Cover path was not stored.");
        var secondPath = await test.WriteRomAsync("second.gb", rom);

        (await test.RecordRomFromFileAsync(secondPath)).Should().Be(coverPath);

        var entry = test.Library.GetRoms(limit: 10).Should().ContainSingle().Which;
        entry.CoverPath.Should().Be(coverPath);
        File.Exists(coverPath).Should().BeTrue();
    }

    [Fact]
    public async Task ClearCover_NullsCoverPathAndRemovesManagedCopy()
    {
        using var test = new LibraryTestContext();
        var romPath = await test.WriteRomAsync("game.gb", TestRomFactory.Create());
        await test.RecordRomFromFileAsync(romPath);
        var romHash = test.Library.GetRoms(limit: 10).Should().ContainSingle().Which.RomHash;
        var sourceImagePath = await test.WriteImageAsync("cover.png", [0x04, 0x05, 0x06]);
        test.Library.AssignCoverImage(romHash, sourceImagePath);
        var coverPath =
            test.Library.GetRoms(limit: 10).Should().ContainSingle().Which.CoverPath
            ?? throw new InvalidOperationException("Cover path was not stored.");

        var result = test.Library.ClearCover(romHash);

        result.IsError.Should().BeFalse();
        test.Library.GetRoms(limit: 10).Should().ContainSingle().Which.CoverPath.Should().BeNull();
        File.Exists(coverPath).Should().BeFalse();
        Directory
            .GetFiles(test.CoverDirectoryPath, "*", SearchOption.TopDirectoryOnly)
            .Should()
            .BeEmpty();
    }

    [Fact]
    public async Task AssignCoverImage_FailedDatabaseUpdatePreservesPreviousCover()
    {
        using var test = new LibraryTestContext();
        var romPath = await test.WriteRomAsync("game.gb", TestRomFactory.Create());
        await test.RecordRomFromFileAsync(romPath);
        var romHash = test.Library.GetRoms(limit: 10).Should().ContainSingle().Which.RomHash;
        byte[] oldBytes = [0x10, 0x11, 0x12];
        var oldSourcePath = await test.WriteImageAsync("old.png", oldBytes);
        test.Library.AssignCoverImage(romHash, oldSourcePath);
        var oldCoverPath =
            test.Library.GetRoms(limit: 10).Should().ContainSingle().Which.CoverPath
            ?? throw new InvalidOperationException("Cover path was not stored.");
        var newSourcePath = await test.WriteImageAsync("new.png", [.. " !\""u8]);
        var databaseFailure = new DbUpdateException("Synthetic database failure.");
        var failingLibrary = new LibraryService(
            new TestDbContextFactory(
                test.DatabasePath,
                new FailingSaveChangesInterceptor(databaseFailure),
                timeProvider: test.TimeProvider
            ),
            test.CoverDirectoryPath,
            NullLogger<LibraryService>.Instance,
            test.TimeProvider
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            failingLibrary.AssignCoverImage(romHash, newSourcePath)
        );

        exception.InnerException.Should().BeSameAs(databaseFailure);
        test.Library.GetRoms(limit: 10)
            .Should()
            .ContainSingle()
            .Which.CoverPath.Should()
            .Be(oldCoverPath);
        (await File.ReadAllBytesAsync(oldCoverPath, TestContext.Current.CancellationToken))
            .Should()
            .Equal(oldBytes);
        Directory
            .GetFiles(test.CoverDirectoryPath, "*", SearchOption.TopDirectoryOnly)
            .Should()
            .Equal(oldCoverPath);
    }

    [Fact]
    public void GetRoms_WhenSqliteCannotOpenDatabasePreservesProviderCause()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var library = new LibraryService(
            new TestDbContextFactory(tempDirectory.Path),
            Path.Combine(tempDirectory.Path, "covers"),
            NullLogger<LibraryService>.Instance
        );

        var exception = FluentActions
            .Invoking(() => library.GetRoms(limit: 10))
            .Should()
            .ThrowExactly<InvalidOperationException>()
            .Which;

        exception.InnerException.Should().BeOfType<SqliteException>();
    }

    [Fact]
    public async Task AssignCoverImage_MissingSourcePreservesPreviousCover()
    {
        using var test = new LibraryTestContext();
        var library = test.Library;
        var romPath = await test.WriteRomAsync("game.gb", TestRomFactory.Create());
        await test.RecordRomFromFileAsync(romPath);
        var romHash = test.Library.GetRoms(limit: 10).Should().ContainSingle().Which.RomHash;
        var oldBytes = "@AB"u8.ToArray();
        var oldSourcePath = await test.WriteImageAsync("old.png", oldBytes);
        test.Library.AssignCoverImage(romHash, oldSourcePath);
        var oldCoverPath =
            test.Library.GetRoms(limit: 10).Should().ContainSingle().Which.CoverPath
            ?? throw new InvalidOperationException("Cover path was not stored.");
        var missingSourcePath = Path.Combine(Path.GetDirectoryName(oldSourcePath)!, "missing.png");

        var result = library.AssignCoverImage(romHash, missingSourcePath);

        result.IsError.Should().BeTrue();
        var error = result.Errors.Should().ContainSingle().Which;
        error.Type.Should().Be(ErrorType.NotFound);
        error.Code.Should().Be(LibraryService.CoverSourceNotFoundErrorCode);
        test.Library.GetRoms(limit: 10)
            .Should()
            .ContainSingle()
            .Which.CoverPath.Should()
            .Be(oldCoverPath);
        (await File.ReadAllBytesAsync(oldCoverPath, TestContext.Current.CancellationToken))
            .Should()
            .Equal(oldBytes);
        Directory
            .GetFiles(test.CoverDirectoryPath, "*", SearchOption.TopDirectoryOnly)
            .Should()
            .Equal(oldCoverPath);
    }

    [Fact]
    public async Task ClearCover_DatabaseConcurrencyFailureRemainsExceptionAndPreservesCover()
    {
        using var test = new LibraryTestContext();
        var romPath = await test.WriteRomAsync("game.gb", TestRomFactory.Create());
        await test.RecordRomFromFileAsync(romPath);
        var romHash = test.Library.GetRoms(limit: 10).Should().ContainSingle().Which.RomHash;
        var sourceImagePath = await test.WriteImageAsync("cover.png", [.. "012"u8]);
        test.Library.AssignCoverImage(romHash, sourceImagePath);
        var coverPath =
            test.Library.GetRoms(limit: 10).Should().ContainSingle().Which.CoverPath
            ?? throw new InvalidOperationException("Cover path was not stored.");
        var databaseFailure = new DbUpdateConcurrencyException();
        var failingLibrary = new LibraryService(
            new TestDbContextFactory(
                test.DatabasePath,
                new FailingSaveChangesInterceptor(databaseFailure),
                timeProvider: test.TimeProvider
            ),
            test.CoverDirectoryPath,
            NullLogger<LibraryService>.Instance,
            test.TimeProvider
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            failingLibrary.ClearCover(romHash)
        );

        exception.InnerException.Should().BeSameAs(databaseFailure);
        test.Library.GetRoms(limit: 10)
            .Should()
            .ContainSingle()
            .Which.CoverPath.Should()
            .Be(coverPath);
        File.Exists(coverPath).Should().BeTrue();
    }

    [Fact]
    public async Task ClearCover_PreservesFileReferencedByAnotherRom()
    {
        using var test = new LibraryTestContext();
        var romPath = await test.WriteRomAsync("game.gb", TestRomFactory.Create());
        await test.RecordRomFromFileAsync(romPath);
        var firstEntry = test.Library.GetRoms(limit: 10).Should().ContainSingle().Which;
        var sourceImagePath = await test.WriteImageAsync("cover.png", [.. "PQR"u8]);
        test.Library.AssignCoverImage(firstEntry.RomHash, sourceImagePath);
        var coverPath =
            test.Library.GetRoms(limit: 10).Should().ContainSingle().Which.CoverPath
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

        Assert
            .Single(
                entries,
                entry => string.Equals(entry.RomHash, firstEntry.RomHash, StringComparison.Ordinal)
            )
            .CoverPath.Should()
            .BeNull();
        Assert
            .Single(
                entries,
                entry => string.Equals(entry.RomHash, "shared-cover-rom", StringComparison.Ordinal)
            )
            .CoverPath.Should()
            .Be(coverPath);
        File.Exists(coverPath).Should().BeTrue();
        Directory
            .GetFiles(test.CoverDirectoryPath, "*", SearchOption.TopDirectoryOnly)
            .Should()
            .Equal(coverPath);
    }

    [Fact]
    public async Task CoverOperations_MissingRomHashReturnNotFound()
    {
        using var test = new LibraryTestContext();
        var sourceImagePath = await test.WriteImageAsync("cover.png", [0x07, 0x08, 0x09]);

        var assignResult = test.Library.AssignCoverImage("missing", sourceImagePath);

        assignResult.IsError.Should().BeTrue();
        var assignError = assignResult.Errors.Should().ContainSingle().Which;
        assignError.Type.Should().Be(ErrorType.NotFound);
        assignError.Code.Should().Be(LibraryService.RomNotFoundErrorCode);
        Directory
            .GetFiles(test.CoverDirectoryPath, "*", SearchOption.TopDirectoryOnly)
            .Should()
            .BeEmpty();
        var clearResult = test.Library.ClearCover("missing");
        clearResult.IsError.Should().BeTrue();
        var clearError = clearResult.Errors.Should().ContainSingle().Which;
        clearError.Type.Should().Be(ErrorType.NotFound);
        clearError.Code.Should().Be(LibraryService.RomNotFoundErrorCode);
    }

    private static void InsertLibraryEntry(
        string databasePath,
        string romHash,
        string fileName,
        string lastOpenedAt,
        string? cartridgeTitle = null,
        string? addedAt = null,
        int launchCount = 1,
        TimeSpan? playTime = null,
        string? coverPath = null,
        CartridgeHardwareKind hardwareKind = CartridgeHardwareKind.GB,
        string noIntroHash = "0000000000000000000000000000000000000000"
    )
    {
        var lastOpened = DateTimeOffset.Parse(lastOpenedAt, CultureInfo.InvariantCulture);
        var rom = LibraryRom.Opened(
            romHash,
            Path.Combine(databasePath, fileName),
            fileName,
            cartridgeTitle,
            hardwareKind,
            noIntroHash,
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
                noIntroHash,
                lastOpened
            );
        }

        if (playTime is not null)
        {
            rom.AddPlayTime(playTime.Value);
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
            launchCount: 3,
            playTime: TimeSpan.FromHours(2)
        );
        InsertLibraryEntry(
            databasePath,
            "alpha",
            "alpha.gb",
            "2026-06-27T12:01:00.0000000+00:00",
            cartridgeTitle: "alpha",
            addedAt: "2026-06-27T12:00:00.0000000+00:00",
            launchCount: 5,
            playTime: TimeSpan.FromHours(4)
        );
        InsertLibraryEntry(
            databasePath,
            "charlie",
            "charlie.gb",
            "2026-06-27T12:02:00.0000000+00:00",
            cartridgeTitle: "Charlie",
            addedAt: "2026-06-27T12:04:00.0000000+00:00",
            launchCount: 1,
            playTime: TimeSpan.FromHours(1)
        );
    }

    private sealed class LibraryTestContext : IDisposable
    {
        public LibraryTestContext()
        {
            Directory.CreateDirectory(DirectoryPath);
            var dbContextFactory = new TestDbContextFactory(
                DatabasePath,
                timeProvider: TimeProvider
            );
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
            await File.WriteAllBytesAsync(path, rom, TestContext.Current.CancellationToken);
            return path;
        }

        public async Task<string?> RecordRomFromFileAsync(string path)
        {
            var rom = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
            var cartridge = Cartridge.LoadOrThrow(rom);
            var identity = RomStorageIdentity.Create(cartridge.Header.Title, rom);
            return Library.RecordLoadedRom(path, identity.HashHex, rom, cartridge.Header);
        }

        public async Task<string> WriteImageAsync(string fileName, byte[] image)
        {
            var path = Path.Combine(DirectoryPath, fileName);
            await File.WriteAllBytesAsync(path, image, TestContext.Current.CancellationToken);
            return path;
        }

        public void Dispose() => TemporaryDirectory.Dispose();
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
