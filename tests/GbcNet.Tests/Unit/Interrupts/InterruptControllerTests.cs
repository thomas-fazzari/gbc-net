// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Interrupts;

namespace GbcNet.Tests.Unit.Interrupts;

public sealed class InterruptControllerTests
{
    [Fact]
    public void SetInterruptFlag_StoresOnlyRequestedInterruptBits()
    {
        var interrupts = new InterruptController();

        interrupts.SetInterruptFlag(0xFF);

        interrupts.InterruptFlag.Should().Be(0x1F);
        interrupts.ReadInterruptFlag().Should().Be(0xFF);
    }

    [Fact]
    public void ReadInterruptFlag_ReturnsUnusedBitsSet()
    {
        var interrupts = new InterruptController();

        interrupts.SetInterruptFlag(0x04);

        interrupts.ReadInterruptFlag().Should().Be(0xE4);
    }

    [Fact]
    public void RequestedAndEnabledMask_ReturnsIntersectionOfIeAndIfBits()
    {
        var interrupts = new InterruptController { InterruptEnable = 0b0001_0101 };
        interrupts.SetInterruptFlag(0b0000_0111);

        interrupts.RequestedAndEnabledMask.Should().Be(0b0000_0101);
        interrupts.HasRequestedAndEnabledInterrupt.Should().BeTrue();
    }

    [Fact]
    public void RequestAndClear_UpdateInterruptFlag()
    {
        var interrupts = new InterruptController();

        interrupts.Request(InterruptSource.Timer);
        interrupts.Request(InterruptSource.Joypad);
        interrupts.Clear(InterruptSource.Timer);

        interrupts.InterruptFlag.Should().Be(0b0001_0000);
    }

    [Fact]
    public void RestoreState_ResumesPriorityAndRetainsRemainingRequests()
    {
        var interrupts = new InterruptController { InterruptEnable = 0xFF };
        interrupts.SetInterruptFlag(0x1F);
        var state = interrupts.CaptureState();
        interrupts.InterruptEnable = 0;
        interrupts.SetInterruptFlag(0);

        interrupts.RestoreState(state);

        interrupts.InterruptEnable.Should().Be(0xFF);

        InterruptController
            .TryGetHighestPriority(
                interrupts.RequestedAndEnabledMask,
                out var source,
                out var vector
            )
            .Should()
            .BeTrue();
        source.Should().Be(InterruptSource.VBlank);
        vector.Should().Be(0x0040);

        interrupts.Clear(source);

        InterruptController
            .TryGetHighestPriority(interrupts.RequestedAndEnabledMask, out source, out vector)
            .Should()
            .BeTrue();
        source.Should().Be(InterruptSource.LcdStat);
        vector.Should().Be(0x0048);
    }

    [Fact]
    public void RestoreState_StoresOnlyRequestedInterruptFlagBits()
    {
        var interrupts = new InterruptController();

        interrupts.RestoreState(new InterruptControllerState(0xFF, 0xFF));

        interrupts.InterruptFlag.Should().Be(0x1F);
        interrupts.ReadInterruptFlag().Should().Be(0xFF);
    }

    [Fact]
    public void TryGetHighestPriority_ReturnsLowestPendingBitAndVector()
    {
        var found = InterruptController.TryGetHighestPriority(
            0b0001_0101,
            out var source,
            out var vector
        );

        found.Should().BeTrue();
        source.Should().Be(InterruptSource.VBlank);
        vector.Should().Be(0x0040);
    }

    [Fact]
    public void TryGetHighestPriority_ReturnsFalseWhenNoInterruptIsRequestedAndEnabled()
    {
        var found = InterruptController.TryGetHighestPriority(0, out var source, out var vector);

        found.Should().BeFalse();
        source.Should().Be(default(InterruptSource));
        vector.Should().Be(0);
    }

    [Theory]
    [InlineData(0, 0x0040)]
    [InlineData(1, 0x0048)]
    [InlineData(2, 0x0050)]
    [InlineData(3, 0x0058)]
    [InlineData(4, 0x0060)]
    public void TryGetHighestPriority_ReturnsHardwareVector(
        byte expectedSource,
        ushort expectedVector
    )
    {
        var interruptSource = (InterruptSource)expectedSource;

        var found = InterruptController.TryGetHighestPriority(
            (byte)(1 << expectedSource),
            out var source,
            out var vector
        );

        found.Should().BeTrue();
        source.Should().Be(interruptSource);
        vector.Should().Be(expectedVector);
    }
}
