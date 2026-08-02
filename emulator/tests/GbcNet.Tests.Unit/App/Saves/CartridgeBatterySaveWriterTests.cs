// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Saves;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Memory;

namespace GbcNet.Tests.Unit.App.Saves;

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
                    await releaseFirstWrite.Task;
                }
            },
            exception => exception.Should().BeNull($"Unexpected error: {exception}")
        );

        writer.QueueSave();
        await firstWriteStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken
        );
        cartridge.IsBatterySaveDirty.Should().BeFalse();

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x22);
        writer.QueueSave();
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x33);
        writer.QueueSave();
        var flush = writer.FlushAsync();

        flush.IsCompleted.Should().BeFalse();
        releaseFirstWrite.SetResult();
        await flush.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        writes
            .Should()
            .SatisfyRespectively(
                first => first[0].Should().Be(0x11),
                latest => latest[0].Should().Be(0x33)
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
            exception => exception.Should().BeNull($"Unexpected error: {exception}")
        );

        await writer
            .FlushAsync()
            .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        persisted.Should().NotBeNull();
        persisted[0].Should().Be(0x42);
        cartridge.IsBatterySaveDirty.Should().BeFalse();
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
        (
            await failureReported.Task.WaitAsync(
                TimeSpan.FromSeconds(1),
                TestContext.Current.CancellationToken
            )
        )
            .Should()
            .Be("synthetic write failure");
        await writer
            .FlushAsync()
            .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        attempts.Should().Be(2);
        persisted.Should().NotBeNull();
        persisted[0].Should().Be(0x44);
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

        var exception = (
            await FluentActions
                .Awaiting(() =>
                    writer
                        .FlushAsync()
                        .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken)
                )
                .Should()
                .ThrowExactlyAsync<IOException>()
        ).Which;

        exception.Message.Should().Be("synthetic final write failure");
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
