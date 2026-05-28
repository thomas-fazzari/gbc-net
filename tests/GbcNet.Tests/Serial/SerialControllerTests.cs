using GbcNet.Core.Interrupts;
using GbcNet.Core.Serial;

namespace GbcNet.Tests.Serial;

public sealed class SerialControllerTests
{
    [Fact]
    public void WriteControl_StoresUsefulDmgBitsAndReadsUnusedBitsSet()
    {
        var serial = new SerialController(new InterruptController());

        serial.WriteControl(0x81);

        Assert.Equal(0xFF, serial.ReadControl());
    }

    [Fact]
    public void Tick_ShiftsDisconnectedInputBitEvery512TCycles()
    {
        var serial = new SerialController(new InterruptController());
        serial.WriteControl(0x81);

        serial.Tick(511);
        Assert.Equal(0x00, serial.TransferData);

        serial.Tick(1);

        Assert.Equal(0x01, serial.TransferData);
    }

    [Fact]
    public void Tick_CompletesInternalClockTransferAndRequestsSerialInterrupt()
    {
        var interrupts = new InterruptController();
        var serial = new SerialController(interrupts);
        serial.WriteControl(0x81);

        serial.Tick(512 * 8);

        Assert.Equal(0xFF, serial.TransferData);
        Assert.Equal(0x7F, serial.ReadControl());
        Assert.Equal(0b0000_1000, interrupts.InterruptFlag);
    }

    [Fact]
    public void Tick_DoesNotAdvanceExternalClockTransfer()
    {
        var interrupts = new InterruptController();
        var serial = new SerialController(interrupts);
        serial.WriteControl(0x80);

        serial.Tick(512 * 8);

        Assert.Equal(0x00, serial.TransferData);
        Assert.Equal(0xFE, serial.ReadControl());
        Assert.Equal(0x00, interrupts.InterruptFlag);
    }

    [Fact]
    public void SetControlState_DoesNotStartTransfer()
    {
        var interrupts = new InterruptController();
        var serial = new SerialController(interrupts);
        serial.SetControlState(0x81);

        serial.Tick(512 * 8);

        Assert.Equal(0x00, serial.TransferData);
        Assert.Equal(0xFF, serial.ReadControl());
        Assert.Equal(0x00, interrupts.InterruptFlag);
    }
}
