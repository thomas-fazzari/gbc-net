// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Cheats;
using GbcNet.App.Database;
using GbcNet.Core.Cheats;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GbcNet.Tests.Integration.Cheats;

public sealed class CheatCodeServiceTests
{
    [Fact]
    public async Task LoadAsync_ReturnsStoredEntriesInTypeAndEntryOrderAndPreservesEnabledState()
    {
        using var test = new CheatCodeTestContext();
        var hash = CreateHash(1);
        var submitted = new[]
        {
            Entry(CheatCodeType.GameShark, "010200C0", isEnabled: false, "Max lives"),
            Entry(CheatCodeType.GameGenie, "068-55F-E66", isEnabled: false, "Infinite lives"),
            Entry(CheatCodeType.GameShark, "010300C0", isEnabled: true, "Max energy"),
            Entry(CheatCodeType.GameGenie, "0A1-B9F", isEnabled: true, "Infinite lives"),
        };
        var expected = new[] { submitted[1], submitted[3], submitted[0], submitted[2] };

        (await test.Service.ReplaceAsync(hash, submitted, TestContext.Current.CancellationToken))
            .Should()
            .Equal(submitted);
        (await test.Service.LoadAsync(hash, TestContext.Current.CancellationToken))
            .Should()
            .Equal(expected);
    }

