using GbcNet.Core.Clock;
using GbcNet.Core.Interrupts;
using GbcNet.Core.Timers;

namespace GbcNet.Tests.Timers;

public sealed class TimerControllerTests
{
    private const byte TimerInterrupt = 0b0000_0100;

    [Fact]
    public void TickMachineCycle_AdvancesDividerVisibleByteEvery64MachineCycles()
    {
        var (counter, timers) = CreateTimers();

        TickMachineCycles(counter, timers, 63);
        Assert.Equal(0x00, counter.ReadDivider());

        TickMachineCycles(counter, timers, 1);

        Assert.Equal(0x01, counter.ReadDivider());
    }

    [Fact]
    public void ResetDivider_ClearsSystemCounter()
    {
        var (counter, timers) = CreateTimers();
        TickMachineCycles(counter, timers, 128);

        ResetSystemCounter(counter, timers);

        Assert.Equal(0x00, counter.ReadDivider());
    }

    [Fact]
    public void TickMachineCycle_DoesNotIncrementTimerCounterWhenTimerIsDisabled()
    {
        var (counter, timers) = CreateTimers();
        timers.WriteTimerControl(0b0000_0001);

        TickMachineCycles(counter, timers, 256);

        Assert.Equal(0x00, timers.TimerCounter);
    }

    [Fact]
    public void TickMachineCycle_AdvancesDividerWhenTimerIsDisabled()
    {
        var (counter, timers) = CreateTimers();
        timers.WriteTimerControl(0b0000_0001);

        TickMachineCycles(counter, timers, 128);

        Assert.Equal(0x02, counter.ReadDivider());
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
        var (counter, timers) = CreateTimers();
        timers.WriteTimerControl(timerControl);

        TickMachineCycles(counter, timers, machineCycles - 1);
        Assert.Equal(0x00, timers.TimerCounter);

        TickMachineCycles(counter, timers, 1);

        Assert.Equal(0x01, timers.TimerCounter);
    }

    [Fact]
    public void WriteTimerControl_UsesUpdatedClockSelectForSubsequentTicks()
    {
        var (counter, timers) = CreateTimers();
        timers.WriteTimerControl(0b0000_0101);
        TickMachineCycles(counter, timers, 4);
        Assert.Equal(0x01, timers.TimerCounter);
        ResetSystemCounter(counter, timers);

        timers.WriteTimerControl(0b0000_0110);
        TickMachineCycles(counter, timers, 15);
        Assert.Equal(0x01, timers.TimerCounter);

        TickMachineCycles(counter, timers, 1);

        Assert.Equal(0x02, timers.TimerCounter);
    }

    [Fact]
    public void ResetDivider_IncrementsTimerCounterWhenSelectedBitFalls()
    {
        var (counter, timers) = CreateTimers();
        timers.WriteTimerControl(0b0000_0101);
        TickMachineCycles(counter, timers, 2);

        ResetSystemCounter(counter, timers);

        Assert.Equal(0x01, timers.TimerCounter);
    }

    [Fact]
    public void WriteTimerControl_IncrementsTimerCounterWhenSelectedBitFalls()
    {
        var (counter, timers) = CreateTimers();
        timers.WriteTimerControl(0b0000_0101);
        TickMachineCycles(counter, timers, 2);

        timers.WriteTimerControl(0b0000_0110);

        Assert.Equal(0x01, timers.TimerCounter);
    }

    [Fact]
    public void WriteTimerControl_IncrementsTimerCounterWhenDisablingHighSelectedBit()
    {
        var (counter, timers) = CreateTimers();
        timers.WriteTimerControl(0b0000_0101);
        TickMachineCycles(counter, timers, 2);

        timers.WriteTimerControl(0b0000_0001);

        Assert.Equal(0x01, timers.TimerCounter);
    }

    [Fact]
    public void TickMachineCycle_ReloadsTimerModuloAndRequestsTimerInterruptOneMachineCycleAfterOverflow()
    {
        var interrupts = new InterruptController();
        var (counter, timers) = CreateTimers(interrupts);
        timers.TimerCounter = 0xFF;
        timers.TimerModulo = 0x42;
        timers.WriteTimerControl(0b0000_0101);

        TickMachineCycles(counter, timers, 4);

        Assert.Equal(0x00, timers.TimerCounter);
        Assert.Equal(0x00, interrupts.InterruptFlag);

        TickMachineCycles(counter, timers, 1);

        Assert.Equal(0x42, timers.TimerCounter);
        Assert.Equal(TimerInterrupt, interrupts.InterruptFlag);
    }

    [Fact]
    public void WriteTimerCounter_CancelsPendingOverflowReload()
    {
        var interrupts = new InterruptController();
        var (counter, timers) = CreateTimers(interrupts);
        timers.TimerCounter = 0xFF;
        timers.TimerModulo = 0x42;
        timers.WriteTimerControl(0b0000_0101);
        TickMachineCycles(counter, timers, 4);

        timers.WriteTimerCounter(0x99);
        TickMachineCycles(counter, timers, 1);

        Assert.Equal(0x99, timers.TimerCounter);
        Assert.Equal(0x00, interrupts.InterruptFlag);
    }

    [Fact]
    public void WriteTimerCounter_DuringReloadMachineCycleIsIgnored()
    {
        var (counter, timers) = CreateTimers();
        timers.TimerCounter = 0xFF;
        timers.TimerModulo = 0x42;
        timers.WriteTimerControl(0b0000_0101);
        TickMachineCycles(counter, timers, 5);

        timers.WriteTimerCounter(0x99);

        Assert.Equal(0x42, timers.TimerCounter);
    }

    [Fact]
    public void WriteTimerModulo_DuringPendingReloadUpdatesReloadedCounter()
    {
        var interrupts = new InterruptController();
        var (counter, timers) = CreateTimers(interrupts);
        timers.TimerCounter = 0xFF;
        timers.TimerModulo = 0x42;
        timers.WriteTimerControl(0b0000_0101);
        TickMachineCycles(counter, timers, 4);

        timers.WriteTimerModulo(0x77);
        TickMachineCycles(counter, timers, 1);

        Assert.Equal(0x77, timers.TimerCounter);
        Assert.Equal(TimerInterrupt, interrupts.InterruptFlag);
    }

    [Fact]
    public void ReadTimerControl_ReturnsUnusedBitsSet()
    {
        var (_, timers) = CreateTimers();

        timers.WriteTimerControl(0b0000_0101);

        Assert.Equal(0b1111_1101, timers.ReadTimerControl());
    }

    private static (SystemCounter Counter, TimerController Timers) CreateTimers(
        InterruptController? interrupts = null
    )
    {
        var counter = new SystemCounter();
        return (counter, new TimerController(interrupts ?? new InterruptController(), counter));
    }

    private static void ResetSystemCounter(SystemCounter counter, TimerController timers)
    {
        timers.TickSystemCounter(counter.Reset());
    }

    private static void TickMachineCycles(
        SystemCounter counter,
        TimerController timers,
        int machineCycles
    )
    {
        for (var cycle = 0; cycle < machineCycles; cycle++)
        {
            timers.AdvanceReloadPipeline();
            timers.TickSystemCounter(counter.AdvanceMachineCycle());
        }
    }
}
