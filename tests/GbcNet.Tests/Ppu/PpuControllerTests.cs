using GbcNet.Core.Interrupts;
using GbcNet.Core.Memory;
using GbcNet.Core.Ppu;

namespace GbcNet.Tests.Ppu;

public sealed class PpuControllerTests
{
    private const byte LcdEnable = 0x80;
    private const byte StatusModeMask = 0x03;
    private const byte LcdInterruptMask = 0b0000_0010;
    private const byte VBlankInterruptMask = 0b0000_0001;
    private const byte LcdYCompareStatusMask = 0x04;

    public static TheoryData<ushort, byte> ReadWriteRegisters =>
        new()
        {
            { AddressMap.LcdControlRegister, 0x91 },
            { AddressMap.ScrollYRegister, 0x12 },
            { AddressMap.ScrollXRegister, 0x34 },
            { AddressMap.LcdYCompareRegister, 0x56 },
            { AddressMap.BackgroundPaletteRegister, 0xFC },
            { AddressMap.ObjectPalette0Register, 0xA5 },
            { AddressMap.ObjectPalette1Register, 0x5A },
            { AddressMap.WindowYRegister, 0x78 },
            { AddressMap.WindowXRegister, 0x9A },
        };

    [Theory]
    [MemberData(nameof(ReadWriteRegisters))]
    public void WriteRegister_StoresReadWriteRegisters(ushort address, byte value)
    {
        var ppu = new PpuController(new InterruptController());

        ppu.WriteRegister(address, value);

        Assert.Equal(value, ppu.ReadRegister(address));
    }

    [Fact]
    public void ReadRegister_ReturnsStatusReadMaskAndPpuState()
    {
        var ppu = new PpuController(new InterruptController());
        ppu.SetRegisterState(AddressMap.LcdControlRegister, LcdEnable);

        ppu.SetRegisterState(AddressMap.LcdStatusRegister, 0x85);

        Assert.Equal(0x85, ppu.ReadRegister(AddressMap.LcdStatusRegister));
    }

    [Fact]
    public void WriteRegister_UpdatesOnlyStatusInterruptSelectBits()
    {
        var ppu = new PpuController(new InterruptController());
        ppu.SetRegisterState(AddressMap.LcdControlRegister, LcdEnable);
        ppu.SetRegisterState(AddressMap.LcdStatusRegister, 0x85);

        ppu.WriteRegister(AddressMap.LcdStatusRegister, 0x78);

        Assert.Equal(0xFD, ppu.ReadRegister(AddressMap.LcdStatusRegister));
    }

    [Fact]
    public void WriteRegister_IgnoresLcdYCoordinateWrites()
    {
        var ppu = new PpuController(new InterruptController());
        ppu.SetRegisterState(AddressMap.LcdYCoordinateRegister, 0x42);

        ppu.WriteRegister(AddressMap.LcdYCoordinateRegister, 0x99);

        Assert.Equal(0x42, ppu.ReadRegister(AddressMap.LcdYCoordinateRegister));
    }

    [Fact]
    public void Tick_AdvancesVisibleScanlineModes()
    {
        var ppu = new PpuController(new InterruptController());
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);

        ppu.Tick(79);
        Assert.Equal(0x02, ppu.ReadRegister(AddressMap.LcdStatusRegister) & StatusModeMask);

        ppu.Tick(1);
        Assert.Equal(0x03, ppu.ReadRegister(AddressMap.LcdStatusRegister) & StatusModeMask);

        ppu.Tick(172);
        Assert.Equal(0x00, ppu.ReadRegister(AddressMap.LcdStatusRegister) & StatusModeMask);

