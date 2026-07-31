// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Cheats;
using GbcNet.App.Database;
using GbcNet.Core.Cheats;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GbcNet.Tests.App.Cheats;

public sealed class GameGenieServiceTests
{
    [Fact]
    public async Task LoadAsync_ReturnsStoredEntriesInOrderAndPreservesEnabledState()
    {
        using var test = new GameGenieTestContext();
        var hash = CreateHash(1);
        var expected = new[]
        {
            Entry("068-55F-E66", false, "Infinite lives"),
            Entry("0A1-B9F", isEnabled: true, name: "Infinite lives"),
        };

        await test.Service.ReplaceAsync(hash, expected, TestContext.Current.CancellationToken);

        Assert.Equal(
            expected,
            await test.Service.LoadAsync(hash, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task ReplaceAsync_NormalizesNamesAndMapsWhitespaceToNull()
    {
        using var test = new GameGenieTestContext();
        var hash = CreateHash(1);
        var expected = new[] { Entry("068-55F-E66", name: "Infinite lives"), Entry("0A1-B9F") };

        Assert.Equal(
            expected,
            await test.Service.ReplaceAsync(
                hash,
                [Entry("068-55F-E66", name: "  Infinite lives  "), Entry("0A1-B9F", name: " \t ")],
                TestContext.Current.CancellationToken
            )
        );
        Assert.Equal(
            expected,
            await test.Service.LoadAsync(hash, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task ReplaceAsync_RejectsNamesLongerThanMaximum()
    {
        using var test = new GameGenieTestContext();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            test.Service.ReplaceAsync(
                CreateHash(1),
                [Entry("0A1-B9F", name: new string('A', GameGenieService.MaxNameLength + 1))],
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task ReplaceAsync_IsolatesHashesAndReplacesOrClearsExistingEntries()
    {
        using var test = new GameGenieTestContext();
        var firstHash = CreateHash(1);
        var secondHash = CreateHash(2);
        var first = new[] { Entry("0A1-B9F") };
        var replacement = new[] { Entry("068-55F-E66", isEnabled: false) };
        var second = new[] { Entry("05D-49C-E62") };

        await test.Service.ReplaceAsync(firstHash, first, TestContext.Current.CancellationToken);
        await test.Service.ReplaceAsync(secondHash, second, TestContext.Current.CancellationToken);
        Assert.Equal(
            replacement,
            await test.Service.ReplaceAsync(
                firstHash,
                replacement,
                TestContext.Current.CancellationToken
            )
        );

        Assert.Equal(
            replacement,
            await test.Service.LoadAsync(firstHash, TestContext.Current.CancellationToken)
        );
        Assert.Equal(
            second,
            await test.Service.LoadAsync(secondHash, TestContext.Current.CancellationToken)
        );

        Assert.Empty(
            await test.Service.ReplaceAsync(firstHash, [], TestContext.Current.CancellationToken)
        );
        Assert.Empty(
            await test.Service.LoadAsync(firstHash, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task ReplaceAsync_PreservesCodesFromOtherTypes()
    {
        using var test = new GameGenieTestContext();
        var hash = CreateHash(1);
        var storedHash = Convert.ToHexString(hash);
        await InsertCodeAsync(
            test.DatabasePath,
            storedHash,
            sortOrder: 0,
            code: "01020304",
            type: CheatCodeType.GameShark
        );

        var expected = new[] { Entry("0A1-B9F") };
        await test.Service.ReplaceAsync(hash, expected, TestContext.Current.CancellationToken);

        await using var db = test.Factory.CreateDbContext();
        Assert.Equal(
            "01020304",
            await db
                .CheatCodes.Where(entry =>
                    entry.RomHash == storedHash && entry.Type == CheatCodeType.GameShark
                )
                .Select(entry => entry.Code)
                .SingleAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal(
            expected,
            await test.Service.LoadAsync(hash, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task ReplaceAsync_AcceptsTwentyEntriesAndRejectsTwentyOne()
    {
        using var test = new GameGenieTestContext();
        var hash = CreateHash(1);
        var entries = Enumerable
            .Range(0, GameGenieService.MaxEntryCount)
            .Select(index => Entry($"{index:X2}0-00F"))
            .ToArray();

        Assert.Equal(
            entries,
            await test.Service.ReplaceAsync(hash, entries, TestContext.Current.CancellationToken)
        );
        await Assert.ThrowsAsync<ArgumentException>(() =>
            test.Service.ReplaceAsync(
                hash,
                [.. entries, Entry("120-01F")],
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task ReplaceAsync_RejectsInvalidHashes()
    {
        using var test = new GameGenieTestContext();
        var entries = new[] { Entry("0A1-B9F") };

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            test.Service.ReplaceAsync(null!, entries, TestContext.Current.CancellationToken)
        );
        await Assert.ThrowsAsync<ArgumentException>(() =>
            test.Service.ReplaceAsync(new byte[31], entries, TestContext.Current.CancellationToken)
        );
        await Assert.ThrowsAsync<ArgumentException>(() =>
            test.Service.ReplaceAsync(new byte[33], entries, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task ReplaceAsync_RejectsDefaultAndEffectiveDuplicates()
    {
        using var test = new GameGenieTestContext();
        var hash = CreateHash(1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            test.Service.ReplaceAsync(
                hash,
                [new GameGenieCodeEntry(default, true)],
                TestContext.Current.CancellationToken
            )
        );
        await Assert.ThrowsAsync<ArgumentException>(() =>
            test.Service.ReplaceAsync(
                hash,
                [Entry("068-55F-E66"), Entry("068-55F-E76")],
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task ReplaceAsync_PersistsWithoutALibraryRom()
    {
        using var test = new GameGenieTestContext();
        var hash = CreateHash(1);
        var entries = new[] { Entry("0A1-B9F") };

        await test.Service.ReplaceAsync(hash, entries, TestContext.Current.CancellationToken);

        await using var db = test.Factory.CreateDbContext();
        Assert.Empty(await db.Roms.ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            entries,
            await test.Service.LoadAsync(hash, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task ReplaceAsync_SaveFailureRollsBackTheExistingList()
    {
        using var test = new GameGenieTestContext();
        var hash = CreateHash(1);
        var original = new[] { Entry("0A1-B9F") };
        var replacement = new[] { Entry("068-55F-E66") };
        await test.Service.ReplaceAsync(hash, original, TestContext.Current.CancellationToken);
        var failingService = new GameGenieService(
            new TestDbContextFactory(test.DatabasePath, new FailingSaveChangesInterceptor())
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            failingService.ReplaceAsync(hash, replacement, TestContext.Current.CancellationToken)
        );

        Assert.Equal("Game Genie codes could not be saved.", exception.Message);
        Assert.IsType<DbUpdateException>(exception.InnerException);
        Assert.Equal(
            original,
            await test.Service.LoadAsync(hash, TestContext.Current.CancellationToken)
        );
    }

    [Theory]
    [InlineData("not-a-code")]
    [InlineData("06855FE66")]
    public async Task LoadAsync_RejectsInvalidOrNonCanonicalStoredCode(string code)
    {
        using var test = new GameGenieTestContext();
        var hash = CreateHash(1);
        await InsertCodeAsync(test.DatabasePath, Convert.ToHexString(hash), 0, code);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            test.Service.LoadAsync(hash, TestContext.Current.CancellationToken)
        );

        Assert.Equal("Game Genie codes could not be loaded.", exception.Message);
    }

    [Fact]
    public async Task LoadAsync_RejectsNonCanonicalOrOversizedStoredNames()
    {
        foreach (
            var name in new[]
            {
                "",
                " Not trimmed",
                new string('A', GameGenieService.MaxNameLength + 1),
            }
        )
        {
            using var test = new GameGenieTestContext();
            var hash = CreateHash(1);
            await InsertCodeAsync(
                test.DatabasePath,
                Convert.ToHexString(hash),
                0,
                "0A1-B9F",
                name: name,
                ignoreCheckConstraints: true
            );

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                test.Service.LoadAsync(hash, TestContext.Current.CancellationToken)
            );

            Assert.Equal("Game Genie codes could not be loaded.", exception.Message);
        }
    }

    [Fact]
    public async Task LoadAsync_RejectsEffectiveDuplicatesThatDifferOnlyInIgnoredNibble()
    {
        using var test = new GameGenieTestContext();
        var hash = CreateHash(1);
        var storedHash = Convert.ToHexString(hash);
        await InsertCodeAsync(test.DatabasePath, storedHash, 0, "068-55F-E66");
        await InsertCodeAsync(test.DatabasePath, storedHash, 1, "068-55F-E76");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            test.Service.LoadAsync(hash, TestContext.Current.CancellationToken)
        );

        Assert.Equal("Game Genie codes could not be loaded.", exception.Message);
    }

    [Fact]
    public async Task Schema_EnforcesTypeSpecificKeysAndRejectsInvalidValues()
    {
        using var test = new GameGenieTestContext();
        var hash = Convert.ToHexString(CreateHash(1));
        await InsertCodeAsync(test.DatabasePath, hash, 0, "0A1-B9F");
        await InsertCodeAsync(
            test.DatabasePath,
            hash,
            sortOrder: 0,
            code: "0A1-B9F",
            type: CheatCodeType.GameShark
        );

        await Assert.ThrowsAnyAsync<Exception>(() =>
            InsertCodeAsync(test.DatabasePath, hash, 20, "068-55F-E66")
        );
        await Assert.ThrowsAnyAsync<Exception>(() =>
            InsertCodeAsync(test.DatabasePath, hash, 1, "068-55F-E66", 2)
        );
        await Assert.ThrowsAnyAsync<Exception>(() =>
            InsertCodeAsync(
                test.DatabasePath,
                hash,
                sortOrder: 1,
                code: "05D-49C-E62",
                type: (CheatCodeType)2
            )
        );
        await Assert.ThrowsAnyAsync<Exception>(() =>
            InsertCodeAsync(test.DatabasePath, hash, 0, "068-55F-E66")
        );
        await Assert.ThrowsAnyAsync<Exception>(() =>
            InsertCodeAsync(test.DatabasePath, hash, 1, "0A1-B9F")
        );
        await InsertCodeAsync(test.DatabasePath, hash, 1, "068-55F-E66", name: "Valid name");
        await Assert.ThrowsAnyAsync<Exception>(() =>
            InsertCodeAsync(test.DatabasePath, hash, 2, "05D-49C-E62", name: "")
        );
        await Assert.ThrowsAnyAsync<Exception>(() =>
            InsertCodeAsync(test.DatabasePath, hash, 3, "073-11F", name: " Not trimmed")
        );
        await Assert.ThrowsAnyAsync<Exception>(() =>
            InsertCodeAsync(
                test.DatabasePath,
                hash,
                4,
                "091-22F",
                name: new string('A', GameGenieService.MaxNameLength + 1)
            )
        );
    }

    private static GameGenieCodeEntry Entry(string code, bool isEnabled = true, string? name = null)
    {
        Assert.True(GameGenieCode.TryParse(code, out var parsed));
        return new GameGenieCodeEntry(parsed, isEnabled, name);
    }

    private static byte[] CreateHash(byte value) => [.. Enumerable.Repeat(value, 32)];

    private static async Task InsertCodeAsync(
        string databasePath,
        string hash,
        int sortOrder,
        string code,
        int isEnabled = 1,
        CheatCodeType type = CheatCodeType.GameGenie,
        string? name = null,
        bool ignoreCheckConstraints = false
    )
    {
        await using var db = new TestDbContextFactory(databasePath).CreateDbContext();

        if (ignoreCheckConstraints)
        {
            await db.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
            await db.Database.ExecuteSqlRawAsync(
                "PRAGMA ignore_check_constraints = ON",
                TestContext.Current.CancellationToken
            );
        }

        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO cheat_codes (rom_hash, type, sort_order, code, name, is_enabled) VALUES ({hash}, {(int)type}, {sortOrder}, {code}, {name}, {isEnabled})",
            TestContext.Current.CancellationToken
        );
    }

    private sealed class GameGenieTestContext : IDisposable
    {
        private TestDirectories.TemporaryDirectory TemporaryDirectory { get; } =
            TestDirectories.CreateTemporaryDirectory();

        public GameGenieTestContext()
        {
            Directory.CreateDirectory(TemporaryDirectory.Path);
            Factory = new TestDbContextFactory(DatabasePath);
            using var db = Factory.CreateDbContext();
            db.Database.Migrate();
            Service = new GameGenieService(Factory);
        }

        public string DatabasePath => Path.Combine(TemporaryDirectory.Path, "gbcnet.sqlite");

        public TestDbContextFactory Factory { get; }

        public GameGenieService Service { get; }

        public void Dispose() => TemporaryDirectory.Dispose();
    }

    private sealed class TestDbContextFactory : IDbContextFactory<GbcNetDbContext>
    {
        private readonly DbContextOptions<GbcNetDbContext> _options;

        public TestDbContextFactory(string databasePath, IInterceptor? interceptor = null)
        {
            var builder = new DbContextOptionsBuilder<GbcNetDbContext>().UseSqlite(
                $"Data Source={databasePath}"
            );
            if (interceptor is not null)
            {
                builder.AddInterceptors(interceptor);
            }

            _options = builder.Options;
        }

        public GbcNetDbContext CreateDbContext() => new(_options);
    }

    private sealed class FailingSaveChangesInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        ) => throw new DbUpdateException("Synthetic save failure.");
    }
}
