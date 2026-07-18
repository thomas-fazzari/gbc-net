// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Saves;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Memory;
using GbcNet.Tests.Cartridges;

namespace GbcNet.Tests.App.Saves;

public sealed class CartridgeBatterySaveWriterTests
{
    [Fact]
    public async Task QueueSave_RunsInBackgroundAndKeepsLatestPendingSnapshot()
    {
        var cartridge = CreateBatteryBackedCartridge();
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x11);
        var releaseFirstWrite = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var firstWriteStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var writes = new List<byte[]>();
        CartridgeBatterySaveWriter writer = new(
            cartridge,
            async save =>
            {
                writes.Add(save.ToArray());
                if (writes.Count == 1)
                {
                    firstWriteStarted.SetResult();
                    await releaseFirstWrite.Task.ConfigureAwait(false);
                }
            },
            exception => Assert.Fail($"Unexpected error: {exception}")
        );

        writer.QueueSave();
        await firstWriteStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken
        );
        Assert.False(cartridge.IsBatterySaveDirty);

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x22);
        writer.QueueSave();
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x33);
        writer.QueueSave();
        var flush = writer.FlushAsync();

        Assert.False(flush.IsCompleted);
        releaseFirstWrite.SetResult();
        await flush.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Collection(
            writes,
            first => Assert.Equal(0x11, first[0]),
            latest => Assert.Equal(0x33, latest[0])
        );
    }

    [Fact]
    public async Task FlushAsync_CapturesFinalDirtyState()
    {
        var cartridge = CreateBatteryBackedCartridge();
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);
        byte[]? persisted = null;
        CartridgeBatterySaveWriter writer = new(
            cartridge,
            save =>
            {
                persisted = save.ToArray();
                return Task.CompletedTask;
            },
            exception => Assert.Fail($"Unexpected error: {exception}")
        );

        await writer
            .FlushAsync()
            .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.NotNull(persisted);
        Assert.Equal(0x42, persisted[0]);
        Assert.False(cartridge.IsBatterySaveDirty);
    }

    [Fact]
    public async Task FlushAsync_RetriesFailedSnapshot()
    {
        var cartridge = CreateBatteryBackedCartridge();
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x44);
        var failureReported = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var attempts = 0;
        byte[]? persisted = null;
        CartridgeBatterySaveWriter writer = new(
            cartridge,
            save =>
            {
                if (Interlocked.Increment(ref attempts) == 1)
                {
                    throw new IOException("synthetic write failure");
                }

                persisted = save.ToArray();
                return Task.CompletedTask;
            },
            exception => failureReported.SetResult(exception.Message)
        );

        writer.QueueSave();
        Assert.Equal(
            "synthetic write failure",
            await failureReported.Task.WaitAsync(
                TimeSpan.FromSeconds(1),
                TestContext.Current.CancellationToken
            )
        );
        await writer
            .FlushAsync()
            .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Equal(2, attempts);
        Assert.NotNull(persisted);
        Assert.Equal(0x44, persisted[0]);
    }

    [Fact]
    public async Task FlushAsync_PropagatesFinalWriteFailure()
    {
        var cartridge = CreateBatteryBackedCartridge();
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x55);
        var failureReported = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        CartridgeBatterySaveWriter writer = new(
            cartridge,
            _ => Task.FromException(new IOException("synthetic final write failure")),
            _ => failureReported.TrySetResult()
        );

        writer.QueueSave();
        await failureReported.Task.WaitAsync(
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken
        );

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            writer
                .FlushAsync()
                .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken)
        );

        Assert.Equal("synthetic final write failure", exception.Message);
    }

    private static Cartridge CreateBatteryBackedCartridge()
    {
        var cartridge = TestRomFactory.LoadCartridge(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc1RamBattery;
            bytes[0x0149] = 0x02;
        });
        cartridge.WriteRom(0x0000, 0x0A);
        return cartridge;
    }
}
