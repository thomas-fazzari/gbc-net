using GbcNet.Core.Interrupts;
using GbcNet.Core.Memory;
using GbcNet.Core.Ppu;
using GbcNet.Core.Ppu.Strategies;

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
        PpuController ppu = CreatePpu();

        ppu.WriteRegister(address, value);

        Assert.Equal(value, ppu.ReadRegister(address));
    }

    [Fact]
    public void ReadRegister_ReturnsStatusReadMaskAndPpuState()
    {
        PpuController ppu = CreatePpu();
        ppu.SetRegisterState(AddressMap.LcdControlRegister, LcdEnable);

        ppu.SetRegisterState(AddressMap.LcdStatusRegister, 0x85);

        Assert.Equal(0x85, ppu.ReadRegister(AddressMap.LcdStatusRegister));
    }

    [Fact]
    public void WriteRegister_UpdatesOnlyStatusInterruptSelectBits()
    {
        PpuController ppu = CreatePpu();
        ppu.SetRegisterState(AddressMap.LcdControlRegister, LcdEnable);
        ppu.SetRegisterState(AddressMap.LcdStatusRegister, 0x85);

        ppu.WriteRegister(AddressMap.LcdStatusRegister, 0x78);

        Assert.Equal(0xFD, ppu.ReadRegister(AddressMap.LcdStatusRegister));
    }

    [Fact]
    public void WriteRegister_IgnoresLcdYCoordinateWrites()
    {
        PpuController ppu = CreatePpu();
        ppu.SetRegisterState(AddressMap.LcdYCoordinateRegister, 0x42);

        ppu.WriteRegister(AddressMap.LcdYCoordinateRegister, 0x99);

        Assert.Equal(0x42, ppu.ReadRegister(AddressMap.LcdYCoordinateRegister));
    }

    [Fact]
    public void Tick_AdvancesVisibleScanlineModes()
    {
        PpuController ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);

        ppu.Tick(79);
        Assert.Equal(0x00, ppu.ReadRegister(AddressMap.LcdStatusRegister) & StatusModeMask);

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
        PpuController ppu = CreatePpu();

        Assert.True(ppu.CanCpuAccessVideoRam);
        Assert.True(ppu.CanCpuAccessObjectAttributeMemory);

        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);
        Assert.True(ppu.CanCpuAccessVideoRam);
        Assert.True(ppu.CanCpuAccessObjectAttributeMemory);

        ppu.Tick(80);
        Assert.False(ppu.CanCpuAccessVideoRam);
        Assert.False(ppu.CanCpuAccessObjectAttributeMemory);

        ppu.Tick(172);
        Assert.True(ppu.CanCpuAccessVideoRam);
        Assert.True(ppu.CanCpuAccessObjectAttributeMemory);

        ppu.Tick(204);
        Assert.True(ppu.CanCpuAccessVideoRam);
        Assert.False(ppu.CanCpuAccessObjectAttributeMemory);
    }

    [Fact]
    public void Tick_RequestsVBlankInterruptWhenEnteringLineOneHundredFortyFour()
    {
        var interrupts = new InterruptController();
        PpuController ppu = CreatePpu(interrupts);
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);

        ppu.Tick(456 * 144);

        Assert.Equal(144, ppu.ReadRegister(AddressMap.LcdYCoordinateRegister));
        Assert.Equal(0x01, ppu.ReadRegister(AddressMap.LcdStatusRegister) & StatusModeMask);
        Assert.Equal(VBlankInterruptMask, interrupts.InterruptFlag);
    }

    [Fact]
    public void Tick_WrapsLyAfterLineOneHundredFiftyThree()
    {
        PpuController ppu = CreatePpu();
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);

        ppu.Tick(456 * 154);

        Assert.Equal(0x00, ppu.ReadRegister(AddressMap.LcdYCoordinateRegister));
        Assert.Equal(0x02, ppu.ReadRegister(AddressMap.LcdStatusRegister) & StatusModeMask);
    }

    [Fact]
    public void Tick_DoesNotAdvanceWhenLcdIsDisabled()
    {
        PpuController ppu = CreatePpu();

        ppu.Tick(456 * 154);

        Assert.Equal(0x00, ppu.ReadRegister(AddressMap.LcdYCoordinateRegister));
        Assert.Equal(0x00, ppu.ReadRegister(AddressMap.LcdStatusRegister) & StatusModeMask);
    }

    [Fact]
    public void WriteRegister_DisablingLcdResetsTiming()
    {
        PpuController ppu = CreatePpu();
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
        PpuController ppu = CreatePpu(interrupts);
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);

        ppu.WriteRegister(AddressMap.LcdStatusRegister, 0x08);

        Assert.Equal(LcdInterruptMask, interrupts.InterruptFlag);
    }

    [Fact]
    public void Tick_RequestsLcdInterruptOnlyOnStatLineRisingEdge()
    {
        var interrupts = new InterruptController();
        PpuController ppu = CreatePpu(interrupts);
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
        PpuController ppu = CreatePpu(interrupts);
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

    [Fact]
    public void WriteRegister_RetainsLycCompareWhileLcdIsDisabled()
    {
        PpuController ppu = CreatePpu();
        ppu.SetRegisterState(AddressMap.LcdStatusRegister, 0x84);
        ppu.SetRegisterState(AddressMap.LcdYCoordinateRegister, 0x90);
        ppu.SetRegisterState(AddressMap.LcdYCompareRegister, 0x90);
        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);

        ppu.WriteRegister(AddressMap.LcdControlRegister, 0x00);
        ppu.WriteRegister(AddressMap.LcdYCompareRegister, 0x01);

        Assert.Equal(0x84, ppu.ReadRegister(AddressMap.LcdStatusRegister));
    }

    [Fact]
    public void WriteRegister_EnablingLcdUpdatesLycCompareAndRequestsRisingInterrupt()
    {
        var interrupts = new InterruptController();
        PpuController ppu = CreatePpu(interrupts);
        ppu.SetRegisterState(AddressMap.LcdStatusRegister, 0x80);
        ppu.WriteRegister(AddressMap.LcdStatusRegister, 0x40);

        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);

        Assert.Equal(
            LcdYCompareStatusMask,
            ppu.ReadRegister(AddressMap.LcdStatusRegister) & LcdYCompareStatusMask
        );
        Assert.Equal(LcdInterruptMask, interrupts.InterruptFlag);
    }

    [Fact]
    public void WriteRegister_EnablingLcdRequestsModeInterruptWhenLycCompareStaysSet()
    {
        var interrupts = new InterruptController();
        PpuController ppu = CreatePpu(interrupts);
        ppu.SetRegisterState(AddressMap.LcdStatusRegister, 0x84);
        ppu.WriteRegister(AddressMap.LcdStatusRegister, 0x48);

        ppu.WriteRegister(AddressMap.LcdControlRegister, LcdEnable);

        Assert.Equal(LcdInterruptMask, interrupts.InterruptFlag);
    }

    private static PpuController CreatePpu(InterruptController? interrupts = null) =>
        new(interrupts ?? new InterruptController(), new DmgPpuTimingStrategy());
}
