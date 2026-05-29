using GbcNet.Core.Interrupts;
using GbcNet.Core.Timers;

namespace GbcNet.Tests.Timers;

public sealed class TimerControllerTests
{
    private const byte TimerInterrupt = 0b0000_0100;

    [Fact]
    public void TickMachineCycle_AdvancesDividerVisibleByteEvery64MachineCycles()
    {
        var interrupts = new InterruptController();
        var timers = new TimerController(interrupts);

        TickMachineCycles(timers, 63);
        Assert.Equal(0x00, timers.ReadDivider());

        timers.TickMachineCycle();

        Assert.Equal(0x01, timers.ReadDivider());
    }

    [Fact]
    public void ResetDivider_ClearsSystemCounter()
    {
        var interrupts = new InterruptController();
        var timers = new TimerController(interrupts);
        TickMachineCycles(timers, 128);

        timers.ResetDivider();

        Assert.Equal(0x00, timers.ReadDivider());
    }

    [Fact]
    public void TickMachineCycle_DoesNotIncrementTimerCounterWhenTimerIsDisabled()
    {
        var interrupts = new InterruptController();
        var timers = new TimerController(interrupts);
        timers.WriteTimerControl(0b0000_0001);

        TickMachineCycles(timers, 256);

        Assert.Equal(0x00, timers.TimerCounter);
    }

    [Fact]
    public void TickMachineCycle_AdvancesDividerWhenTimerIsDisabled()
    {
        var interrupts = new InterruptController();
        var timers = new TimerController(interrupts);
        timers.WriteTimerControl(0b0000_0001);

        TickMachineCycles(timers, 128);

        Assert.Equal(0x02, timers.ReadDivider());
    }

    [Theory]
    [InlineData(0b0000_0100, 256)]
    [InlineData(0b0000_0101, 4)]
    [InlineData(0b0000_0110, 16)]
    [InlineData(0b0000_0111, 64)]
    public void TickMachineCycle_IncrementsTimerCounterAtSelectedCadence(
        byte timerControl,
        int machineCycles
    )
    {
        var interrupts = new InterruptController();
        var timers = new TimerController(interrupts);
        timers.WriteTimerControl(timerControl);

        TickMachineCycles(timers, machineCycles - 1);
        Assert.Equal(0x00, timers.TimerCounter);

        timers.TickMachineCycle();

        Assert.Equal(0x01, timers.TimerCounter);
    }

    [Fact]
    public void WriteTimerControl_UsesUpdatedClockSelectForSubsequentTicks()
    {
        var interrupts = new InterruptController();
        var timers = new TimerController(interrupts);
        timers.WriteTimerControl(0b0000_0101);
        TickMachineCycles(timers, 4);
        Assert.Equal(0x01, timers.TimerCounter);
        timers.ResetDivider();

        timers.WriteTimerControl(0b0000_0110);
        TickMachineCycles(timers, 15);
        Assert.Equal(0x01, timers.TimerCounter);

        timers.TickMachineCycle();

        Assert.Equal(0x02, timers.TimerCounter);
    }

    [Fact]
    public void ResetDivider_IncrementsTimerCounterWhenSelectedBitFalls()
    {
        var interrupts = new InterruptController();
        var timers = new TimerController(interrupts);
        timers.WriteTimerControl(0b0000_0101);
        TickMachineCycles(timers, 2);

        timers.ResetDivider();

        Assert.Equal(0x01, timers.TimerCounter);
    }

    [Fact]
    public void WriteTimerControl_IncrementsTimerCounterWhenSelectedBitFalls()
    {
        var interrupts = new InterruptController();
        var timers = new TimerController(interrupts);
        timers.WriteTimerControl(0b0000_0101);
        TickMachineCycles(timers, 2);

        timers.WriteTimerControl(0b0000_0110);

        Assert.Equal(0x01, timers.TimerCounter);
    }

    [Fact]
    public void WriteTimerControl_IncrementsTimerCounterWhenDisablingHighSelectedBit()
    {
        var interrupts = new InterruptController();
        var timers = new TimerController(interrupts);
        timers.WriteTimerControl(0b0000_0101);
        TickMachineCycles(timers, 2);

        timers.WriteTimerControl(0b0000_0001);

        Assert.Equal(0x01, timers.TimerCounter);
    }

    [Fact]
    public void TickMachineCycle_ReloadsTimerModuloAndRequestsTimerInterruptOneMachineCycleAfterOverflow()
    {
        var interrupts = new InterruptController();
        var timers = new TimerController(interrupts);
        timers.TimerCounter = 0xFF;
        timers.TimerModulo = 0x42;
        timers.WriteTimerControl(0b0000_0101);

        TickMachineCycles(timers, 4);

        Assert.Equal(0x00, timers.TimerCounter);
        Assert.Equal(0x00, interrupts.InterruptFlag);

        timers.TickMachineCycle();

        Assert.Equal(0x42, timers.TimerCounter);
        Assert.Equal(TimerInterrupt, interrupts.InterruptFlag);
    }

    [Fact]
    public void WriteTimerCounter_CancelsPendingOverflowReload()
    {
        var interrupts = new InterruptController();
        var timers = new TimerController(interrupts);
        timers.TimerCounter = 0xFF;
        timers.TimerModulo = 0x42;
        timers.WriteTimerControl(0b0000_0101);
        TickMachineCycles(timers, 4);

        timers.WriteTimerCounter(0x99);
        timers.TickMachineCycle();

        Assert.Equal(0x99, timers.TimerCounter);
        Assert.Equal(0x00, interrupts.InterruptFlag);
    }

    [Fact]
    public void WriteTimerCounter_DuringReloadMachineCycleIsIgnored()
    {
        var interrupts = new InterruptController();
        var timers = new TimerController(interrupts);
        timers.TimerCounter = 0xFF;
        timers.TimerModulo = 0x42;
        timers.WriteTimerControl(0b0000_0101);
        TickMachineCycles(timers, 5);

        timers.WriteTimerCounter(0x99);

        Assert.Equal(0x42, timers.TimerCounter);
    }

    [Fact]
    public void WriteTimerModulo_DuringPendingReloadUpdatesReloadedCounter()
    {
        var interrupts = new InterruptController();
        var timers = new TimerController(interrupts);
        timers.TimerCounter = 0xFF;
        timers.TimerModulo = 0x42;
        timers.WriteTimerControl(0b0000_0101);
        TickMachineCycles(timers, 4);

        timers.WriteTimerModulo(0x77);
        timers.TickMachineCycle();

        Assert.Equal(0x77, timers.TimerCounter);
        Assert.Equal(TimerInterrupt, interrupts.InterruptFlag);
    }

    [Fact]
    public void ReadTimerControl_ReturnsUnusedBitsSet()
    {
        var interrupts = new InterruptController();
        var timers = new TimerController(interrupts);

        timers.WriteTimerControl(0b0000_0101);

        Assert.Equal(0b1111_1101, timers.ReadTimerControl());
    }

    private static void TickMachineCycles(TimerController timers, int machineCycles)
    {
        for (int cycle = 0; cycle < machineCycles; cycle++)
        {
            timers.TickMachineCycle();
        }
    }
}
