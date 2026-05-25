using FluentResults;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Cpu;
using GbcNet.Core.Memory;
using GbcNet.Tests.Cartridges;

namespace GbcNet.Tests.Cpu;

public sealed class Sm83CpuTests
{
    [Fact]
    public void Constructor_InitializesDmgPostBootProgramCounterAndStackPointer()
    {
        Sm83Cpu cpu = CreateCpu();

        Assert.Equal(0x0100, cpu.Registers.PC);
        Assert.Equal(0xFFFE, cpu.Registers.SP);
    }

    [Fact]
    public void Step_ExecutesNop()
    {
        Sm83Cpu cpu = CreateCpu(bytes => bytes[0x0100] = 0x00);

        int machineCycles = cpu.Step();

        Assert.Equal(1, machineCycles);
        Assert.Equal(0x0101, cpu.Registers.PC);
    }

    [Fact]
    public void Step_LoadsImmediate16IntoRegisterPairs()
    {
        Sm83Cpu cpu = CreateCpu(bytes =>
        {
            bytes[0x0100] = 0x01;
            bytes[0x0101] = 0x34;
            bytes[0x0102] = 0x12;
            bytes[0x0103] = 0x11;
            bytes[0x0104] = 0x78;
            bytes[0x0105] = 0x56;
            bytes[0x0106] = 0x21;
            bytes[0x0107] = 0xBC;
            bytes[0x0108] = 0x9A;
            bytes[0x0109] = 0x31;
            bytes[0x010A] = 0xF0;
            bytes[0x010B] = 0xDE;
        });

        Assert.Equal(3, cpu.Step());
        Assert.Equal(0x1234, cpu.Registers.BC);
        Assert.Equal(0x0103, cpu.Registers.PC);

        Assert.Equal(3, cpu.Step());
        Assert.Equal(0x5678, cpu.Registers.DE);
        Assert.Equal(0x0106, cpu.Registers.PC);

        Assert.Equal(3, cpu.Step());
        Assert.Equal(0x9ABC, cpu.Registers.HL);
        Assert.Equal(0x0109, cpu.Registers.PC);

        Assert.Equal(3, cpu.Step());
        Assert.Equal(0xDEF0, cpu.Registers.SP);
        Assert.Equal(0x010C, cpu.Registers.PC);
    }

    [Fact]
    public void Step_StoresAccumulatorThroughRegisterPairAddresses()
    {
        Sm83Cpu cpu = CreateCpu(bytes =>
        {
            bytes[0x0100] = 0x02;
            bytes[0x0101] = 0x12;
        });

        cpu.Registers.A = 0xAB;
        cpu.Registers.BC = 0xC000;

        Assert.Equal(2, cpu.Step());
        Assert.Equal(0xAB, cpu.ReadByte(0xC000));
        Assert.Equal(0x0101, cpu.Registers.PC);

        cpu.Registers.A = 0xCD;
        cpu.Registers.DE = 0xC001;

        Assert.Equal(2, cpu.Step());
        Assert.Equal(0xCD, cpu.ReadByte(0xC001));
        Assert.Equal(0x0102, cpu.Registers.PC);
    }

    [Fact]
    public void Step_LoadsAccumulatorThroughRegisterPairAddresses()
    {
        Sm83Cpu cpu = CreateCpu(bytes =>
        {
            bytes[0x0100] = 0x0A;
            bytes[0x0101] = 0x1A;
        });
        cpu.Registers.BC = 0xC002;
        cpu.Registers.DE = 0xC003;
        cpu.WriteByte(0xC002, 0x34);
        cpu.WriteByte(0xC003, 0x56);

        Assert.Equal(2, cpu.Step());
        Assert.Equal(0x34, cpu.Registers.A);
        Assert.Equal(0x0101, cpu.Registers.PC);

        Assert.Equal(2, cpu.Step());
        Assert.Equal(0x56, cpu.Registers.A);
        Assert.Equal(0x0102, cpu.Registers.PC);
    }

    [Fact]
    public void Step_LoadsThroughHlAndUpdatesHl()
    {
        Sm83Cpu cpu = CreateCpu(bytes =>
        {
            bytes[0x0100] = 0x22;
            bytes[0x0101] = 0x2A;
            bytes[0x0102] = 0x32;
            bytes[0x0103] = 0x3A;
        });
        cpu.Registers.HL = 0xC010;
        cpu.Registers.A = 0x44;

        Assert.Equal(2, cpu.Step());
        Assert.Equal(0x44, cpu.ReadByte(0xC010));
        Assert.Equal(0xC011, cpu.Registers.HL);

        cpu.WriteByte(0xC011, 0x55);

        Assert.Equal(2, cpu.Step());
        Assert.Equal(0x55, cpu.Registers.A);
        Assert.Equal(0xC012, cpu.Registers.HL);

        cpu.Registers.A = 0x66;

        Assert.Equal(2, cpu.Step());
        Assert.Equal(0x66, cpu.ReadByte(0xC012));
        Assert.Equal(0xC011, cpu.Registers.HL);

        cpu.WriteByte(0xC011, 0x77);

        Assert.Equal(2, cpu.Step());
        Assert.Equal(0x77, cpu.Registers.A);
        Assert.Equal(0xC010, cpu.Registers.HL);
        Assert.Equal(0x0104, cpu.Registers.PC);
    }

    [Fact]
    public void Step_StoresStackPointerAtImmediate16Address()
    {
        Sm83Cpu cpu = CreateCpu(bytes =>
        {
            bytes[0x0100] = 0x08;
            bytes[0x0101] = 0x20;
            bytes[0x0102] = 0xC0;
        });
        cpu.Registers.SP = 0xBEEF;

        int machineCycles = cpu.Step();

        Assert.Equal(5, machineCycles);
        Assert.Equal(0xEF, cpu.ReadByte(0xC020));
        Assert.Equal(0xBE, cpu.ReadByte(0xC021));
        Assert.Equal(0x0103, cpu.Registers.PC);
    }

    [Fact]
    public void Step_RejectsUnsupportedOpcode()
    {
        Sm83Cpu cpu = CreateCpu(bytes => bytes[0x0100] = 0xFF);

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => cpu.Step());

        Assert.Equal("Opcode 0xFF is not supported yet.", exception.Message);
    }

    private static Sm83Cpu CreateCpu(Action<byte[]>? configure = null)
    {
        Result<Cartridge> cartridge = Cartridge.Load(TestRomFactory.Create(configure));
        Assert.True(cartridge.IsSuccess, DescribeErrors(cartridge.Errors));
        return new Sm83Cpu(new MemoryBus(cartridge.Value));
    }

    private static string DescribeErrors(IReadOnlyList<FluentResults.IError> errors)
    {
        return string.Join(Environment.NewLine, errors.Select(error => error.Message));
    }
}
