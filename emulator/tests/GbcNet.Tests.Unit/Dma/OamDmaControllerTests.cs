// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Dma;
using GbcNet.Core.Memory;

namespace GbcNet.Tests.Unit.Dma;

public sealed class OamDmaControllerTests
{
    [Fact]
    public void StartOamTransfer_StoresSourceHighByteAndMarksActive()
    {
        var dma = new OamDmaController();

        dma.StartOamTransfer(0xC0);

        dma.ReadRegister().Should().Be(0xC0);
        dma.IsActive.Should().BeTrue();
        dma.IsCpuOamBlocked.Should().BeFalse();
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

        dma.ReadRegister().Should().Be(0xFF);
        dma.IsActive.Should().BeFalse();
        writes.Should().BeEmpty();
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

        dma.IsActive.Should().BeTrue();
        dma.IsCpuOamBlocked.Should().BeTrue();
        writes.Should().BeEmpty();
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

        var (destinationAddress, copiedValue) = writes.Should().ContainSingle().Which;
        destinationAddress.Should().Be(AddressMap.ObjectAttributeMemoryStart);
        copiedValue.Should().Be(0x00);
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
        writes.Should().Equal(expectedWrites);
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

        writes.Count.Should().Be(160);
        writes[0].Should().Be((AddressMap.ObjectAttributeMemoryStart, 0x00));
        writes[^1].Should().Be((AddressMap.ObjectAttributeMemoryEnd, 0x9F));
        dma.IsActive.Should().BeFalse();
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
        writes.Should().Equal(expectedWrites);
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
        dma.IsActive.Should().BeFalse();

        dma.Tick(
            1,
            ReadSourceHighByte,
            (destinationAddress, copiedValue) => writes.Add((destinationAddress, copiedValue))
        );
        dma.IsActive.Should().BeTrue();
        dma.Tick(
            1,
            ReadSourceHighByte,
            (destinationAddress, copiedValue) => writes.Add((destinationAddress, copiedValue))
        );

        writes.Count.Should().Be(161);
        writes[159].Should().Be((AddressMap.ObjectAttributeMemoryEnd, 0xC0));
        writes[160].Should().Be((AddressMap.ObjectAttributeMemoryStart, 0xD0));
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

        restored.ReadRegister().Should().Be(0xC0);
        restored.IsActive.Should().BeTrue();
        restored.IsCpuOamBlocked.Should().BeFalse();
        writes.Should().BeEmpty();

        restored.Tick(1, ReadSourceHighByte, (address, value) => writes.Add((address, value)));
        writes.Should().BeEmpty();

        restored.Tick(1, ReadSourceHighByte, (address, value) => writes.Add((address, value)));
        writes.Should().Equal((AddressMap.ObjectAttributeMemoryStart, 0xC0));
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

        writes.Should().BeEmpty();
        restored.ReadRegister().Should().Be(0xD0);
        restored.IsActive.Should().BeTrue();
        restored.IsCpuOamBlocked.Should().BeTrue();
        restored.TryGetCpuConflictSourceAddress(out var conflictSourceAddress).Should().BeTrue();
        conflictSourceAddress.Should().Be(0xC001);

        restored.Tick(1, ReadSourceHighByte, (address, value) => writes.Add((address, value)));
        writes.Should().Equal((AddressMap.ObjectAttributeMemoryStart + 2, 0xC0));
        restored.IsActive.Should().BeTrue();

        restored.Tick(1, ReadSourceHighByte, (address, value) => writes.Add((address, value)));
        writes
            .Should()
            .Equal(
                (AddressMap.ObjectAttributeMemoryStart + 2, 0xC0),
                (AddressMap.ObjectAttributeMemoryStart, 0xD0)
            );
    }

    [Fact]
    public void RestoreState_RejectsActiveTransferAtCompletedOffset()
    {
        var dma = new OamDmaController();
        var state = dma.CaptureState() with { NextOffset = 0xA0, IsActive = true };

        FluentActions
            .Invoking(() => dma.RestoreState(state))
            .Should()
            .ThrowExactly<ArgumentException>();
    }

    private static byte ReadLowByte(ushort address) => (byte)address;

    private static byte ReadSourceHighByte(ushort address) => (byte)(address >> 8);
}
