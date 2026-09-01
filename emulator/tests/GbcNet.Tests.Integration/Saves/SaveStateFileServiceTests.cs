// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers;
using GbcNet.App.Saves;
using GbcNet.Core.Hardware;
using Microsoft.Extensions.Logging.Abstractions;

namespace GbcNet.Tests.Integration.Saves;

public sealed class SaveStateFileServiceTests
{
    [Fact]
    public async Task SaveAsyncAndLoadAsync_RoundTripsRomBoundPayload()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        SaveStateFileService saveStates = new(
            tempDirectory.Path,
            NullLogger<SaveStateFileService>.Instance
        );
        var rom = RomStorageIdentity.Create("Test Rom", [0x01, 0x02]);

        rom.FileStem.Should()
            .Be("TEST_ROM-A12871FEE210FB8619291EAEA194581CBD2531E4B23759D225F6806923F63222");

        rom.HashHex.Should().Be("A12871FEE210FB8619291EAEA194581CBD2531E4B23759D225F6806923F63222");

        saveStates.GetSaveStateDate(rom, 3).Should().BeNull();

        await saveStates.SaveAsync(
            rom,
            3,
            HardwareModel.Cgb,
            new byte[] { 0x10, 0x20, 0x30 },
            TestContext.Current.CancellationToken
        );
        var payload = await saveStates.LoadAsync(
            rom,
            3,
            HardwareModel.Cgb,
            TestContext.Current.CancellationToken
        );