    [Fact]
    public async Task ReplaceAsync_NormalizesNamesAndMapsWhitespaceToNull()
    {
        using var test = new CheatCodeTestContext();
        var hash = CreateHash(1);
        var expected = new[]
        {
            Entry(CheatCodeType.GameGenie, "068-55F-E66", name: "Infinite lives"),
            Entry(CheatCodeType.GameShark, "010100C0"),
        };

        (
            await test.Service.ReplaceAsync(
                hash,
                [
                    Entry(CheatCodeType.GameGenie, "068-55F-E66", name: "  Infinite lives  "),
                    Entry(CheatCodeType.GameShark, "010100C0", name: " \t "),
                ],
                TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Equal(expected);
        (await test.Service.LoadAsync(hash, TestContext.Current.CancellationToken))
            .Should()
            .Equal(expected);
    }

    [Fact]
    public async Task ReplaceAsync_RejectsNamesLongerThanMaximum()
    {
        using var test = new CheatCodeTestContext();
        var service = test.Service;

        await FluentActions
            .Awaiting(() =>
                service.ReplaceAsync(
                    CreateHash(1),
                    [
                        Entry(
                            CheatCodeType.GameGenie,
                            "0A1-B9F",
                            name: new string('A', CheatCodeService.MaxNameLength + 1)
                        ),
                    ],
                    TestContext.Current.CancellationToken
                )
            )
            .Should()
            .ThrowExactlyAsync<ArgumentException>();
    }

    [Fact]
    public async Task ReplaceAsync_IsolatesHashesAndReplacesOrClearsBothFamilies()
    {
        using var test = new CheatCodeTestContext();
        var firstHash = CreateHash(1);
        var secondHash = CreateHash(2);
        var first = new[]
        {
            Entry(CheatCodeType.GameGenie, "0A1-B9F"),
            Entry(CheatCodeType.GameShark, "010100C0"),
        };
        var replacement = new[]
        {
            Entry(CheatCodeType.GameGenie, "068-55F-E66", isEnabled: false),
            Entry(CheatCodeType.GameShark, "010200C0", isEnabled: false),
        };
        var second = new[]
        {
            Entry(CheatCodeType.GameGenie, "05D-49C-E62"),
            Entry(CheatCodeType.GameShark, "010300C0"),
        };

        await test.Service.ReplaceAsync(firstHash, first, TestContext.Current.CancellationToken);
        await test.Service.ReplaceAsync(secondHash, second, TestContext.Current.CancellationToken);
        (
            await test.Service.ReplaceAsync(
                firstHash,
                replacement,
                TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Equal(replacement);

        (await test.Service.LoadAsync(firstHash, TestContext.Current.CancellationToken))
            .Should()
            .Equal(replacement);
        (await test.Service.LoadAsync(secondHash, TestContext.Current.CancellationToken))
            .Should()
            .Equal(second);

        (await test.Service.ReplaceAsync(firstHash, [], TestContext.Current.CancellationToken))
            .Should()
            .BeEmpty();
        (await test.Service.LoadAsync(firstHash, TestContext.Current.CancellationToken))
            .Should()
            .BeEmpty();
    }

    [Theory]
    [InlineData(CheatCodeType.GameGenie)]
    [InlineData(CheatCodeType.GameShark)]
    public async Task ReplaceAsync_AcceptsTwentyEntriesAndRejectsTwentyOnePerType(
        CheatCodeType type
    )
    {
        using var test = new CheatCodeTestContext();
        var hash = CreateHash(1);
        var entries = Enumerable
            .Range(0, CheatCodeService.MaxEntryCount)
            .Select(index => Entry(type, Code(type, index)))
            .ToArray();

        (await test.Service.ReplaceAsync(hash, entries, TestContext.Current.CancellationToken))
            .Should()
            .Equal(entries);
        var service = test.Service;
        await FluentActions
            .Awaiting(() =>
                service.ReplaceAsync(
                    hash,
                    [.. entries, Entry(type, Code(type, CheatCodeService.MaxEntryCount))],
                    TestContext.Current.CancellationToken
                )
            )
            .Should()
            .ThrowExactlyAsync<ArgumentException>();
    }

    [Fact]
    public async Task ReplaceAsync_AcceptsLimitsForBothFamilies()
    {
        using var test = new CheatCodeTestContext();
        CheatCodeEntry[] entries =
        [
            .. Enumerable
                .Range(0, CheatCodeService.MaxEntryCount)
                .Select(index =>
                    Entry(CheatCodeType.GameGenie, Code(CheatCodeType.GameGenie, index))
                ),
            .. Enumerable
                .Range(0, CheatCodeService.MaxEntryCount)
                .Select(index =>
                    Entry(CheatCodeType.GameShark, Code(CheatCodeType.GameShark, index))
                ),
        ];

        (
            await test.Service.ReplaceAsync(
                CreateHash(1),
                entries,
                TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Equal(entries);
    }

    [Fact]
    public async Task ReplaceAsync_RejectsInvalidHashes()
    {
        using var test = new CheatCodeTestContext();
        var entries = new[] { Entry(CheatCodeType.GameGenie, "0A1-B9F") };
        var service = test.Service;

        await FluentActions
            .Awaiting(() =>
                service.ReplaceAsync(null!, entries, TestContext.Current.CancellationToken)
            )
            .Should()
            .ThrowExactlyAsync<ArgumentNullException>();
        await FluentActions
            .Awaiting(() =>
                service.ReplaceAsync(new byte[31], entries, TestContext.Current.CancellationToken)
            )
            .Should()
            .ThrowExactlyAsync<ArgumentException>();
        await FluentActions
            .Awaiting(() =>
                service.ReplaceAsync(new byte[33], entries, TestContext.Current.CancellationToken)
            )
            .Should()
            .ThrowExactlyAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(CheatCodeType.GameGenie, "068-55F-E66", "068-55F-E76")]
    [InlineData(CheatCodeType.GameShark, "010100C0", "010100C0")]
    public async Task ReplaceAsync_RejectsDefaultAndEffectiveDuplicatesPerType(
        CheatCodeType type,
        string firstCode,
        string duplicateCode
    )
    {
        using var test = new CheatCodeTestContext();
        var hash = CreateHash(1);
        var service = test.Service;

        await FluentActions
            .Awaiting(() =>
                service.ReplaceAsync(
                    hash,
                    [new CheatCodeEntry(default, true)],
                    TestContext.Current.CancellationToken
                )
            )
            .Should()
            .ThrowExactlyAsync<ArgumentException>();
        await FluentActions
            .Awaiting(() =>
                service.ReplaceAsync(
                    hash,
                    [Entry(type, firstCode), Entry(type, duplicateCode)],
                    TestContext.Current.CancellationToken
                )
            )
            .Should()
            .ThrowExactlyAsync<ArgumentException>();
    }

    [Fact]
    public async Task ReplaceAsync_PersistsWithoutALibraryRom()
    {
        using var test = new CheatCodeTestContext();
        var hash = CreateHash(1);
        var entries = new[]
        {
            Entry(CheatCodeType.GameGenie, "0A1-B9F"),
            Entry(CheatCodeType.GameShark, "010100C0"),
        };

        await test.Service.ReplaceAsync(hash, entries, TestContext.Current.CancellationToken);

        await using var db = test.Factory.CreateDbContext();
        (await db.Roms.ToArrayAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        (await test.Service.LoadAsync(hash, TestContext.Current.CancellationToken))
            .Should()
            .Equal(entries);
    }

    [Fact]
    public async Task ReplaceAsync_SaveFailureRollsBackBothFamilies()
    {
        using var test = new CheatCodeTestContext();
        var hash = CreateHash(1);
        var original = new[]
        {
            Entry(CheatCodeType.GameGenie, "0A1-B9F"),
            Entry(CheatCodeType.GameShark, "010100C0"),
        };
        var replacement = new[]
        {
            Entry(CheatCodeType.GameGenie, "068-55F-E66"),
            Entry(CheatCodeType.GameShark, "010200C0"),
        };
        await test.Service.ReplaceAsync(hash, original, TestContext.Current.CancellationToken);
        var failingService = new CheatCodeService(
            new TestDbContextFactory(test.DatabasePath, new FailingSaveChangesInterceptor())
        );

        var exception = (
            await FluentActions
                .Awaiting(() =>
                    failingService.ReplaceAsync(
                        hash,
                        replacement,
                        TestContext.Current.CancellationToken
                    )
                )
                .Should()
                .ThrowExactlyAsync<InvalidOperationException>()
        ).Which;

        exception.Message.Should().Be("Cheat codes could not be saved.");
        exception.InnerException.Should().BeOfType<DbUpdateException>();
        (await test.Service.LoadAsync(hash, TestContext.Current.CancellationToken))
            .Should()
            .Equal(original);
    }

    [Theory]
    [InlineData(CheatCodeType.GameGenie, "not-a-code")]
    [InlineData(CheatCodeType.GameGenie, "06855FE66")]
    [InlineData(CheatCodeType.GameShark, "not-a-code")]
    [InlineData(CheatCodeType.GameShark, "010100c0")]
    public async Task LoadAsync_RejectsInvalidOrNonCanonicalStoredCode(
        CheatCodeType type,
        string code
    )
    {
        using var test = new CheatCodeTestContext();
        var hash = CreateHash(1);
        await InsertCodeAsync(test.DatabasePath, Convert.ToHexString(hash), 0, code, type: type);
        var service = test.Service;

        var exception = (
            await FluentActions
                .Awaiting(() => service.LoadAsync(hash, TestContext.Current.CancellationToken))
                .Should()
                .ThrowExactlyAsync<InvalidOperationException>()
        ).Which;

        exception.Message.Should().Be("Cheat codes could not be loaded.");
    }

    [Fact]
    public async Task LoadAsync_RejectsNonCanonicalOrOversizedStoredNames()
    {
        foreach (
            var name in new[]
            {
                "",
                " Not trimmed",
                new string('A', CheatCodeService.MaxNameLength + 1),
            }
        )
        {
            using var test = new CheatCodeTestContext();
            var service = test.Service;
            var hash = CreateHash(1);
            await InsertCodeAsync(
                test.DatabasePath,
                Convert.ToHexString(hash),
                0,
                "010100C0",
                type: CheatCodeType.GameShark,
                name: name,
                ignoreCheckConstraints: true
            );

            var exception = (
                await FluentActions
                    .Awaiting(() => service.LoadAsync(hash, TestContext.Current.CancellationToken))
                    .Should()
                    .ThrowExactlyAsync<InvalidOperationException>()
            ).Which;

            exception.Message.Should().Be("Cheat codes could not be loaded.");
        }
    }

    [Fact]
    public async Task LoadAsync_RejectsEffectiveGameGenieDuplicates()
    {
        using var test = new CheatCodeTestContext();
        var hash = CreateHash(1);
        var storedHash = Convert.ToHexString(hash);
        await InsertCodeAsync(test.DatabasePath, storedHash, 0, "068-55F-E66");
        await InsertCodeAsync(test.DatabasePath, storedHash, 1, "068-55F-E76");
        var service = test.Service;

        var exception = (
            await FluentActions
                .Awaiting(() => service.LoadAsync(hash, TestContext.Current.CancellationToken))
                .Should()
                .ThrowExactlyAsync<InvalidOperationException>()
        ).Which;

        exception.Message.Should().Be("Cheat codes could not be loaded.");
    }

    [Fact]
    public async Task Schema_EnforcesTypeAndSortOrderKeysAndRejectsInvalidValues()
    {
        using var test = new CheatCodeTestContext();
        var hash = Convert.ToHexString(CreateHash(1));
        var databasePath = test.DatabasePath;
        await InsertCodeAsync(databasePath, hash, 0, "0A1-B9F");
        await InsertCodeAsync(
            databasePath,
            hash,
            sortOrder: 0,
            code: "010100C0",
            type: CheatCodeType.GameShark
        );

        await FluentActions
            .Awaiting(() => InsertCodeAsync(databasePath, hash, 20, "068-55F-E66"))
            .Should()
            .ThrowExactlyAsync<SqliteException>();
        await FluentActions
            .Awaiting(() => InsertCodeAsync(databasePath, hash, 1, "068-55F-E66", isEnabled: 2))
            .Should()
            .ThrowExactlyAsync<SqliteException>();
        await FluentActions
            .Awaiting(() =>
                InsertCodeAsync(
                    databasePath,
                    hash,
                    sortOrder: 1,
                    code: "010200C0",
                    type: (CheatCodeType)2
                )
            )
            .Should()
            .ThrowExactlyAsync<SqliteException>();
        await FluentActions
            .Awaiting(() => InsertCodeAsync(databasePath, hash, 0, "068-55F-E66"))
            .Should()
            .ThrowExactlyAsync<SqliteException>();
        await FluentActions
            .Awaiting(() =>
                InsertCodeAsync(
                    databasePath,
                    hash,
                    sortOrder: 0,
                    code: "010200C0",
                    type: CheatCodeType.GameShark
                )
            )
            .Should()
            .ThrowExactlyAsync<SqliteException>();
        await InsertCodeAsync(databasePath, hash, 1, "068-55F-E66", name: "Valid name");
        await FluentActions
            .Awaiting(() => InsertCodeAsync(databasePath, hash, 2, "05D-49C-E62", name: ""))
            .Should()
            .ThrowExactlyAsync<SqliteException>();
        await FluentActions
            .Awaiting(() => InsertCodeAsync(databasePath, hash, 3, "073-11F", name: " Not trimmed"))
            .Should()
            .ThrowExactlyAsync<SqliteException>();
        await FluentActions
            .Awaiting(() =>
                InsertCodeAsync(
                    databasePath,
                    hash,
                    4,
                    "091-22F",
                    name: new string('A', CheatCodeService.MaxNameLength + 1)
                )
            )
            .Should()
            .ThrowExactlyAsync<SqliteException>();
    }

    private static CheatCodeEntry Entry(
        CheatCodeType type,
        string code,
        bool isEnabled = true,
        string? name = null
    )
    {
        CheatCode.TryParse(type, code, out var parsed).Should().BeTrue();
        return new CheatCodeEntry(parsed, isEnabled, name);
    }

    private static string Code(CheatCodeType type, int index) =>
        type == CheatCodeType.GameGenie ? $"{index:X2}0-00F" : $"01{index:X2}{index:X2}C0";

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

    private sealed class CheatCodeTestContext : IDisposable
    {
        private TestDirectories.TemporaryDirectory TemporaryDirectory { get; } =
            TestDirectories.CreateTemporaryDirectory();

        public CheatCodeTestContext()
        {
            Directory.CreateDirectory(TemporaryDirectory.Path);
            Factory = new TestDbContextFactory(DatabasePath);
            using var db = Factory.CreateDbContext();
            db.Database.Migrate();
            Service = new CheatCodeService(Factory);
        }

        public string DatabasePath => Path.Combine(TemporaryDirectory.Path, "gbcnet.sqlite");

        public TestDbContextFactory Factory { get; }

        public CheatCodeService Service { get; }

        public void Dispose() => TemporaryDirectory.Dispose();
    }

    private sealed class TestDbContextFactory : IDbContextFactory<GbcNetDbContext>
    {
        private readonly DbContextOptions<GbcNetDbContext> _options;

        public TestDbContextFactory(string databasePath, IInterceptor? interceptor = null)
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
