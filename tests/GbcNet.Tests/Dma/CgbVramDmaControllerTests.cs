// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Dma;
using GbcNet.Core.Memory;

namespace GbcNet.Tests.Dma;

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
        Assert.True(dma.TryConsumeCpuStallMachineCycle());
        Assert.True(dma.TryConsumeCpuStallMachineCycle());
        Assert.True(dma.TryConsumeCpuStallMachineCycle());

        var state = dma.CaptureState();
        sourceReads.Clear();
        destinationWrites.Clear();

        var restored = CreateController(sourceReads, destinationWrites);
        restored.RestoreState(state);
        restored.TransferHBlankBlock();

        Assert.Equal(
            Enumerable.Range(0, 0x10).Select(offset => (ushort)(0xC010 + offset)),
            sourceReads
        );
        Assert.Equal(
            Enumerable
                .Range(0, 0x10)
                .Select(offset => ((ushort)(0x8010 + offset), (byte)(0x10 + offset))),
            destinationWrites
        );
        Assert.Equal(
            0xFF,
            restored.ReadHdmaRegister(AddressMap.VideoRamDmaLengthModeStartRegister)
        );

        for (var cycle = 0; cycle < 13; cycle++)
        {
            Assert.True(restored.TryConsumeCpuStallMachineCycle());
        }

        Assert.False(restored.TryConsumeCpuStallMachineCycle());
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

        Assert.Empty(sourceReads);
        Assert.Empty(destinationWrites);
        Assert.Equal(
            0x00,
            restored.ReadHdmaRegister(AddressMap.VideoRamDmaLengthModeStartRegister)
        );

        restored.SetCpuHalted(value: false);
        restored.TransferHBlankBlock();

        Assert.Equal(0x10, sourceReads.Count);
        Assert.Equal(0x10, destinationWrites.Count);
        Assert.Equal(
            0xFF,
            restored.ReadHdmaRegister(AddressMap.VideoRamDmaLengthModeStartRegister)
        );
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

        Assert.Equal(
            0x81,
            restored.ReadHdmaRegister(AddressMap.VideoRamDmaLengthModeStartRegister)
        );
        Assert.Empty(sourceReads);
        Assert.Empty(destinationWrites);
    }

    [Fact]
    public void RestoreState_RejectsActiveHblankDmaWithoutRemainingBlocks()
    {
        var dma = CreateController([], []);
        var state = dma.CaptureState() with { IsHblankDmaActive = true };

        Assert.Throws<ArgumentException>(() => dma.RestoreState(state));
    }

    [Fact]
    public void RestoreState_RejectsNonInertStateWhenRegistersAreDisabled()
    {
        var dma = CreateDisabledController();
        var state = dma.CaptureState() with { SourceHigh = 0xC0 };

        Assert.Throws<ArgumentException>(() => dma.RestoreState(state));
    }

    [Fact]
    public void RestoreState_AllowsCpuHaltedStateWhenRegistersAreDisabled()
    {
        var dma = CreateDisabledController();
        var state = dma.CaptureState() with { CpuHalted = true };

        dma.RestoreState(state);

        Assert.Equal(state, dma.CaptureState());
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
