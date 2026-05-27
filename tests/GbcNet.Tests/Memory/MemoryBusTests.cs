using FluentResults;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Memory;
using GbcNet.Tests.Cartridges;

namespace GbcNet.Tests.Memory;

public sealed class MemoryBusTests
{
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
    public void ReadWriteByte_IgnoresNotUsableRange()
    {
        MemoryBus bus = CreateBus();

        bus.WriteByte(0xFEA0, 0x12);
        bus.WriteByte(0xFEFF, 0x34);

        Assert.Equal(0x00, bus.ReadByte(0xFEA0));
        Assert.Equal(0x00, bus.ReadByte(0xFEFF));
    }

    [Fact]
    public void ReadWriteByte_StoresIoRegisters()
    {
        MemoryBus bus = CreateBus();

        bus.WriteByte(0xFF00, 0x12);
        bus.WriteByte(0xFF7F, 0x34);

        Assert.Equal(0x12, bus.ReadByte(0xFF00));
        Assert.Equal(0x34, bus.ReadByte(0xFF7F));
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

        bus.WriteByte(0xFFFF, 0x1F);

        Assert.Equal(0x1F, bus.ReadByte(0xFFFF));
    }

    [Fact]
    public void ReadWriteByte_ExternalRamIsUnmappedForRomOnlyCartridge()
    {
        MemoryBus bus = CreateBus();

        bus.WriteByte(0xA000, 0x42);

        Assert.Equal(0xFF, bus.ReadByte(0xA000));
        Assert.Equal(0xFF, bus.ReadByte(0xBFFF));
    }

    private static MemoryBus CreateBus()
    {
        return CreateBus(TestRomFactory.Create());
    }

    private static MemoryBus CreateBus(byte[] rom)
    {
        Result<Cartridge> cartridge = Cartridge.Load(rom);
        Assert.True(cartridge.IsSuccess, DescribeErrors(cartridge.Errors));
        return new MemoryBus(cartridge.Value);
    }

    private static string DescribeErrors(IReadOnlyList<IError> errors)
    {
        return string.Join(Environment.NewLine, errors.Select(error => error.Message));
    }
}
