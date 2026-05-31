using GbcNet.Core.Cartridges;
using GbcNet.Core.Joypad;
using GbcNet.Core.Memory;
using GbcNet.Tests.Cartridges;

namespace GbcNet.Tests.Memory;

public sealed class MemoryBusTests
{
    private const byte LcdEnable = 0x80;

    [Fact]
    public void ReadByte_RoutesRomWindowToCartridge()
    {
        byte[] rom = TestRomFactory.Create();
        rom[0x0000] = 0x11;
        rom[0x4000] = 0x22;
        rom[0x7FFF] = 0x33;
        MemoryBus bus = CreateBus(rom);

        Assert.Equal(0x11, bus.ReadByte(0x0000));
        Assert.Equal(0x22, bus.ReadByte(0x4000));
        Assert.Equal(0x33, bus.ReadByte(0x7FFF));
    }

    [Fact]
    public void WriteByte_IgnoresRomWindowForRomOnlyCartridge()
    {
        byte[] rom = TestRomFactory.Create();
        rom[0x0000] = 0x11;
        MemoryBus bus = CreateBus(rom);

        bus.WriteByte(0x0000, 0xAA);

        Assert.Equal(0x11, bus.ReadByte(0x0000));
    }

    [Fact]
    public void ReadWriteByte_StoresVideoRam()
    {
        MemoryBus bus = CreateBus();

        bus.WriteByte(0x8000, 0x12);
        bus.WriteByte(0x9FFF, 0x34);

        Assert.Equal(0x12, bus.ReadByte(0x8000));
        Assert.Equal(0x34, bus.ReadByte(0x9FFF));
    }

    [Fact]
    public void ReadWriteByte_StoresWorkRam()
    {
        MemoryBus bus = CreateBus();

        bus.WriteByte(0xC000, 0x56);
        bus.WriteByte(0xDFFF, 0x78);

        Assert.Equal(0x56, bus.ReadByte(0xC000));
        Assert.Equal(0x78, bus.ReadByte(0xDFFF));
    }

    [Fact]
    public void ReadWriteByte_MirrorsEchoRamToWorkRam()
    {
        MemoryBus bus = CreateBus();

        bus.WriteByte(0xC000, 0x9A);
        bus.WriteByte(0xFDFF, 0xBC);

        Assert.Equal(0x9A, bus.ReadByte(0xE000));
        Assert.Equal(0xBC, bus.ReadByte(0xDDFF));
    }

    [Fact]
    public void ReadWriteByte_StoresObjectAttributeMemory()
    {
        MemoryBus bus = CreateBus();

        bus.WriteByte(0xFE00, 0xDE);
        bus.WriteByte(0xFE9F, 0xF0);

        Assert.Equal(0xDE, bus.ReadByte(0xFE00));
        Assert.Equal(0xF0, bus.ReadByte(0xFE9F));
    }

    [Fact]
    public void ReadWriteByte_BlocksVideoRamDuringPpuDrawingMode()
    {
        MemoryBus bus = CreateBus();
        bus.WriteByte(AddressMap.VideoRamStart, 0x12);
        bus.WriteByte(AddressMap.LcdControlRegister, LcdEnable);
        bus.Ppu.Tick(80);

        Assert.Equal(0xFF, bus.ReadByte(AddressMap.VideoRamStart));

        bus.WriteByte(AddressMap.VideoRamStart, 0x34);
        bus.Ppu.Tick(172);

        Assert.Equal(0x12, bus.ReadByte(AddressMap.VideoRamStart));
    }

