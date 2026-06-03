using GbcNet.Core;
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
    public void TickSystemCounter_ShiftsDisconnectedInputBitEvery128MachineCycles()
    {
        var counter = new SystemCounter();
        var serial = new SerialController(new InterruptController());
        serial.WriteControl(0x81);

        TickMachineCycles(counter, serial, 127);
        Assert.Equal(0x00, serial.TransferData);

        TickMachineCycles(counter, serial, 1);

        Assert.Equal(0x01, serial.TransferData);
    }

    [Fact]
    public void WriteControl_WhenMasterClockIsHigh_DelaysFirstShiftUntilNextLowEdge()
    {
        var counter = new SystemCounter();
        var serial = new SerialController(new InterruptController());
        TickMachineCycles(counter, serial, 64);

        serial.WriteControl(0x81);
        TickMachineCycles(counter, serial, 64);
        Assert.Equal(0x00, serial.TransferData);

        TickMachineCycles(counter, serial, 64);

        Assert.Equal(0x01, serial.TransferData);
    }

    [Fact]
    public void TickSystemCounter_CompletesInternalClockTransferAndRequestsSerialInterrupt()
    {
        var counter = new SystemCounter();
        var interrupts = new InterruptController();
        var serial = new SerialController(interrupts);
        byte? transferredByte = null;
        serial.ByteTransferred += (_, e) => transferredByte = e.Value;
        serial.TransferData = 0x41;
        serial.WriteControl(0x81);
        serial.TransferData = 0x00;

        TickMachineCycles(counter, serial, 128 * 8);

        Assert.Equal(0xFF, serial.TransferData);
        Assert.Equal(0x7F, serial.ReadControl());
        Assert.Equal(0b0000_1000, interrupts.InterruptFlag);
        Assert.Equal((byte)0x41, transferredByte);
    }

    [Fact]
    public void TickSystemCounter_DoesNotAdvanceExternalClockTransfer()
    {
        var counter = new SystemCounter();
        var interrupts = new InterruptController();
        var serial = new SerialController(interrupts);
        byte? transferredByte = null;
        serial.ByteTransferred += (_, e) => transferredByte = e.Value;
        serial.WriteControl(0x80);

        TickMachineCycles(counter, serial, 128 * 8);

        Assert.Equal(0x00, serial.TransferData);
        Assert.Equal(0xFE, serial.ReadControl());
        Assert.Equal(0x00, interrupts.InterruptFlag);
        Assert.Null(transferredByte);
    }

    [Fact]
    public void SetControlState_DoesNotStartTransfer()
    {
        var counter = new SystemCounter();
        var interrupts = new InterruptController();
        var serial = new SerialController(interrupts);
        byte? transferredByte = null;
        serial.ByteTransferred += (_, e) => transferredByte = e.Value;
        serial.SetControlState(0x81);

        TickMachineCycles(counter, serial, 128 * 8);

        Assert.Equal(0x00, serial.TransferData);
        Assert.Equal(0xFF, serial.ReadControl());
        Assert.Equal(0x00, interrupts.InterruptFlag);
        Assert.Null(transferredByte);
    }

    private static void TickMachineCycles(
        SystemCounter counter,
        SerialController serial,
        int machineCycles
    )
    {
        for (int cycle = 0; cycle < machineCycles; cycle++)
        {
            serial.TickSystemCounter(counter.AdvanceMachineCycle());
        }
    }
}
