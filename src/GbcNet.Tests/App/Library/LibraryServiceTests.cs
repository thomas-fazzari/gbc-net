using System.Globalization;
using FluentResults;
using GbcNet.App.Library;
using GbcNet.Tests.Cartridges;

namespace GbcNet.Tests.App.Library;

public sealed class LibraryServiceTests
{
    [Fact]
    public async Task RecordOpenedRomAsync_AppliesSchemaVersionOneMigration()
    {
        using var test = new LibraryTestContext();
        var romPath = await test.WriteRomAsync("game.gb", TestRomFactory.Create());

        AssertSuccess(await test.Library.RecordOpenedRomAsync(romPath));

        Assert.Equal(1, GetAppliedMigrationVersion(test.DatabasePath));
    }

    [Fact]
    public async Task RecordOpenedRomAsync_UpsertsByRomHashAndUpdatesLastKnownPath()
    {
        using var test = new LibraryTestContext();
        var rom = TestRomFactory.Create();
        var firstPath = await test.WriteRomAsync("first.gb", rom);
        var secondPath = await test.WriteRomAsync("second.gb", rom);

        AssertSuccess(await test.Library.RecordOpenedRomAsync(firstPath));
        test.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        AssertSuccess(await test.Library.RecordOpenedRomAsync(secondPath));

        var entry = Assert.Single(test.Library.GetRecentRoms(limit: 10));
        Assert.Equal(Path.GetFullPath(secondPath), entry.LastKnownPath);
        Assert.Equal("second.gb", entry.FileName);
        Assert.Equal("TEST ROM", entry.CartridgeTitle);
        Assert.Equal(2, entry.LaunchCount);
        Assert.Equal("2026-06-27T14:00:00.0000000+02:00", entry.AddedAt);
        Assert.Equal("2026-06-27T14:01:00.0000000+02:00", entry.LastOpenedAt);
    }

    [Fact]
    public async Task RecordOpenedRomAsync_ReplacesPreviousHashForSamePath()
    {
        using var test = new LibraryTestContext();
        var romPath = await test.WriteRomAsync("game.gb", TestRomFactory.Create());
        AssertSuccess(await test.Library.RecordOpenedRomAsync(romPath));
        await test.WriteRomAsync(
            "game.gb",
            TestRomFactory.Create(bytes => "SECOND ROM"u8.CopyTo(bytes.AsSpan(0x0134)))
        );

        AssertSuccess(await test.Library.RecordOpenedRomAsync(romPath));

        var entry = Assert.Single(test.Library.GetRecentRoms(limit: 10));
        Assert.Equal(Path.GetFullPath(romPath), entry.LastKnownPath);
        Assert.Equal("SECOND ROM", entry.CartridgeTitle);
        Assert.Equal(1, entry.LaunchCount);
    }

    [Fact]
    public void GetRecentRoms_OrdersByInstantWhenLocalOffsetChanges()
    {
        using var test = new LibraryTestContext();
        InsertLibraryEntry(
            test.DatabasePath,
            "older",
            "older.gb",
            "2026-10-25T02:30:00.0000000+02:00"
        );
        InsertLibraryEntry(
            test.DatabasePath,
            "newer",
            "newer.gb",
            "2026-10-25T02:15:00.0000000+01:00"
        );

        var entries = test.Library.GetRecentRoms(limit: 10);

        Assert.Collection(
            entries,
            entry => Assert.Equal("newer.gb", entry.FileName),
            entry => Assert.Equal("older.gb", entry.FileName)
        );
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
        string lastOpenedAt
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
              cover_path
            ) values (
              $romHash,
              $lastKnownPath,
              $fileName,
              null,
              $lastOpenedAt,
              $lastOpenedAt,
              1,
              null
            );
            """;
        command.Parameters.AddWithValue("$romHash", romHash);
        command.Parameters.AddWithValue("$lastKnownPath", Path.Combine(databasePath, fileName));
        command.Parameters.AddWithValue("$fileName", fileName);
        command.Parameters.AddWithValue("$lastOpenedAt", lastOpenedAt);
        command.ExecuteNonQuery();
    }

    private static void AssertSuccess(Result result)
    {
        Assert.True(
            result.IsSuccess,
            string.Join(Environment.NewLine, result.Errors.Select(static error => error.Message))
        );
    }

    private sealed class LibraryTestContext : IDisposable
    {
        public LibraryTestContext()
        {
            Directory.CreateDirectory(DirectoryPath);
            Library = new LibraryService(new LibraryDatabase(DatabasePath), TimeProvider);
        }

        private string DirectoryPath { get; } = TestDirectories.GetTemporaryDirectoryPath();

        public string DatabasePath => Path.Combine(DirectoryPath, "library.sqlite");

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

        public void Dispose() => TestDirectories.DeleteDirectoryIfExists(DirectoryPath);
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
