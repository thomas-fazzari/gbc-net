// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Dma;
using GbcNet.Core.Memory;

namespace GbcNet.Tests.Unit.Dma;

// Pan Docs `cgb-registers.md`: VRAM DMA transfers 16 bytes in 8 normal or 16 fast M-cycles.
public sealed class CgbVramDmaControllerTests
{
    [Fact]
    public void TickMachineCycle_TransfersGeneralDmaOnlyOverElapsedNormalSpeedCycles()
    {
        var sourceReads = new List<ushort>();
        var destinationWrites = new List<(ushort Address, byte Value)>();
        var dma = CreateController(sourceReads, destinationWrites);

        StartGeneralDma(dma);

        sourceReads.Should().BeEmpty();
        destinationWrites.Should().BeEmpty();
        dma.IsCpuStalled.Should().BeTrue();

        dma.TickMachineCycle();
        sourceReads.Should().BeEmpty();

        dma.TickMachineCycle();
        sourceReads.Should().Equal(0xC000, 0xC001);
        destinationWrites.Should().Equal((0x8000, 0x00), (0x8001, 0x01));

        TickMachineCycles(dma, 6);
        dma.IsCpuStalled.Should().BeTrue();
        sourceReads.Count.Should().Be(0x0E);

        dma.TickMachineCycle();
        dma.IsCpuStalled.Should().BeFalse();
        sourceReads.Count.Should().Be(0x10);
        dma.ReadHdmaRegister(AddressMap.VideoRamDmaLengthModeStartRegister).Should().Be(0xFF);
    }

    [Fact]
    public void TickMachineCycle_TransfersGeneralDmaAtOneBytePerDoubleSpeedCycle()
    {
        var sourceReads = new List<ushort>();
        var dma = CreateController(sourceReads, [], isDoubleSpeed: true);

        StartGeneralDma(dma);
        dma.TickMachineCycle();
        TickMachineCycles(dma, 15);

        sourceReads.Count.Should().Be(0x0F);
        dma.IsCpuStalled.Should().BeTrue();

        dma.TickMachineCycle();

        sourceReads.Count.Should().Be(0x10);
        dma.IsCpuStalled.Should().BeFalse();
    }

    [Fact]
    public void CaptureRestore_HblankDmaContinuesMidBlockWithoutCopyingAhead()
    {
        var sourceReads = new List<ushort>();
        var destinationWrites = new List<(ushort Address, byte Value)>();
        var dma = CreateController(sourceReads, destinationWrites);

        StartHblankDma(dma, blockCountMinusOne: 0);
        dma.BeginHBlankBlock();
        dma.TickMachineCycle();
        TickMachineCycles(dma, 3);

        sourceReads.Count.Should().Be(6);
        var state = dma.CaptureState();
        sourceReads.Clear();
        destinationWrites.Clear();

        var restored = CreateController(sourceReads, destinationWrites);
        restored.RestoreState(state);
        TickMachineCycles(restored, 5);

        sourceReads
            .Should()
            .Equal(Enumerable.Range(0x06, 0x0A).Select(offset => (ushort)(0xC000 + offset)));
        destinationWrites
            .Should()
            .Equal(
                Enumerable
                    .Range(0x06, 0x0A)
                    .Select(offset => ((ushort)(0x8000 + offset), (byte)offset))
            );
        restored.ReadHdmaRegister(AddressMap.VideoRamDmaLengthModeStartRegister).Should().Be(0xFF);
        restored.IsCpuStalled.Should().BeFalse();
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
        restored.BeginHBlankBlock();

        sourceReads.Should().BeEmpty();
        destinationWrites.Should().BeEmpty();
        restored.ReadHdmaRegister(AddressMap.VideoRamDmaLengthModeStartRegister).Should().Be(0x00);

        restored.SetCpuHalted(value: false);
        restored.BeginHBlankBlock();
        TickMachineCycles(restored, 9);

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
        dma.BeginHBlankBlock();
        TickMachineCycles(dma, 9);
        dma.WriteHdmaRegister(AddressMap.VideoRamDmaLengthModeStartRegister, 0x00);
        var state = dma.CaptureState();
        sourceReads.Clear();
        destinationWrites.Clear();

        var restored = CreateController(sourceReads, destinationWrites);
        restored.RestoreState(state);
        restored.BeginHBlankBlock();
        TickMachineCycles(restored, 9);

        restored.ReadHdmaRegister(AddressMap.VideoRamDmaLengthModeStartRegister).Should().Be(0x81);
        sourceReads.Should().BeEmpty();
        destinationWrites.Should().BeEmpty();
    }

    [Fact]
    public void RestoreState_RejectsActiveHblankDmaWithoutRemainingBlocks()
    {
        var dma = CreateController([], []);
        var state = dma.CaptureState() with { TransferMode = VramDmaTransferMode.HBlank };

        FluentActions
            .Invoking(() => dma.RestoreState(state))
            .Should()
            .ThrowExactly<ArgumentException>();
    }

    [Fact]
    public void RestoreState_RejectsInvalidTransferModeWithoutMutation()
    {
        var dma = CreateController([], []);
        var before = dma.CaptureState();
        var state = before with { TransferMode = (VramDmaTransferMode)int.MaxValue };

        FluentActions
            .Invoking(() => dma.RestoreState(state))
            .Should()
            .ThrowExactly<ArgumentOutOfRangeException>();
        dma.CaptureState().Should().Be(before);
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
        List<(ushort Address, byte Value)> destinationWrites,
        bool isDoubleSpeed = false
    ) =>
        new(
            isRegisterEnabled: true,
            isDoubleSpeed: () => isDoubleSpeed,
            readSourceByte: address =>
            {
                sourceReads.Add(address);
                return (byte)address;
            },
            writeDestinationByte: (address, value) => destinationWrites.Add((address, value))
        );

    private static void StartGeneralDma(CgbVramDmaController dma)
    {
        SetTransferAddresses(dma);
        dma.WriteHdmaRegister(AddressMap.VideoRamDmaLengthModeStartRegister, 0x00);
    }

    private static void StartHblankDma(CgbVramDmaController dma, byte blockCountMinusOne)
    {
        SetTransferAddresses(dma);
        dma.WriteHdmaRegister(
            AddressMap.VideoRamDmaLengthModeStartRegister,
            (byte)(0x80 | blockCountMinusOne)
        );
    }

    private static void SetTransferAddresses(CgbVramDmaController dma)
    {
        dma.WriteHdmaRegister(AddressMap.VideoRamDmaSourceHighRegister, 0xC0);
        dma.WriteHdmaRegister(AddressMap.VideoRamDmaSourceLowRegister, 0x00);
        dma.WriteHdmaRegister(AddressMap.VideoRamDmaDestinationHighRegister, 0x00);
        dma.WriteHdmaRegister(AddressMap.VideoRamDmaDestinationLowRegister, 0x00);
    }

    private static void TickMachineCycles(CgbVramDmaController dma, int machineCycles)
    {
        for (var cycle = 0; cycle < machineCycles; cycle++)
        {
            dma.TickMachineCycle();
        }
    }
}
