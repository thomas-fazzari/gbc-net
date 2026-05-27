using GbcNet.Core.Interrupts;
using GbcNet.Core.Timers;

namespace GbcNet.Tests.Timers;

public sealed class TimerControllerTests
{
    [Fact]
    public void Tick_AdvancesDividerVisibleByteEvery256TCycles()
    {
        var interrupts = new InterruptController();
        var timers = new TimerController(interrupts);

        timers.Tick(255);
        Assert.Equal(0x00, timers.ReadDivider());

        timers.Tick(1);

        Assert.Equal(0x01, timers.ReadDivider());
    }

    [Fact]
    public void ResetDivider_ClearsSystemCounter()
    {
        var interrupts = new InterruptController();
        var timers = new TimerController(interrupts);
        timers.Tick(512);

        timers.ResetDivider();

        Assert.Equal(0x00, timers.ReadDivider());
    }

    [Fact]
    public void Tick_DoesNotIncrementTimerCounterWhenTimerIsDisabled()
    {
        var interrupts = new InterruptController();
        var timers = new TimerController(interrupts);
        timers.WriteTimerControl(0b0000_0001);

        timers.Tick(1024);

        Assert.Equal(0x00, timers.TimerCounter);
    }

    [Theory]
    [InlineData(0b0000_0100, 1024)]
    [InlineData(0b0000_0101, 16)]
    [InlineData(0b0000_0110, 64)]
    [InlineData(0b0000_0111, 256)]
    public void Tick_IncrementsTimerCounterAtSelectedCadence(byte timerControl, int tCycles)
    {
        var interrupts = new InterruptController();
        var timers = new TimerController(interrupts);
        timers.WriteTimerControl(timerControl);

        timers.Tick(tCycles - 1);
        Assert.Equal(0x00, timers.TimerCounter);

        timers.Tick(1);

        Assert.Equal(0x01, timers.TimerCounter);
    }

    [Fact]
    public void Tick_ReloadsTimerModuloAndRequestsTimerInterruptOnOverflow()
    {
        var interrupts = new InterruptController();
        var timers = new TimerController(interrupts) { TimerCounter = 0xFF, TimerModulo = 0x42 };
        timers.WriteTimerControl(0b0000_0101);

        timers.Tick(16);

        Assert.Equal(0x42, timers.TimerCounter);
        Assert.Equal(0b0000_0100, interrupts.InterruptFlag);
    }

    [Fact]
    public void ReadTimerControl_ReturnsUnusedBitsSet()
    {
        var interrupts = new InterruptController();
        var timers = new TimerController(interrupts);

        timers.WriteTimerControl(0b0000_0101);

        Assert.Equal(0b1111_1101, timers.ReadTimerControl());
    }
}
