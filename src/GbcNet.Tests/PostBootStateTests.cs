using GbcNet.Core;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Hardware;
using GbcNet.Core.Hardware.Profiles;
using GbcNet.Core.Memory;
using GbcNet.Core.Ppu;
using GbcNet.Core.Sm83;
using GbcNet.Tests.Cartridges;

namespace GbcNet.Tests;

public sealed class PostBootStateTests
{
    [Fact]
    public void Apply_SetsDmgCpuRegistersAfterBootHandoff()
    {
        Cartridge cartridge = LoadCartridge(TestRomFactory.Create());
        (Cpu cpu, MemoryBus bus) = CreateHardware(cartridge);

        PostBootState.Apply(DmgHardwareProfile.Instance, cartridge, cpu, bus);

        Assert.Equal(0x01, cpu.Registers.A);
        Assert.Equal(0xB0, cpu.Registers.F);
        Assert.Equal(0x0013, cpu.Registers.BC);
        Assert.Equal(0x00D8, cpu.Registers.DE);
        Assert.Equal(0x014D, cpu.Registers.HL);
        Assert.Equal(0x0100, cpu.Registers.PC);
        Assert.Equal(0xFFFE, cpu.Registers.SP);
    }

    [Fact]
    public void Apply_ClearsDmgHalfCarryAndCarryWhenHeaderChecksumIsZero()
    {
        Cartridge cartridge = LoadCartridge(CreateRomWithZeroHeaderChecksum());
        (Cpu cpu, MemoryBus bus) = CreateHardware(cartridge);

        PostBootState.Apply(DmgHardwareProfile.Instance, cartridge, cpu, bus);

        Assert.Equal(0x80, cpu.Registers.F);
    }

