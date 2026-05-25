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
