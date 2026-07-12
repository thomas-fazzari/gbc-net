// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Dma;
using GbcNet.Core.Memory;

namespace GbcNet.Tests.Dma;

public sealed class OamDmaControllerTests
{
    [Fact]
    public void StartOamTransfer_StoresSourceHighByteAndMarksActive()
    {
        var dma = new OamDmaController();

        dma.StartOamTransfer(0xC0);

        Assert.Equal(0xC0, dma.ReadRegister());
        Assert.True(dma.IsActive);
        Assert.False(dma.IsCpuOamBlocked);
    }

    [Fact]
    public void SetRegisterState_SeedsRegisterWithoutStartingTransfer()
    {
        var dma = new OamDmaController();
        var writes = new List<(ushort Address, byte Value)>();

        dma.SetRegisterState(0xFF);
        dma.Tick(
            160,
            ReadLowByte,
            (destinationAddress, copiedValue) => writes.Add((destinationAddress, copiedValue))
        );

        Assert.Equal(0xFF, dma.ReadRegister());
        Assert.False(dma.IsActive);
        Assert.Empty(writes);
    }

    [Fact]
    public void Tick_WaitsStartupDelayAfterTransferStart()
    {
        var dma = new OamDmaController();
        var writes = new List<(ushort Address, byte Value)>();

        dma.StartOamTransfer(0xC0);
        dma.Tick(
            1,
            ReadLowByte,
            (destinationAddress, copiedValue) => writes.Add((destinationAddress, copiedValue))
        );
        dma.Tick(
            1,
            ReadLowByte,
            (destinationAddress, copiedValue) => writes.Add((destinationAddress, copiedValue))
        );

        Assert.True(dma.IsActive);
        Assert.True(dma.IsCpuOamBlocked);
        Assert.Empty(writes);
    }

    [Fact]
    public void Tick_CopiesOneBytePerMachineCycleAfterStartupDelay()
    {
        var dma = new OamDmaController();
        var writes = new List<(ushort Address, byte Value)>();

        dma.StartOamTransfer(0xC0);
        dma.Tick(1, ReadLowByte, (address, value) => writes.Add((address, value)));
        dma.Tick(1, ReadLowByte, (address, value) => writes.Add((address, value)));
        dma.Tick(1, ReadLowByte, (address, value) => writes.Add((address, value)));

        var (destinationAddress, copiedValue) = Assert.Single(writes);
        Assert.Equal(AddressMap.ObjectAttributeMemoryStart, destinationAddress);
        Assert.Equal(0x00, copiedValue);
    }

    [Fact]
    public void Tick_CopiesPartialTransfer()
    {
        var dma = new OamDmaController();
        var writes = new List<(ushort Address, byte Value)>();

        dma.StartOamTransfer(0xC0);
        dma.Tick(
            2,
            ReadLowByte,
            (destinationAddress, copiedValue) => writes.Add((destinationAddress, copiedValue))
        );
        dma.Tick(
            3,
            ReadLowByte,
            (destinationAddress, copiedValue) => writes.Add((destinationAddress, copiedValue))
        );

        (ushort Address, byte Value)[] expectedWrites =
        [
            (0xFE00, 0x00),
            (0xFE01, 0x01),
            (0xFE02, 0x02),
        ];
        Assert.Equal(expectedWrites, writes);
    }

    [Fact]
    public void Tick_CompletesTransferAfterOneHundredSixtyCopiedBytes()
    {
        var dma = new OamDmaController();
        var writes = new List<(ushort Address, byte Value)>();

        dma.StartOamTransfer(0xC0);
        dma.Tick(
            2,
            ReadLowByte,
            (destinationAddress, copiedValue) => writes.Add((destinationAddress, copiedValue))
        );
        dma.Tick(
            1,
            ReadLowByte,
            (destinationAddress, copiedValue) => writes.Add((destinationAddress, copiedValue))
        );
        dma.Tick(
            159,
            ReadLowByte,
            (destinationAddress, copiedValue) => writes.Add((destinationAddress, copiedValue))
        );
        dma.Tick(
            1,
            ReadLowByte,
            (destinationAddress, copiedValue) => writes.Add((destinationAddress, copiedValue))
        );

        Assert.Equal(160, writes.Count);
        Assert.Equal((AddressMap.ObjectAttributeMemoryStart, (byte)0x00), writes[0]);
        Assert.Equal((AddressMap.ObjectAttributeMemoryEnd, (byte)0x9F), writes[^1]);
        Assert.False(dma.IsActive);
    }

    [Fact]
    public void StartOamTransfer_DelaysRestartWhilePreviousTransferKeepsRunning()
    {
        var dma = new OamDmaController();
        var writes = new List<(ushort Address, byte Value)>();

        dma.StartOamTransfer(0xC0);
        dma.Tick(
            2,
            ReadSourceHighByte,
            (destinationAddress, copiedValue) => writes.Add((destinationAddress, copiedValue))
        );
        dma.Tick(
            1,
            ReadSourceHighByte,
            (destinationAddress, copiedValue) => writes.Add((destinationAddress, copiedValue))
        );

        dma.StartOamTransfer(0xD0);
        dma.Tick(
            1,
            ReadSourceHighByte,
            (destinationAddress, copiedValue) => writes.Add((destinationAddress, copiedValue))
        );
        dma.Tick(
            1,
            ReadSourceHighByte,
            (destinationAddress, copiedValue) => writes.Add((destinationAddress, copiedValue))
        );
        dma.Tick(
            1,
            ReadSourceHighByte,
            (destinationAddress, copiedValue) => writes.Add((destinationAddress, copiedValue))
        );

        (ushort Address, byte Value)[] expectedWrites =
        [
            (AddressMap.ObjectAttributeMemoryStart, 0xC0),
            (AddressMap.ObjectAttributeMemoryStart + 1, 0xC0),
            (AddressMap.ObjectAttributeMemoryStart + 2, 0xC0),
            (AddressMap.ObjectAttributeMemoryStart, 0xD0),
        ];
        Assert.Equal(expectedWrites, writes);
    }