    [Fact]
    public void Apply_SetsDmgIoRegistersAfterBootHandoff()
    {
        Cartridge cartridge = LoadCartridge(TestRomFactory.Create());
        (Cpu cpu, MemoryBus bus) = CreateHardware(cartridge);

        PostBootState.Apply(DmgHardwareProfile.Instance, cartridge, cpu, bus);

        Assert.Equal(0xCF, bus.ReadByte(AddressMap.JoypadRegister));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.SerialTransferDataRegister));
        Assert.Equal(0x7E, bus.ReadByte(AddressMap.SerialTransferControlRegister));
        Assert.Equal(0xAB, bus.ReadByte(AddressMap.DividerRegister));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.TimerCounterRegister));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.TimerModuloRegister));
        Assert.Equal(0xF8, bus.ReadByte(AddressMap.TimerControlRegister));
        Assert.Equal(0xE1, bus.ReadByte(AddressMap.InterruptFlagRegister));
        Assert.Equal(0x91, bus.ReadByte(AddressMap.LcdControlRegister));
        Assert.Equal(0x85, bus.ReadByte(AddressMap.LcdStatusRegister));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.ScrollYRegister));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.ScrollXRegister));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.LcdYCoordinateRegister));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.LcdYCompareRegister));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.DmaRegister));
        Assert.Equal(0xFC, bus.ReadByte(AddressMap.BackgroundPaletteRegister));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.WindowYRegister));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.WindowXRegister));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.InterruptEnableRegister));
    }

    [Fact]
    public void Apply_SetsCgbModeCpuRegistersAfterBootHandoff()
    {
        Cartridge cartridge = LoadCartridge(
            TestRomFactory.Create(rom => rom[0x0143] = (byte)CgbSupport.Required)
        );
        var profile = new CgbHardwareProfile(CgbOperatingMode.Cgb);
        var bus = new MemoryBus(cartridge, profile);
        var cpu = new Cpu(bus);

        PostBootState.Apply(profile, cartridge, cpu, bus);

        Assert.Equal(0x11, cpu.Registers.A);
        Assert.Equal(0x80, cpu.Registers.F);
        Assert.Equal(0x0000, cpu.Registers.BC);
        Assert.Equal(0xFF56, cpu.Registers.DE);
        Assert.Equal(0x000D, cpu.Registers.HL);
        Assert.Equal(0x0100, cpu.Registers.PC);
        Assert.Equal(0xFFFE, cpu.Registers.SP);
    }

    [Fact]
    public void Apply_SetsCgbDmgCompatibilityCpuRegistersAfterBootHandoff()
    {
        Cartridge cartridge = LoadCartridge(TestRomFactory.Create());
        var profile = new CgbHardwareProfile(CgbOperatingMode.DmgCompatibility);
        var bus = new MemoryBus(cartridge, profile);
        var cpu = new Cpu(bus);

        PostBootState.Apply(profile, cartridge, cpu, bus);

        Assert.Equal(0x11, cpu.Registers.A);
        Assert.Equal(0x80, cpu.Registers.F);
        Assert.Equal(0x0000, cpu.Registers.BC);
        Assert.Equal(0x0008, cpu.Registers.DE);
        Assert.Equal(0x007C, cpu.Registers.HL);
        Assert.Equal(0x0100, cpu.Registers.PC);
        Assert.Equal(0xFFFE, cpu.Registers.SP);
    }

    [Fact]
    public void Apply_SetsCgbModeIoRegistersAfterBootHandoff()
    {
        Cartridge cartridge = LoadCartridge(
            TestRomFactory.Create(rom => rom[0x0143] = (byte)CgbSupport.Required)
        );
        var profile = new CgbHardwareProfile(CgbOperatingMode.Cgb);
        var bus = new MemoryBus(cartridge, profile);
        var cpu = new Cpu(bus);

        PostBootState.Apply(profile, cartridge, cpu, bus);

        Assert.Equal(0x00, bus.ReadByte(AddressMap.SerialTransferDataRegister));
        Assert.Equal(0x7F, bus.ReadByte(AddressMap.SerialTransferControlRegister));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.TimerCounterRegister));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.TimerModuloRegister));
        Assert.Equal(0xF8, bus.ReadByte(AddressMap.TimerControlRegister));
        Assert.Equal(0xE1, bus.ReadByte(AddressMap.InterruptFlagRegister));
        Assert.Equal(0x91, bus.ReadByte(AddressMap.LcdControlRegister));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.ScrollYRegister));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.ScrollXRegister));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.LcdYCompareRegister));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.DmaRegister));
        Assert.Equal(0xFC, bus.ReadByte(AddressMap.BackgroundPaletteRegister));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.WindowYRegister));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.WindowXRegister));
        Assert.Equal(0x7E, bus.ReadByte(AddressMap.Key1Register));
        Assert.Equal(0xFE, bus.ReadByte(AddressMap.VideoRamBankRegister));
        Assert.Equal(0xF8, bus.ReadByte(AddressMap.WorkRamBankRegister));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.InterruptEnableRegister));
    }

    [Fact]
    public void Apply_SeedsCgbBackgroundPaletteRamToWhiteWithoutChangingIndex()
    {
        Cartridge cartridge = LoadCartridge(
            TestRomFactory.Create(rom => rom[0x0143] = (byte)CgbSupport.Required)
        );
        var profile = new CgbHardwareProfile(CgbOperatingMode.Cgb);
        var bus = new MemoryBus(cartridge, profile);
        var cpu = new Cpu(bus);
        bus.WriteByte(AddressMap.BackgroundPaletteIndexRegister, 0x85);
        bus.WriteByte(AddressMap.BackgroundPaletteDataRegister, 0x12);
        bus.WriteByte(AddressMap.BackgroundPaletteIndexRegister, 0x85);

        PostBootState.Apply(profile, cartridge, cpu, bus);

        Assert.Equal(0xC5, bus.ReadByte(AddressMap.BackgroundPaletteIndexRegister));
        Assert.Equal(0x7F, bus.ReadByte(AddressMap.BackgroundPaletteDataRegister));
        bus.WriteByte(AddressMap.BackgroundPaletteIndexRegister, 0x04);
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.BackgroundPaletteDataRegister));
    }

    [Fact]
    public void Apply_SeedsCgbDmgCompatibilityPaletteRamForRgbRendering()
    {
        Cartridge cartridge = LoadCartridge(TestRomFactory.Create());
        var profile = new CgbHardwareProfile(CgbOperatingMode.DmgCompatibility);
        var bus = new MemoryBus(cartridge, profile);
        var cpu = new Cpu(bus);

        PostBootState.Apply(profile, cartridge, cpu, bus);

        bus.WriteByte(AddressMap.BackgroundPaletteRegister, 0x08);
        bus.Ppu.VideoRam.Write(0x8000, 0x80);

        bus.Ppu.Tick(456 * 154);
        LcdFrame frame = Assert.IsType<LcdFrame>(bus.Ppu.Tick(456 * 144));

        Assert.Equal(LcdPixelFormat.Rgb555Le, frame.PixelFormat);
        Assert.Equal(0x4A, frame.Pixels.Span[0]);
        Assert.Equal(0x29, frame.Pixels.Span[1]);
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.BackgroundPaletteIndexRegister));
    }

    private static (Cpu Cpu, MemoryBus Bus) CreateHardware(Cartridge cartridge)
    {
        var bus = new MemoryBus(cartridge, DmgHardwareProfile.Instance);
        return (new Cpu(bus), bus);
    }

    private static Cartridge LoadCartridge(byte[] rom) =>
        ResultAssertions.AssertSuccess(Cartridge.Load(rom));

    private static byte[] CreateRomWithZeroHeaderChecksum()
    {
        return TestRomFactory.Create(bytes =>
        {
            byte checksum = CartridgeHeader.CalculateHeaderChecksum(bytes);
            bytes[0x0134] = unchecked((byte)(bytes[0x0134] + checksum));
        });
    }
}