        ppu.Tick(204);
        Assert.Equal(0x01, ppu.ReadRegister(AddressMap.LcdYCoordinateRegister));
        Assert.Equal(0x02, ppu.ReadRegister(AddressMap.LcdStatusRegister) & StatusModeMask);
    }

    [Fact]
    public void AccessProperties_ReflectCurrentPpuMode()
    {
        var ppu = new PpuController(new InterruptController());

        Assert.True(ppu.CanCpuAccessVideoRam);
        Assert.True(ppu.CanCpuAccessObjectAttributeMemory);

        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);
        Assert.True(ppu.CanCpuAccessVideoRam);
        Assert.False(ppu.CanCpuAccessObjectAttributeMemory);

        ppu.Tick(80);
        Assert.False(ppu.CanCpuAccessVideoRam);
        Assert.False(ppu.CanCpuAccessObjectAttributeMemory);

        ppu.Tick(172);
        Assert.True(ppu.CanCpuAccessVideoRam);
        Assert.True(ppu.CanCpuAccessObjectAttributeMemory);
    }

    [Fact]
    public void Tick_RequestsVBlankInterruptWhenEnteringLineOneHundredFortyFour()
    {
        var interrupts = new InterruptController();
        var ppu = new PpuController(interrupts);
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);

        ppu.Tick(456 * 144);

        Assert.Equal(144, ppu.ReadRegister(AddressMap.LcdYCoordinateRegister));
        Assert.Equal(0x01, ppu.ReadRegister(AddressMap.LcdStatusRegister) & StatusModeMask);
        Assert.Equal(VBlankInterruptMask, interrupts.InterruptFlag);
    }

    [Fact]
    public void Tick_WrapsLyAfterLineOneHundredFiftyThree()
    {
        var ppu = new PpuController(new InterruptController());
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);

        ppu.Tick(456 * 154);

        Assert.Equal(0x00, ppu.ReadRegister(AddressMap.LcdYCoordinateRegister));
        Assert.Equal(0x02, ppu.ReadRegister(AddressMap.LcdStatusRegister) & StatusModeMask);
    }

    [Fact]
    public void Tick_DoesNotAdvanceWhenLcdIsDisabled()
    {
        var ppu = new PpuController(new InterruptController());

        ppu.Tick(456 * 154);

        Assert.Equal(0x00, ppu.ReadRegister(AddressMap.LcdYCoordinateRegister));
        Assert.Equal(0x00, ppu.ReadRegister(AddressMap.LcdStatusRegister) & StatusModeMask);
    }

    [Fact]
    public void WriteRegister_DisablingLcdResetsTiming()
    {
        var ppu = new PpuController(new InterruptController());
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);
        ppu.Tick(456 * 3);

        ppu.WriteRegister(AddressMap.LcdControlRegister, 0x00);

        Assert.Equal(0x00, ppu.ReadRegister(AddressMap.LcdYCoordinateRegister));
        Assert.Equal(0x00, ppu.ReadRegister(AddressMap.LcdStatusRegister) & StatusModeMask);
    }

    [Fact]
    public void WriteRegister_StatusEnableRequestsLcdInterruptOnRisingStatLine()
    {
        var interrupts = new InterruptController();
        var ppu = new PpuController(interrupts);
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);

        ppu.WriteRegister(AddressMap.LcdStatusRegister, 0x20);

        Assert.Equal(LcdInterruptMask, interrupts.InterruptFlag);
    }

    [Fact]
    public void Tick_RequestsLcdInterruptOnlyOnStatLineRisingEdge()
    {
        var interrupts = new InterruptController();
        var ppu = new PpuController(interrupts);
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);
        ppu.WriteRegister(AddressMap.LcdStatusRegister, 0x08);

        ppu.Tick(252);
        Assert.Equal(LcdInterruptMask, interrupts.InterruptFlag);

        interrupts.Clear(InterruptSource.Lcd);
        ppu.Tick(10);

        Assert.Equal(0x00, interrupts.InterruptFlag);
    }

    [Fact]
    public void WriteRegister_LycCompareUpdatesStatusAndRequestsLcdInterruptOnRisingStatLine()
    {
        var interrupts = new InterruptController();
        var ppu = new PpuController(interrupts);
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);
        ppu.WriteRegister(AddressMap.LcdYCompareRegister, 0x01);
        ppu.WriteRegister(AddressMap.LcdStatusRegister, 0x40);

        ppu.WriteRegister(AddressMap.LcdYCompareRegister, 0x00);

        Assert.Equal(
            LcdYCompareStatusMask,
            ppu.ReadRegister(AddressMap.LcdStatusRegister) & LcdYCompareStatusMask
        );
        Assert.Equal(LcdInterruptMask, interrupts.InterruptFlag);
    }
}