    [Fact]
    public void StartOamTransfer_PendingRestartStartsAfterCurrentTransferCompletesNearEnd()
    {
        var dma = new OamDmaController();
        var writes = new List<(ushort Address, byte Value)>();

        dma.StartOamTransfer(0xC0);
        dma.Tick(
            2,
            ReadSourceHighByte,
            (destinationAddress, copiedValue) => writes.Add((destinationAddress, copiedValue))
        );
        dma.Tick(
            159,
            ReadSourceHighByte,
            (destinationAddress, copiedValue) => writes.Add((destinationAddress, copiedValue))
        );

        dma.StartOamTransfer(0xD0);
        dma.Tick(
            1,
            ReadSourceHighByte,
            (destinationAddress, copiedValue) => writes.Add((destinationAddress, copiedValue))
        );
        Assert.False(dma.IsActive);

        dma.Tick(
            1,
            ReadSourceHighByte,
            (destinationAddress, copiedValue) => writes.Add((destinationAddress, copiedValue))
        );
        Assert.True(dma.IsActive);
        dma.Tick(
            1,
            ReadSourceHighByte,
            (destinationAddress, copiedValue) => writes.Add((destinationAddress, copiedValue))
        );

        Assert.Equal(161, writes.Count);
        Assert.Equal((AddressMap.ObjectAttributeMemoryEnd, (byte)0xC0), writes[159]);
        Assert.Equal((AddressMap.ObjectAttributeMemoryStart, (byte)0xD0), writes[160]);
    }

    [Fact]
    public void CaptureRestoreState_ResumesAfterRemainingStartupDelay()
    {
        var source = new OamDmaController();
        source.StartOamTransfer(0xC0);
        source.Tick(1, ReadSourceHighByte, (_, _) => throw new InvalidOperationException());

        var restored = new OamDmaController();
        var writes = new List<(ushort Address, byte Value)>();
        restored.RestoreState(source.CaptureState());

        Assert.Equal(0xC0, restored.ReadRegister());
        Assert.True(restored.IsActive);
        Assert.False(restored.IsCpuOamBlocked);
        Assert.Empty(writes);

        restored.Tick(1, ReadSourceHighByte, (address, value) => writes.Add((address, value)));
        Assert.Empty(writes);

        restored.Tick(1, ReadSourceHighByte, (address, value) => writes.Add((address, value)));
        Assert.Equal([(AddressMap.ObjectAttributeMemoryStart, (byte)0xC0)], writes);
    }

    [Fact]
    public void CaptureRestoreState_ResumesPendingRestartFromOldSourceThenRestartsAtOamStart()
    {
        var source = new OamDmaController();
        source.StartOamTransfer(0xC0);
        source.Tick(2, ReadSourceHighByte, (_, _) => throw new InvalidOperationException());
        source.Tick(1, ReadSourceHighByte, (_, _) => { });
        source.StartOamTransfer(0xD0);
        source.Tick(1, ReadSourceHighByte, (_, _) => { });

        var restored = new OamDmaController();
        var writes = new List<(ushort Address, byte Value)>();
        restored.RestoreState(source.CaptureState());

        Assert.Empty(writes);
        Assert.Equal(0xD0, restored.ReadRegister());
        Assert.True(restored.IsActive);
        Assert.True(restored.IsCpuOamBlocked);
        Assert.True(restored.TryGetCpuConflictSourceAddress(out var conflictSourceAddress));
        Assert.Equal(0xC001, conflictSourceAddress);

        restored.Tick(1, ReadSourceHighByte, (address, value) => writes.Add((address, value)));
        Assert.Equal([(AddressMap.ObjectAttributeMemoryStart + 2, (byte)0xC0)], writes);
        Assert.True(restored.IsActive);

        restored.Tick(1, ReadSourceHighByte, (address, value) => writes.Add((address, value)));
        Assert.Equal(
            [
                (AddressMap.ObjectAttributeMemoryStart + 2, (byte)0xC0),
                (AddressMap.ObjectAttributeMemoryStart, (byte)0xD0),
            ],
            writes
        );
    }

    [Fact]
    public void RestoreState_RejectsActiveTransferAtCompletedOffset()
    {
        var dma = new OamDmaController();
        var state = dma.CaptureState() with { NextOffset = 0xA0, IsActive = true };

        Assert.Throws<ArgumentException>(() => dma.RestoreState(state));
    }

    private static byte ReadLowByte(ushort address) => (byte)address;

    private static byte ReadSourceHighByte(ushort address) => (byte)(address >> 8);
}