    [Fact]
    public void ReadWriteByte_BlocksObjectAttributeMemoryDuringPpuOamScanAndDrawingModes()
    {
        MemoryBus bus = CreateBus();
        bus.WriteByte(AddressMap.ObjectAttributeMemoryStart, 0x12);
        bus.WriteByte(AddressMap.LcdControlRegister, LcdEnable);

        Assert.Equal(0x12, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
        bus.WriteByte(AddressMap.ObjectAttributeMemoryStart, 0x34);

        bus.Ppu.Tick(80);
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
        bus.WriteByte(AddressMap.ObjectAttributeMemoryStart, 0x56);

        bus.Ppu.Tick(172);

        Assert.Equal(0x34, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));

        bus.Ppu.Tick(204);

        Assert.Equal(0xFF, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
        bus.WriteByte(AddressMap.ObjectAttributeMemoryStart, 0x78);

        bus.Ppu.Tick(252);

        Assert.Equal(0x34, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void ReadWriteByte_IgnoresNotUsableRange()
    {
        MemoryBus bus = CreateBus();

        bus.WriteByte(0xFEA0, 0x12);
        bus.WriteByte(0xFEFF, 0x34);

        Assert.Equal(0x00, bus.ReadByte(0xFEA0));
        Assert.Equal(0x00, bus.ReadByte(0xFEFF));
    }

    [Fact]
    public void ReadWriteByte_KeepsNotUsableRangeBehaviorDuringPpuOamBlock()
    {
        MemoryBus bus = CreateBus();
        bus.WriteByte(AddressMap.LcdControlRegister, LcdEnable);

        bus.WriteByte(AddressMap.NotUsableStart, 0x42);

        Assert.Equal(0x00, bus.ReadByte(AddressMap.NotUsableStart));
    }

    [Fact]
    public void ReadWriteByte_StoresIoRegisters()
    {
        MemoryBus bus = CreateBus();

        bus.WriteByte(0xFF03, 0x12);
        bus.WriteByte(0xFF7F, 0x34);

        Assert.Equal(0x12, bus.ReadByte(0xFF03));
        Assert.Equal(0x34, bus.ReadByte(0xFF7F));
    }

    [Fact]
    public void ReadWriteByte_RoutesJoypadRegister()
    {
        MemoryBus bus = CreateBus();
        bus.WriteByte(AddressMap.JoypadRegister, 0x20);

        bus.Joypad.SetButtonState(JoypadButton.Right, pressed: true);

        Assert.Equal(0xEE, bus.ReadByte(AddressMap.JoypadRegister));
        Assert.Equal(0b0001_0000, bus.Interrupts.InterruptFlag);
    }

    [Fact]
    public void ReadWriteByte_RoutesSerialRegisters()
    {
        MemoryBus bus = CreateBus();

        bus.WriteByte(AddressMap.SerialTransferDataRegister, 0x12);
        bus.WriteByte(AddressMap.SerialTransferControlRegister, 0x81);

        Assert.Equal(0x12, bus.ReadByte(AddressMap.SerialTransferDataRegister));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.SerialTransferControlRegister));

        bus.Serial.Tick(512 * 8);

        Assert.Equal(0xFF, bus.ReadByte(AddressMap.SerialTransferDataRegister));
        Assert.Equal(0x7F, bus.ReadByte(AddressMap.SerialTransferControlRegister));
        Assert.Equal(0b0000_1000, bus.Interrupts.InterruptFlag);
    }

    [Fact]
    public void ReadWriteByte_StoresHighRam()
    {
        MemoryBus bus = CreateBus();

        bus.WriteByte(0xFF80, 0x56);
        bus.WriteByte(0xFFFE, 0x78);

        Assert.Equal(0x56, bus.ReadByte(0xFF80));
        Assert.Equal(0x78, bus.ReadByte(0xFFFE));
    }

    [Fact]
    public void ReadWriteByte_StoresInterruptEnableRegister()
    {
        MemoryBus bus = CreateBus();

        bus.WriteByte(0xFFFF, 0xF1);

        Assert.Equal(0xF1, bus.ReadByte(0xFFFF));
        Assert.Equal(0xF1, bus.Interrupts.InterruptEnable);
    }

    [Fact]
    public void ReadWriteByte_RoutesInterruptFlagRegister()
    {
        MemoryBus bus = CreateBus();

        bus.WriteByte(0xFF0F, 0xFF);

        Assert.Equal(0xFF, bus.ReadByte(0xFF0F));
        Assert.Equal(0x1F, bus.Interrupts.InterruptFlag);
    }

    [Fact]
    public void ReadWriteByte_RoutesTimerRegisters()
    {
        MemoryBus bus = CreateBus();
        TickTimerMachineCycles(bus, 64);

        Assert.Equal(0x01, bus.ReadByte(AddressMap.DividerRegister));

        bus.WriteByte(AddressMap.DividerRegister, 0xFF);
        bus.WriteByte(AddressMap.TimerCounterRegister, 0x12);
        bus.WriteByte(AddressMap.TimerModuloRegister, 0x34);
        bus.WriteByte(AddressMap.TimerControlRegister, 0b0000_0101);

        Assert.Equal(0x00, bus.ReadByte(AddressMap.DividerRegister));
        Assert.Equal(0x12, bus.ReadByte(AddressMap.TimerCounterRegister));
        Assert.Equal(0x34, bus.ReadByte(AddressMap.TimerModuloRegister));
        Assert.Equal(0b1111_1101, bus.ReadByte(AddressMap.TimerControlRegister));
    }

    [Fact]
    public void ReadWriteByte_RoutesPpuRegisters()
    {
        MemoryBus bus = CreateBus();
        bus.SetHardwareRegisterState(AddressMap.LcdStatusRegister, 0x85);
        bus.SetHardwareRegisterState(AddressMap.LcdYCoordinateRegister, 0x42);

        bus.WriteByte(AddressMap.LcdControlRegister, 0x91);
        bus.WriteByte(AddressMap.LcdStatusRegister, 0x78);
        bus.WriteByte(AddressMap.LcdYCoordinateRegister, 0x99);
        bus.WriteByte(AddressMap.BackgroundPaletteRegister, 0xFC);
        bus.WriteByte(AddressMap.ObjectPalette0Register, 0xA5);
        bus.WriteByte(AddressMap.ObjectPalette1Register, 0x5A);

        Assert.Equal(0x91, bus.ReadByte(AddressMap.LcdControlRegister));
        Assert.Equal(0xF8, bus.ReadByte(AddressMap.LcdStatusRegister));
        Assert.Equal(0x42, bus.ReadByte(AddressMap.LcdYCoordinateRegister));
        Assert.Equal(0xFC, bus.ReadByte(AddressMap.BackgroundPaletteRegister));
        Assert.Equal(0xA5, bus.ReadByte(AddressMap.ObjectPalette0Register));
        Assert.Equal(0x5A, bus.ReadByte(AddressMap.ObjectPalette1Register));
    }

    [Fact]
    public void ReadWriteByte_RoutesDmaRegisterAndDefersOamCopy()
    {
        byte[] rom = TestRomFactory.Create(bytes => bytes[0x1200] = 0x42);
        MemoryBus bus = CreateBus(rom);

        bus.WriteByte(AddressMap.DmaRegister, 0x12);
        bus.TickDma(2);
        bus.TickDma(160);

        Assert.Equal(0x42, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void TickDma_CopiesFromRomWindow()
    {
        byte[] rom = TestRomFactory.Create(bytes => bytes[0x1200] = 0x66);
        MemoryBus bus = CreateBus(rom);

        bus.WriteByte(AddressMap.DmaRegister, 0x12);
        bus.TickDma(2);
        bus.TickDma(160);

        Assert.Equal(0x66, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void TickDma_CopiesFromVideoRam()
    {
        MemoryBus bus = CreateBus();
        bus.WriteByte(AddressMap.VideoRamStart, 0x99);

        bus.WriteByte(AddressMap.DmaRegister, 0x80);
        bus.TickDma(2);
        bus.TickDma(160);

        Assert.Equal(0x99, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void TickDma_DecodesHighSourcePagesAsExternalRam()
    {
        byte[] rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc1Ram;
            bytes[0x0149] = 0x02;
        });
        MemoryBus bus = CreateBus(rom);
        bus.WriteByte(0x0000, 0x0A);
        bus.WriteByte(AddressMap.ExternalRamStart, 0x42);
        bus.WriteByte(AddressMap.WorkRamStart, 0x99);

        bus.WriteByte(AddressMap.DmaRegister, 0xC0);
        bus.TickDma(2);
        bus.TickDma(160);

        Assert.Equal(0x42, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void TickDma_DoesNotReadIoRegistersAsSource()
    {
        byte[] rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc1Ram;
            bytes[0x0149] = 0x02;
        });
        MemoryBus bus = CreateBus(rom);
        bus.WriteByte(0x0000, 0x0A);
        bus.WriteByte(0xBF00, 0x42);
        bus.WriteByte(AddressMap.JoypadRegister, 0x20);
        bus.Joypad.SetButtonState(JoypadButton.Right, pressed: true);

        bus.WriteByte(AddressMap.DmaRegister, 0xFF);
        bus.TickDma(2);
        bus.TickDma(160);

        Assert.Equal(0x42, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void TickDma_WritesObjectAttributeMemoryWhileCpuAccessIsPpuBlocked()
    {
        MemoryBus bus = CreateBus();
        bus.WriteByte(AddressMap.VideoRamStart, 0x42);
        bus.WriteByte(AddressMap.LcdControlRegister, LcdEnable);
        bus.Ppu.Tick(80);

        bus.WriteByte(AddressMap.DmaRegister, 0x80);
        bus.TickDma(2);
        bus.TickDma(160);

        Assert.Equal(0xFF, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));

        bus.Ppu.Tick(172);

        Assert.Equal(0x42, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void ReadByte_AllowsObjectAttributeMemoryDuringDmaStartupDelay()
    {
        MemoryBus bus = CreateBus();
        bus.WriteByte(AddressMap.ObjectAttributeMemoryStart, 0x44);

        bus.WriteByte(AddressMap.DmaRegister, 0x80);

        Assert.Equal(0x44, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));

        bus.TickDma(1);
        Assert.Equal(0x44, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));

        bus.TickDma(1);
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void ReadByte_AppliesDmgDmaBusConflicts()
    {
        byte[] rom = TestRomFactory.Create(bytes => bytes[0x0000] = 0x11);
        MemoryBus bus = CreateBus(rom);
        bus.WriteByte(AddressMap.VideoRamStart, 0x22);
        bus.WriteByte(AddressMap.VideoRamStart + 1, 0x77);
        bus.WriteByte(AddressMap.WorkRamStart, 0x33);
        bus.WriteByte(AddressMap.ObjectAttributeMemoryStart, 0x44);

        bus.WriteByte(AddressMap.DmaRegister, 0x80);
        bus.TickDma(2);
        bus.TickDma(1);

        Assert.Equal(0x11, bus.ReadByte(AddressMap.RomStart));
        Assert.Equal(0x22, bus.ReadByte(AddressMap.VideoRamStart + 1));
        Assert.Equal(0x33, bus.ReadByte(AddressMap.WorkRamStart));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void WriteByte_BlocksCpuMemoryDuringDma()
    {
        MemoryBus bus = CreateBus();
        bus.WriteByte(AddressMap.VideoRamStart, 0x22);
        bus.WriteByte(AddressMap.WorkRamStart, 0x42);
        bus.WriteByte(AddressMap.WorkRamStart + 1, 0x33);
        bus.WriteByte(AddressMap.WorkRamStart + 2, 0x44);
        bus.WriteByte(AddressMap.ObjectAttributeMemoryStart, 0x55);

        bus.WriteByte(AddressMap.DmaRegister, 0xC0);
        bus.TickDma(2);
        bus.TickDma(1);
        bus.WriteByte(AddressMap.VideoRamStart, 0xAA);
        bus.WriteByte(AddressMap.WorkRamStart + 1, 0xBB);
        bus.WriteByte(AddressMap.EchoRamStart + 2, 0xCC);
        bus.WriteByte(AddressMap.ObjectAttributeMemoryStart, 0xDD);
        bus.TickDma(160);

        Assert.Equal(0xAA, bus.ReadByte(AddressMap.VideoRamStart));
        Assert.Equal(0x33, bus.ReadByte(AddressMap.WorkRamStart + 1));
        Assert.Equal(0x44, bus.ReadByte(AddressMap.WorkRamStart + 2));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void ReadWriteByte_KeepsNotUsableRangeBehaviorDuringDma()
    {
        MemoryBus bus = CreateBus();

        bus.WriteByte(AddressMap.DmaRegister, 0xC0);
        bus.WriteByte(AddressMap.NotUsableStart, 0x42);

        Assert.Equal(0x00, bus.ReadByte(AddressMap.NotUsableStart));
    }

    [Fact]
    public void ReadWriteByte_AllowsIoHighRamAndInterruptEnableDuringDma()
    {
        MemoryBus bus = CreateBus();

        bus.WriteByte(AddressMap.DmaRegister, 0xC0);
        bus.WriteByte(0xFF03, 0x12);
        bus.WriteByte(AddressMap.HighRamStart, 0x34);
        bus.WriteByte(AddressMap.InterruptEnableRegister, 0x56);

        Assert.Equal(0xC0, bus.ReadByte(AddressMap.DmaRegister));
        Assert.Equal(0x12, bus.ReadByte(0xFF03));
        Assert.Equal(0x34, bus.ReadByte(AddressMap.HighRamStart));
        Assert.Equal(0x56, bus.ReadByte(AddressMap.InterruptEnableRegister));
    }

    [Fact]
    public void WriteByte_AllowsDmaRestartDuringDma()
    {
        MemoryBus bus = CreateBus();
        bus.WriteByte(AddressMap.VideoRamStart, 0xC0);
        bus.WriteByte(0x9000, 0xD0);

        bus.WriteByte(AddressMap.DmaRegister, 0x80);
        bus.TickDma(2);
        bus.TickDma(1);
        bus.WriteByte(AddressMap.DmaRegister, 0x90);
        bus.TickDma(2);
        bus.TickDma(160);

        Assert.Equal(0x90, bus.ReadByte(AddressMap.DmaRegister));
        Assert.Equal(0xD0, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void SetHardwareRegisterState_DmaRegisterDoesNotStartTransfer()
    {
        MemoryBus bus = CreateBus();
        bus.WriteByte(AddressMap.VideoRamStart, 0x42);

        bus.SetHardwareRegisterState(AddressMap.DmaRegister, 0x80);
        bus.TickDma(160);

        Assert.Equal(0x80, bus.ReadByte(AddressMap.DmaRegister));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void SetHardwareRegisterState_SerialControlDoesNotStartTransfer()
    {
        MemoryBus bus = CreateBus();

        bus.SetHardwareRegisterState(AddressMap.SerialTransferDataRegister, 0x00);
        bus.SetHardwareRegisterState(AddressMap.SerialTransferControlRegister, 0x81);
        bus.Serial.Tick(512 * 8);

        Assert.Equal(0x00, bus.ReadByte(AddressMap.SerialTransferDataRegister));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.SerialTransferControlRegister));
        Assert.Equal(0x00, bus.Interrupts.InterruptFlag);
    }

    [Fact]
    public void ReadWriteByte_ExternalRamIsUnmappedForRomOnlyCartridge()
    {
        MemoryBus bus = CreateBus();

        bus.WriteByte(0xA000, 0x42);

        Assert.Equal(0xFF, bus.ReadByte(0xA000));
        Assert.Equal(0xFF, bus.ReadByte(0xBFFF));
    }

    [Fact]
    public void ReadWriteByte_RoutesExternalRamToMbcCartridge()
    {
        byte[] rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc1Ram;
            bytes[0x0149] = 0x02;
        });
        MemoryBus bus = CreateBus(rom);

        bus.WriteByte(0x0000, 0x0A);
        bus.WriteByte(AddressMap.ExternalRamStart, 0x42);

        Assert.Equal(0x42, bus.ReadByte(AddressMap.ExternalRamStart));
    }

    private static MemoryBus CreateBus() => CreateBus(TestRomFactory.Create());

    private static MemoryBus CreateBus(byte[] rom)
    {
        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));
        return new MemoryBus(cartridge);
    }

    private static void TickTimerMachineCycles(MemoryBus bus, int machineCycles)
    {
        for (int cycle = 0; cycle < machineCycles; cycle++)
        {
            bus.Timers.TickMachineCycle();
        }
    }
}
