// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers;
using GbcNet.App.Saves;
using GbcNet.Core.Hardware;
using Microsoft.Extensions.Logging.Abstractions;

namespace GbcNet.Tests.App.Saves;

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

        Assert.Null(saveStates.GetSaveStateDate(rom, 3));

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

        Assert.Equal([0x10, 0x20, 0x30], payload);
        Assert.Equal("TEST_ROM-", Path.GetFileName(saveStates.GetSaveStatePath(rom, 3))[..9]);
        Assert.NotNull(saveStates.GetSaveStateDate(rom, 3));
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

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            saveStates.LoadAsync(rom, 0, HardwareModel.Dmg, TestContext.Current.CancellationToken)
        );
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

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            saveStates.LoadAsync(rom, 0, HardwareModel.Dmg, TestContext.Current.CancellationToken)
        );
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

        var olderSave = Task.Run(
            () =>
                saveStates.SaveAsync(
                    rom,
                    0,
                    HardwareModel.Dmg,
                    olderPayload.Payload,
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
        Assert.Equal([0x20], payload);
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

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            saveStates.SaveAsync(rom, 0, HardwareModel.Dmg, payload.Payload, cancellation.Token)
        );

        Assert.Equal(0, payload.SpanAccessCount);
        Assert.False(File.Exists(saveStates.GetSaveStatePath(rom, 0)));
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
                        payload.Payload,
                        CancellationToken.None
                    );
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default
            )
            .Unwrap();

        await save;

        Assert.NotEqual(0, payload.FirstSpanAccessThreadId);
        Assert.NotEqual(callerThreadId, payload.FirstSpanAccessThreadId);
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

        public void WaitUntilAccess(CancellationToken cancellationToken) =>
            _accessed.Wait(cancellationToken);

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
