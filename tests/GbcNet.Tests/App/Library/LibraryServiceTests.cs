// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using GbcNet.App.Library;
using GbcNet.Core.Cartridges;
using GbcNet.Tests.Cartridges;
using Microsoft.Data.Sqlite;

namespace GbcNet.Tests.App.Library;

public sealed class LibraryServiceTests
{
    [Fact]
    public async Task RecordOpenedRomAsync_AppliesLatestSchemaMigration()
    {
        using var test = new LibraryTestContext();
        var romPath = await test.WriteRomAsync("game.gb", TestRomFactory.Create());

        ResultAssertions.AssertSuccess(await test.Library.RecordOpenedRomAsync(romPath));

        Assert.Equal(2, GetAppliedMigrationVersion(test.DatabasePath));
    }

    [Fact]
    public async Task RecordOpenedRomAsync_UpsertsByRomHashAndUpdatesLastKnownPath()
    {
        using var test = new LibraryTestContext();
        var rom = TestRomFactory.Create();
        var firstPath = await test.WriteRomAsync("first.gb", rom);
        var secondPath = await test.WriteRomAsync("second.gb", rom);

        ResultAssertions.AssertSuccess(await test.Library.RecordOpenedRomAsync(firstPath));
        test.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        ResultAssertions.AssertSuccess(await test.Library.RecordOpenedRomAsync(secondPath));

        var entry = Assert.Single(ResultAssertions.AssertSuccess(test.Library.GetRoms(limit: 10)));
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
        ResultAssertions.AssertSuccess(await test.Library.RecordOpenedRomAsync(romPath));
        var oldRomHash = Assert
            .Single(ResultAssertions.AssertSuccess(test.Library.GetRoms(limit: 10)))
            .RomHash;
        var sourceImagePath = await test.WriteImageAsync("old-cover.png", [0x10, 0x11, 0x12]);
        ResultAssertions.AssertSuccess(test.Library.AssignCoverImage(oldRomHash, sourceImagePath));
        var oldCoverPath =
            Assert.Single(ResultAssertions.AssertSuccess(test.Library.GetRoms(limit: 10))).CoverPath
            ?? throw new InvalidOperationException("Cover path was not stored.");
        await test.WriteRomAsync(
            "game.gb",
            TestRomFactory.Create(bytes => "SECOND ROM"u8.CopyTo(bytes.AsSpan(0x0134)))
        );

        Assert.True(File.Exists(oldCoverPath));
        ResultAssertions.AssertSuccess(await test.Library.RecordOpenedRomAsync(romPath));

        var entry = Assert.Single(ResultAssertions.AssertSuccess(test.Library.GetRoms(limit: 10)));
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
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));
        var path = Path.Combine(Path.GetDirectoryName(test.DatabasePath)!, "memory.gb");

        ResultAssertions.AssertSuccess(test.Library.RecordLoadedRom(path, rom, cartridge.Header));

        var entry = Assert.Single(ResultAssertions.AssertSuccess(test.Library.GetRoms(limit: 10)));
        Assert.Equal(Path.GetFullPath(path), entry.LastKnownPath);
        Assert.Equal("MEMORY ROM", entry.CartridgeTitle);
    }

    [Fact]
    public async Task RemoveRomPath_RemovesEntryByLastKnownPathAndAssignedManagedCover()
    {
        using var test = new LibraryTestContext();
        var romPath = await test.WriteRomAsync("game.gb", TestRomFactory.Create());
        ResultAssertions.AssertSuccess(await test.Library.RecordOpenedRomAsync(romPath));
        var romHash = Assert
            .Single(ResultAssertions.AssertSuccess(test.Library.GetRoms(limit: 10)))
            .RomHash;
        var sourceImagePath = await test.WriteImageAsync("cover.png", [0x13, 0x14, 0x15]);
        ResultAssertions.AssertSuccess(test.Library.AssignCoverImage(romHash, sourceImagePath));
        var coverPath =
            Assert.Single(ResultAssertions.AssertSuccess(test.Library.GetRoms(limit: 10))).CoverPath
            ?? throw new InvalidOperationException("Cover path was not stored.");

        Assert.True(File.Exists(coverPath));
        ResultAssertions.AssertSuccess(test.Library.RemoveRomPath(romPath));

        Assert.Empty(ResultAssertions.AssertSuccess(test.Library.GetRoms(limit: 10)));
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

        var entries = ResultAssertions.AssertSuccess(test.Library.GetRoms(limit: 10));

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

        var entries = ResultAssertions.AssertSuccess(
            test.Library.GetRoms(new LibraryQuery(SearchText: "fusion"), limit: 10)
        );

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
            ResultAssertions.AssertSuccess(
                test.Library.GetRoms(
                    new LibraryQuery(Hardware: (LibraryHardwareFilter)hardware),
                    limit: 10
                )
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
            ResultAssertions.AssertSuccess(
                test.Library.GetRoms(new LibraryQuery(Cover: (LibraryCoverFilter)cover), limit: 10)
            )
        );

        Assert.Equal(expectedFileName, entry.FileName);
    }

    [Fact]
    public void GetRoms_TitleSortOrdersByDisplayTitleAscending()
    {
        using var test = new LibraryTestContext();
        InsertSortEntries(test.DatabasePath);

        var entries = ResultAssertions.AssertSuccess(
            test.Library.GetRoms(new LibraryQuery(Sort: LibrarySortMode.Title), limit: 10)
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

        var entries = ResultAssertions.AssertSuccess(
            test.Library.GetRoms(new LibraryQuery(Sort: LibrarySortMode.MostPlayed), limit: 10)
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

        var entries = ResultAssertions.AssertSuccess(
            test.Library.GetRoms(new LibraryQuery(Sort: LibrarySortMode.RecentlyAdded), limit: 10)
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
        ResultAssertions.AssertSuccess(await test.Library.RecordOpenedRomAsync(romPath));
        var romHash = Assert
            .Single(ResultAssertions.AssertSuccess(test.Library.GetRoms(limit: 10)))
            .RomHash;
        byte[] imageBytes = [0x89, 0x50, 0x4E, 0x47];
        var sourceImagePath = await test.WriteImageAsync("cover.PNG", imageBytes);
        var expectedCoverPath = Path.Combine(test.CoverDirectoryPath, $"{romHash}.png");

        ResultAssertions.AssertSuccess(test.Library.AssignCoverImage(romHash, sourceImagePath));

        var copiedImageBytes = await File.ReadAllBytesAsync(
            expectedCoverPath,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(imageBytes, copiedImageBytes);
        var entry = Assert.Single(ResultAssertions.AssertSuccess(test.Library.GetRoms(limit: 10)));
        Assert.Equal(expectedCoverPath, entry.CoverPath);
    }

    [Fact]
    public async Task RecordOpenedRomAsync_PreservesCoverPathWhenUpsertingSameRom()
    {
        using var test = new LibraryTestContext();
        var rom = TestRomFactory.Create();
        var firstPath = await test.WriteRomAsync("first.gb", rom);
        ResultAssertions.AssertSuccess(await test.Library.RecordOpenedRomAsync(firstPath));
        var romHash = Assert
            .Single(ResultAssertions.AssertSuccess(test.Library.GetRoms(limit: 10)))
            .RomHash;
        var sourceImagePath = await test.WriteImageAsync("cover.png", [0x01, 0x02, 0x03]);
        ResultAssertions.AssertSuccess(test.Library.AssignCoverImage(romHash, sourceImagePath));
        var coverPath =
            Assert.Single(ResultAssertions.AssertSuccess(test.Library.GetRoms(limit: 10))).CoverPath
            ?? throw new InvalidOperationException("Cover path was not stored.");
        var secondPath = await test.WriteRomAsync("second.gb", rom);

        ResultAssertions.AssertSuccess(await test.Library.RecordOpenedRomAsync(secondPath));

        var entry = Assert.Single(ResultAssertions.AssertSuccess(test.Library.GetRoms(limit: 10)));
        Assert.Equal(coverPath, entry.CoverPath);
        Assert.True(File.Exists(coverPath));
    }

    [Fact]
    public async Task ClearCover_NullsCoverPathAndRemovesManagedCopy()
    {
        using var test = new LibraryTestContext();
        var romPath = await test.WriteRomAsync("game.gb", TestRomFactory.Create());
        ResultAssertions.AssertSuccess(await test.Library.RecordOpenedRomAsync(romPath));
        var romHash = Assert
            .Single(ResultAssertions.AssertSuccess(test.Library.GetRoms(limit: 10)))
            .RomHash;
        var sourceImagePath = await test.WriteImageAsync("cover.png", [0x04, 0x05, 0x06]);
        ResultAssertions.AssertSuccess(test.Library.AssignCoverImage(romHash, sourceImagePath));
        var coverPath =
            Assert.Single(ResultAssertions.AssertSuccess(test.Library.GetRoms(limit: 10))).CoverPath
            ?? throw new InvalidOperationException("Cover path was not stored.");

        ResultAssertions.AssertSuccess(test.Library.ClearCover(romHash));

        Assert.Null(
            Assert.Single(ResultAssertions.AssertSuccess(test.Library.GetRoms(limit: 10))).CoverPath
        );
        Assert.False(File.Exists(coverPath));
    }

    [Fact]
    public async Task CoverOperations_MissingRomHashReturnsFailure()
    {
        using var test = new LibraryTestContext();
        var sourceImagePath = await test.WriteImageAsync("cover.png", [0x07, 0x08, 0x09]);

        Assert.True(test.Library.AssignCoverImage("missing", sourceImagePath).IsFailed);
        Assert.True(test.Library.ClearCover("missing").IsFailed);
    }

    private static int GetAppliedMigrationVersion(string databasePath)
    {
        using var connection = new LibraryDatabase(databasePath).OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
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
        using var connection = new LibraryDatabase(databasePath).OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            insert into roms (
              rom_hash,
              last_known_path,
              file_name,
              cartridge_title,
              added_at,
              last_opened_at,
              launch_count,
              cover_path,
              hardware_kind
            ) values (
              $romHash,
              $lastKnownPath,
              $fileName,
              $cartridgeTitle,
              $addedAt,
              $lastOpenedAt,
              $launchCount,
              $coverPath,
              $hardwareKind
            );
            """;
        command.Parameters.AddWithValue("$romHash", romHash);
        command.Parameters.AddWithValue("$lastKnownPath", Path.Combine(databasePath, fileName));
        command.Parameters.AddWithValue("$fileName", fileName);
        command.Parameters.AddWithValue("$cartridgeTitle", cartridgeTitle ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$addedAt", addedAt ?? lastOpenedAt);
        command.Parameters.AddWithValue("$lastOpenedAt", lastOpenedAt);
        command.Parameters.AddWithValue("$launchCount", launchCount);
        command.Parameters.AddWithValue("$coverPath", coverPath ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$hardwareKind", hardwareKind.ToString());
        command.ExecuteNonQuery();
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
            Library = new LibraryService(
                new LibraryDatabase(DatabasePath),
                CoverDirectoryPath,
                TimeProvider
            );
        }

        private string DirectoryPath { get; } = TestDirectories.GetTemporaryDirectoryPath();

        public string DatabasePath => Path.Combine(DirectoryPath, "library.sqlite");

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

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            TestDirectories.DeleteDirectoryIfExists(DirectoryPath);
        }
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
