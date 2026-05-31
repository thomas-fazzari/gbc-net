using GbcNet.Core;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Hardware;
using GbcNet.Core.Hardware.Strategies;
using GbcNet.Core.Memory;
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

        PostBootState.Apply(HardwareModel.Dmg, cartridge, cpu, bus);

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

        PostBootState.Apply(HardwareModel.Dmg, cartridge, cpu, bus);

        Assert.Equal(0x80, cpu.Registers.F);
    }

    [Fact]
    public void Apply_SetsDmgIoRegistersAfterBootHandoff()
    {
        Cartridge cartridge = LoadCartridge(TestRomFactory.Create());
        (Cpu cpu, MemoryBus bus) = CreateHardware(cartridge);

        PostBootState.Apply(HardwareModel.Dmg, cartridge, cpu, bus);

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

    private static (Cpu Cpu, MemoryBus Bus) CreateHardware(Cartridge cartridge)
    {
        var bus = new MemoryBus(cartridge, new DmgHardwareStrategy());
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
