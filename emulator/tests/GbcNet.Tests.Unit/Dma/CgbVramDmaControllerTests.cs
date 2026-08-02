// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Dma;
using GbcNet.Core.Memory;

namespace GbcNet.Tests.Unit.Dma;

public sealed class CgbVramDmaControllerTests
{
    [Fact]
    public void CaptureRestore_HblankDmaContinuesFromNextBlockAndPreservesQueuedStalls()
    {
        var sourceReads = new List<ushort>();
        var destinationWrites = new List<(ushort Address, byte Value)>();
        var dma = CreateController(sourceReads, destinationWrites);

        StartHblankDma(dma, blockCountMinusOne: 1);
        dma.TransferHBlankBlock();
        dma.TryConsumeCpuStallMachineCycle().Should().BeTrue();
        dma.TryConsumeCpuStallMachineCycle().Should().BeTrue();
        dma.TryConsumeCpuStallMachineCycle().Should().BeTrue();

        var state = dma.CaptureState();
        sourceReads.Clear();
        destinationWrites.Clear();

        var restored = CreateController(sourceReads, destinationWrites);
        restored.RestoreState(state);
        restored.TransferHBlankBlock();

        sourceReads
            .Should()
            .Equal(Enumerable.Range(0, 0x10).Select(offset => (ushort)(0xC010 + offset)));
        destinationWrites
            .Should()
            .Equal(
                Enumerable
                    .Range(0, 0x10)
                    .Select(offset => ((ushort)(0x8010 + offset), (byte)(0x10 + offset)))
            );
        restored.ReadHdmaRegister(AddressMap.VideoRamDmaLengthModeStartRegister).Should().Be(0xFF);

        for (var cycle = 0; cycle < 13; cycle++)
        {
            restored.TryConsumeCpuStallMachineCycle().Should().BeTrue();
        }

        restored.TryConsumeCpuStallMachineCycle().Should().BeFalse();
    }

    [Fact]
    public void CaptureRestore_ActiveHblankDmaStaysPausedWhileCpuIsHaltedThenResumes()
    {
        var sourceReads = new List<ushort>();
        var destinationWrites = new List<(ushort Address, byte Value)>();
        var dma = CreateController(sourceReads, destinationWrites);

        StartHblankDma(dma, blockCountMinusOne: 0);
        dma.SetCpuHalted(true);
        var state = dma.CaptureState();

        var restored = CreateController(sourceReads, destinationWrites);
        restored.RestoreState(state);
        restored.TransferHBlankBlock();

        sourceReads.Should().BeEmpty();
        destinationWrites.Should().BeEmpty();
        restored.ReadHdmaRegister(AddressMap.VideoRamDmaLengthModeStartRegister).Should().Be(0x00);

        restored.SetCpuHalted(value: false);
        restored.TransferHBlankBlock();

        sourceReads.Count.Should().Be(0x10);
        destinationWrites.Count.Should().Be(0x10);
        restored.ReadHdmaRegister(AddressMap.VideoRamDmaLengthModeStartRegister).Should().Be(0xFF);
    }

    [Fact]
    public void CaptureRestore_CancelledHblankDmaRetainsRemainingCountAndDoesNotCopyAgain()
    {
        var sourceReads = new List<ushort>();
        var destinationWrites = new List<(ushort Address, byte Value)>();
        var dma = CreateController(sourceReads, destinationWrites);

        StartHblankDma(dma, blockCountMinusOne: 2);
        dma.TransferHBlankBlock();
        dma.WriteHdmaRegister(AddressMap.VideoRamDmaLengthModeStartRegister, 0x00);
        var state = dma.CaptureState();
        sourceReads.Clear();
        destinationWrites.Clear();

        var restored = CreateController(sourceReads, destinationWrites);
        restored.RestoreState(state);
        restored.TransferHBlankBlock();

        restored.ReadHdmaRegister(AddressMap.VideoRamDmaLengthModeStartRegister).Should().Be(0x81);
        sourceReads.Should().BeEmpty();
        destinationWrites.Should().BeEmpty();
    }

    [Fact]
    public void RestoreState_RejectsActiveHblankDmaWithoutRemainingBlocks()
    {
        var dma = CreateController([], []);
        var state = dma.CaptureState() with { IsHblankDmaActive = true };

        FluentActions
            .Invoking(() => dma.RestoreState(state))
            .Should()
            .ThrowExactly<ArgumentException>();
    }

    [Fact]
    public void RestoreState_RejectsNonInertStateWhenRegistersAreDisabled()
    {
        var dma = CreateDisabledController();
        var state = dma.CaptureState() with { SourceHigh = 0xC0 };

        FluentActions
            .Invoking(() => dma.RestoreState(state))
            .Should()
            .ThrowExactly<ArgumentException>();
    }

    [Fact]
    public void RestoreState_AllowsCpuHaltedStateWhenRegistersAreDisabled()
    {
        var dma = CreateDisabledController();
        var state = dma.CaptureState() with { CpuHalted = true };

        dma.RestoreState(state);

        dma.CaptureState().Should().Be(state);
    }

    private static CgbVramDmaController CreateDisabledController() =>
        new(
            isRegisterEnabled: false,
            isDoubleSpeed: () => false,
            readSourceByte: _ => 0,
            writeDestinationByte: (_, _) => { }
        );

    private static CgbVramDmaController CreateController(
        List<ushort> sourceReads,
        List<(ushort Address, byte Value)> destinationWrites
    ) =>
        new(
            isRegisterEnabled: true,
            isDoubleSpeed: () => false,
            readSourceByte: address =>
            {
                sourceReads.Add(address);
                return (byte)address;
            },
            writeDestinationByte: (address, value) => destinationWrites.Add((address, value))
        );

    private static void StartHblankDma(CgbVramDmaController dma, byte blockCountMinusOne)
    {
        dma.WriteHdmaRegister(AddressMap.VideoRamDmaSourceHighRegister, 0xC0);
        dma.WriteHdmaRegister(AddressMap.VideoRamDmaSourceLowRegister, 0x00);
        dma.WriteHdmaRegister(AddressMap.VideoRamDmaDestinationHighRegister, 0x00);
        dma.WriteHdmaRegister(AddressMap.VideoRamDmaDestinationLowRegister, 0x00);
        dma.WriteHdmaRegister(
            AddressMap.VideoRamDmaLengthModeStartRegister,
            (byte)(0x80 | blockCountMinusOne)
        );
    }
}
