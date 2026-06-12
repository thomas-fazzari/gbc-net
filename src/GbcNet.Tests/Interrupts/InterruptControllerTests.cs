using GbcNet.Core.Interrupts;

namespace GbcNet.Tests.Interrupts;

public sealed class InterruptControllerTests
{
    [Fact]
    public void SetInterruptFlag_StoresOnlyRequestedInterruptBits()
    {
        var interrupts = new InterruptController();

        interrupts.SetInterruptFlag(0xFF);

        Assert.Equal(0x1F, interrupts.InterruptFlag);
        Assert.Equal(0xFF, interrupts.ReadInterruptFlag());
    }

    [Fact]
    public void ReadInterruptFlag_ReturnsUnusedBitsSet()
    {
        var interrupts = new InterruptController();

        interrupts.SetInterruptFlag(0x04);

        Assert.Equal(0xE4, interrupts.ReadInterruptFlag());
    }

    [Fact]
    public void RequestedAndEnabledMask_ReturnsIntersectionOfIeAndIfBits()
    {
        var interrupts = new InterruptController { InterruptEnable = 0b0001_0101 };
        interrupts.SetInterruptFlag(0b0000_0111);

        Assert.Equal(0b0000_0101, interrupts.RequestedAndEnabledMask);
        Assert.True(interrupts.HasRequestedAndEnabledInterrupt);
    }

    [Fact]
    public void RequestAndClear_UpdateInterruptFlag()
    {
        var interrupts = new InterruptController();

        interrupts.Request(InterruptSource.Timer);
        interrupts.Request(InterruptSource.Joypad);
        interrupts.Clear(InterruptSource.Timer);

        Assert.Equal(0b0001_0000, interrupts.InterruptFlag);
    }

    [Fact]
    public void TryGetHighestPriority_ReturnsLowestPendingBitAndVector()
    {
        var found = InterruptController.TryGetHighestPriority(
            0b0001_0101,
            out var source,
            out var vector
        );

        Assert.True(found);
        Assert.Equal(InterruptSource.VBlank, source);
        Assert.Equal(0x0040, vector);
    }

    [Fact]
    public void TryGetHighestPriority_ReturnsFalseWhenNoInterruptIsRequestedAndEnabled()
    {
        var found = InterruptController.TryGetHighestPriority(0, out var source, out var vector);

        Assert.False(found);
        Assert.Equal(default, source);
        Assert.Equal(0, vector);
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

        Assert.True(found);
        Assert.Equal(interruptSource, source);
        Assert.Equal(expectedVector, vector);
    }
}
