using GbcNet.Core.Memory;
using GbcNet.Core.Ppu;

namespace GbcNet.Tests.Ppu;

public sealed class PpuControllerTests
{
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
        var ppu = new PpuController();

        ppu.WriteRegister(address, value);

        Assert.Equal(value, ppu.ReadRegister(address));
    }

    [Fact]
    public void ReadRegister_ReturnsStatusReadMaskAndPpuState()
    {
        var ppu = new PpuController();

        ppu.SetRegisterState(AddressMap.LcdStatusRegister, 0x85);

        Assert.Equal(0x85, ppu.ReadRegister(AddressMap.LcdStatusRegister));
    }

    [Fact]
    public void WriteRegister_UpdatesOnlyStatusInterruptSelectBits()
    {
        var ppu = new PpuController();
        ppu.SetRegisterState(AddressMap.LcdStatusRegister, 0x85);

        ppu.WriteRegister(AddressMap.LcdStatusRegister, 0x78);

        Assert.Equal(0xFD, ppu.ReadRegister(AddressMap.LcdStatusRegister));
    }

    [Fact]
    public void WriteRegister_IgnoresLcdYCoordinateWrites()
    {
        var ppu = new PpuController();
        ppu.SetRegisterState(AddressMap.LcdYCoordinateRegister, 0x42);

        ppu.WriteRegister(AddressMap.LcdYCoordinateRegister, 0x99);

        Assert.Equal(0x42, ppu.ReadRegister(AddressMap.LcdYCoordinateRegister));
    }
}