        payload.Should().Equal(0x10, 0x20, 0x30);
        Path.GetFileName(saveStates.GetSaveStatePath(rom, 3))
            .Should()
            .Be(rom.FileStem + ".slot-3.gbstate");
        saveStates.GetSaveStateDate(rom, 3).Should().NotBeNull();
    }

    [Fact]
    public void GetSaveStatePath_DistinguishesRomsWithTheSameLegacyHashPrefix()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        SaveStateFileService saveStates = new(
            tempDirectory.Path,
            NullLogger<SaveStateFileService>.Instance
        );
        var firstRom = RomStorageIdentity.Create("Test Rom", [0x00, 0x00, 0xA0, 0x36]);
        var secondRom = RomStorageIdentity.Create("Test Rom", [0x00, 0x00, 0xB2, 0x54]);

        firstRom.Hash.AsSpan(0, 4).SequenceEqual(secondRom.Hash.AsSpan(0, 4)).Should().BeTrue();
        saveStates
            .GetSaveStatePath(firstRom, 0)
            .Should()
            .NotBe(saveStates.GetSaveStatePath(secondRom, 0));
    }

    [Fact]
    public async Task LoadAsync_RejectsPayloadCorruption()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        SaveStateFileService saveStates = new(
            tempDirectory.Path,
            NullLogger<SaveStateFileService>.Instance
        );
        var rom = RomStorageIdentity.Create("Test Rom", [0x01, 0x02]);
        await saveStates.SaveAsync(
            rom,
            0,
            HardwareModel.Dmg,
            new byte[] { 0x10 },
            TestContext.Current.CancellationToken
        );
        var path = saveStates.GetSaveStatePath(rom, 0);
        var bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        bytes[^1] ^= 0x01;
        await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);

        await FluentActions
            .Awaiting(() =>
                saveStates.LoadAsync(
                    rom,
                    0,
                    HardwareModel.Dmg,
                    TestContext.Current.CancellationToken
                )
            )
            .Should()
            .ThrowExactlyAsync<InvalidDataException>();
    }

    [Fact]
    public async Task LoadAsync_ReportsTruncatedStateWithTheExistingInvalidDataContract()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        SaveStateFileService saveStates = new(
            tempDirectory.Path,
            NullLogger<SaveStateFileService>.Instance
        );
        var rom = RomStorageIdentity.Create("Test Rom", [0x01, 0x02]);
        await saveStates.SaveAsync(
            rom,
            0,
            HardwareModel.Dmg,
            new byte[] { 0x10 },
            TestContext.Current.CancellationToken
        );
        var path = saveStates.GetSaveStatePath(rom, 0);
        var bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(path, bytes[..^1], TestContext.Current.CancellationToken);

        var exception = (
            await FluentActions
                .Awaiting(() =>
                    saveStates.LoadAsync(
                        rom,
                        0,
                        HardwareModel.Dmg,
                        TestContext.Current.CancellationToken
                    )
                )
                .Should()
                .ThrowExactlyAsync<InvalidDataException>()
        ).Which;

        exception.Message.Should().Be("Save-state file is truncated.");
        exception.InnerException.Should().BeOfType<EndOfStreamException>();
    }

    [Fact]
    public async Task LoadAsync_DistinguishesMissingSlot()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        SaveStateFileService saveStates = new(
            tempDirectory.Path,
            NullLogger<SaveStateFileService>.Instance
        );
        var rom = RomStorageIdentity.Create("Test Rom", [0x01, 0x02]);

        await FluentActions
            .Awaiting(() =>
                saveStates.LoadAsync(
                    rom,
                    0,
                    HardwareModel.Dmg,
                    TestContext.Current.CancellationToken
                )
            )
            .Should()
            .ThrowExactlyAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task SaveAsync_SerializesOverlappingRequestsInCallOrder()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        SaveStateFileService saveStates = new(
            tempDirectory.Path,
            NullLogger<SaveStateFileService>.Instance
        );
        var rom = RomStorageIdentity.Create("Test Rom", [0x01, 0x02]);
        using var olderPayload = new BlockingMemoryManager(
            [0x10],
            TestContext.Current.CancellationToken
        );
        var olderPayloadMemory = olderPayload.Payload;

        var olderSave = Task.Run(
            () =>
                saveStates.SaveAsync(
                    rom,
                    0,
                    HardwareModel.Dmg,
                    olderPayloadMemory,
                    TestContext.Current.CancellationToken
                ),
            TestContext.Current.CancellationToken
        );
        olderPayload.WaitUntilAccess(TestContext.Current.CancellationToken);

        var newerSave = saveStates.SaveAsync(
            rom,
            0,
            HardwareModel.Dmg,
            new byte[] { 0x20 },
            TestContext.Current.CancellationToken
        );
        olderPayload.Release();

        await Task.WhenAll(olderSave, newerSave);

        var payload = await saveStates.LoadAsync(
            rom,
            0,
            HardwareModel.Dmg,
            TestContext.Current.CancellationToken
        );
        payload.Should().Equal(0x20);
    }

    [Fact]
    public async Task SaveAsync_CanceledBeforeCompression_DoesNotAccessPayloadOrPublishFile()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        SaveStateFileService saveStates = new(
            tempDirectory.Path,
            NullLogger<SaveStateFileService>.Instance
        );
        var rom = RomStorageIdentity.Create("Test Rom", [0x01, 0x02]);
        using var payload = new ObservingMemoryManager([0x10]);
        using var cancellation = new CancellationTokenSource();
        var payloadMemory = payload.Payload;
        var token = cancellation.Token;

        await cancellation.CancelAsync();

        await FluentActions
            .Awaiting(() => saveStates.SaveAsync(rom, 0, HardwareModel.Dmg, payloadMemory, token))
            .Should()
            .ThrowExactlyAsync<OperationCanceledException>();

        payload.SpanAccessCount.Should().Be(0);
        File.Exists(saveStates.GetSaveStatePath(rom, 0)).Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_CompressesPayloadOffTheCallerThread()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        SaveStateFileService saveStates = new(
            tempDirectory.Path,
            NullLogger<SaveStateFileService>.Instance
        );
        var rom = RomStorageIdentity.Create("Test Rom", [0x01, 0x02]);
        using var payload = new ObservingMemoryManager([0x10, 0x20, 0x30]);
        var payloadMemory = payload.Payload;
        var callerThreadId = 0;

        var save = Task
            .Factory.StartNew(
                () =>
                {
                    callerThreadId = Environment.CurrentManagedThreadId;
                    return saveStates.SaveAsync(
                        rom,
                        0,
                        HardwareModel.Dmg,
                        payloadMemory,
                        CancellationToken.None
                    );
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default
            )
            .Unwrap();

        await save;

        payload.FirstSpanAccessThreadId.Should().NotBe(0);
        payload.FirstSpanAccessThreadId.Should().NotBe(callerThreadId);
    }

    private class ObservingMemoryManager(byte[] buffer) : MemoryManager<byte>
    {
        private int _firstSpanAccessThreadId;
        private int _spanAccessCount;

        public ReadOnlyMemory<byte> Payload => CreateMemory(buffer.Length);

        public int FirstSpanAccessThreadId => Volatile.Read(ref _firstSpanAccessThreadId);

        public int SpanAccessCount => Volatile.Read(ref _spanAccessCount);

        public override Span<byte> GetSpan()
        {
            Interlocked.CompareExchange(
                ref _firstSpanAccessThreadId,
                Environment.CurrentManagedThreadId,
                comparand: 0
            );
            Interlocked.Increment(ref _spanAccessCount);
            return buffer;
        }

        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();

        public override void Unpin() { }

        protected override void Dispose(bool disposing) { }
    }

    private sealed class BlockingMemoryManager(byte[] buffer, CancellationToken cancellationToken)
        : ObservingMemoryManager(buffer)
    {
        private readonly ManualResetEventSlim _accessed = new(initialState: false);
        private readonly ManualResetEventSlim _release = new(initialState: false);
        private int _waitForFirstAccess = 1;

        public void WaitUntilAccess(CancellationToken waitCancellationToken) =>
            _accessed.Wait(waitCancellationToken);

        public void Release() => _release.Set();

        public override Span<byte> GetSpan()
        {
            _accessed.Set();
            if (Interlocked.Exchange(ref _waitForFirstAccess, 0) == 1)
            {
                _release.Wait(cancellationToken);
            }

            return base.GetSpan();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _accessed.Dispose();
                _release.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
