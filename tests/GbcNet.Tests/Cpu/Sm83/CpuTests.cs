using FluentResults;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Cpu.Sm83;
using GbcNet.Core.Memory;
using GbcNet.Tests.Cartridges;
using Sm83Cpu = GbcNet.Core.Cpu.Sm83.Cpu;

namespace GbcNet.Tests.Cpu.Sm83;

public sealed class CpuTests
{
    private const byte ARegister = (byte)Register8.A;
    private const byte BRegister = (byte)Register8.B;
    private const byte CRegister = (byte)Register8.C;
    private const byte DRegister = (byte)Register8.D;
    private const byte ERegister = (byte)Register8.E;
    private const byte HRegister = (byte)Register8.H;
    private const byte LRegister = (byte)Register8.L;
    private const byte BcRegisterPair = (byte)RegisterPair.BC;
    private const byte DeRegisterPair = (byte)RegisterPair.DE;
    private const byte HlRegisterPair = (byte)RegisterPair.HL;
    private const byte SpRegisterPair = (byte)RegisterPair.SP;

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

    [Theory]
    [InlineData(0x06, BRegister, 0x12)]
    [InlineData(0x0E, CRegister, 0x23)]
    [InlineData(0x16, DRegister, 0x34)]
    [InlineData(0x1E, ERegister, 0x45)]
    [InlineData(0x26, HRegister, 0x56)]
    [InlineData(0x2E, LRegister, 0x67)]
    [InlineData(0x3E, ARegister, 0x78)]
    public void Step_LoadsImmediate8IntoRegistersWithoutChangingFlags(
        byte opcode,
        byte register,
        byte value
    )
    {
        Sm83Cpu cpu = CreateCpu(bytes =>
        {
            bytes[0x0100] = opcode;
            bytes[0x0101] = value;
        });
        var register8 = (Register8)register;
        cpu.Registers.F = 0xF0;

        int machineCycles = cpu.Step();

        Assert.Equal(2, machineCycles);
        Assert.Equal(value, cpu.Registers.GetRegister(register8));
        Assert.Equal(0xF0, cpu.Registers.F);
        Assert.Equal(0x0102, cpu.Registers.PC);
    }

    [Fact]
    public void Step_LoadsImmediate8IntoMemoryAtHlWithoutChangingFlags()
    {
        Sm83Cpu cpu = CreateCpu(bytes =>
        {
            bytes[0x0100] = 0x36;
            bytes[0x0101] = 0x9A;
        });
        cpu.Registers.HL = 0xC123;
        cpu.Registers.F = 0xF0;

        int machineCycles = cpu.Step();

        Assert.Equal(3, machineCycles);
        Assert.Equal(0x9A, cpu.ReadByte(0xC123));
        Assert.Equal(0xF0, cpu.Registers.F);
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
    public void Step_IncrementsRegisterPairsWithoutChangingFlags()
    {
        Sm83Cpu cpu = CreateCpu(bytes =>
        {
            bytes[0x0100] = 0x03;
            bytes[0x0101] = 0x13;
            bytes[0x0102] = 0x23;
            bytes[0x0103] = 0x33;
        });
        cpu.Registers.F = 0xF0;
        cpu.Registers.BC = 0x00FF;
        cpu.Registers.DE = 0xFFFF;
        cpu.Registers.HL = 0x1234;
        cpu.Registers.SP = 0xFFFE;

        Assert.Equal(2, cpu.Step());
        Assert.Equal(0x0100, cpu.Registers.BC);
        Assert.Equal(0xF0, cpu.Registers.F);

        Assert.Equal(2, cpu.Step());
        Assert.Equal(0x0000, cpu.Registers.DE);
        Assert.Equal(0xF0, cpu.Registers.F);

        Assert.Equal(2, cpu.Step());
        Assert.Equal(0x1235, cpu.Registers.HL);
        Assert.Equal(0xF0, cpu.Registers.F);

        Assert.Equal(2, cpu.Step());
        Assert.Equal(0xFFFF, cpu.Registers.SP);
        Assert.Equal(0xF0, cpu.Registers.F);
        Assert.Equal(0x0104, cpu.Registers.PC);
    }

    [Fact]
    public void Step_DecrementsRegisterPairsWithoutChangingFlags()
    {
        Sm83Cpu cpu = CreateCpu(bytes =>
        {
            bytes[0x0100] = 0x0B;
            bytes[0x0101] = 0x1B;
            bytes[0x0102] = 0x2B;
            bytes[0x0103] = 0x3B;
        });
        cpu.Registers.F = 0xF0;
        cpu.Registers.BC = 0x0100;
        cpu.Registers.DE = 0x0000;
        cpu.Registers.HL = 0x1234;
        cpu.Registers.SP = 0x0001;

        Assert.Equal(2, cpu.Step());
        Assert.Equal(0x00FF, cpu.Registers.BC);
        Assert.Equal(0xF0, cpu.Registers.F);

        Assert.Equal(2, cpu.Step());
        Assert.Equal(0xFFFF, cpu.Registers.DE);
        Assert.Equal(0xF0, cpu.Registers.F);

        Assert.Equal(2, cpu.Step());
        Assert.Equal(0x1233, cpu.Registers.HL);
        Assert.Equal(0xF0, cpu.Registers.F);

        Assert.Equal(2, cpu.Step());
        Assert.Equal(0x0000, cpu.Registers.SP);
        Assert.Equal(0xF0, cpu.Registers.F);
        Assert.Equal(0x0104, cpu.Registers.PC);
    }

    [Theory]
    [InlineData(0x04, BRegister, 0x0F, 0xD0, 0x10, 0x30)]
    [InlineData(0x0C, CRegister, 0xFF, 0x10, 0x00, 0xB0)]
    [InlineData(0x14, DRegister, 0x00, 0xF0, 0x01, 0x10)]
    [InlineData(0x1C, ERegister, 0x7E, 0x00, 0x7F, 0x00)]
    [InlineData(0x24, HRegister, 0x2F, 0x10, 0x30, 0x30)]
    [InlineData(0x2C, LRegister, 0xFE, 0x10, 0xFF, 0x10)]
    [InlineData(0x3C, ARegister, 0xFF, 0x00, 0x00, 0xA0)]
    [InlineData(0x05, BRegister, 0x10, 0x90, 0x0F, 0x70)]
    [InlineData(0x0D, CRegister, 0x01, 0xB0, 0x00, 0xD0)]
    [InlineData(0x15, DRegister, 0x00, 0x00, 0xFF, 0x60)]
    [InlineData(0x1D, ERegister, 0x20, 0x10, 0x1F, 0x70)]
    [InlineData(0x25, HRegister, 0x02, 0x10, 0x01, 0x50)]
    [InlineData(0x2D, LRegister, 0xF0, 0x10, 0xEF, 0x70)]
    [InlineData(0x3D, ARegister, 0xFF, 0x00, 0xFE, 0x40)]
    public void Step_UpdatesRegistersAndFlags(
        byte opcode,
        byte register,
        byte initialValue,
        byte initialFlags,
        byte expectedValue,
        byte expectedFlags
    )
    {
        Sm83Cpu cpu = CreateCpu(bytes => bytes[0x0100] = opcode);
        var register8 = (Register8)register;
        cpu.Registers.SetRegister(register8, initialValue);
        cpu.Registers.F = initialFlags;

        int machineCycles = cpu.Step();

        Assert.Equal(1, machineCycles);
        Assert.Equal(expectedValue, cpu.Registers.GetRegister(register8));
        Assert.Equal(expectedFlags, cpu.Registers.F);
        Assert.Equal(0x0101, cpu.Registers.PC);
    }

    [Theory]
    [InlineData(0x34, 0x0F, 0xD0, 0x10, 0x30)]
    [InlineData(0x34, 0xFF, 0x10, 0x00, 0xB0)]
    [InlineData(0x35, 0x10, 0x90, 0x0F, 0x70)]
    [InlineData(0x35, 0x01, 0xB0, 0x00, 0xD0)]
    public void Step_UpdatesMemoryAtHlAndFlags(
        byte opcode,
        byte initialValue,
        byte initialFlags,
        byte expectedValue,
        byte expectedFlags
    )
    {
        Sm83Cpu cpu = CreateCpu(bytes => bytes[0x0100] = opcode);
        cpu.Registers.HL = 0xC100;
        cpu.Registers.F = initialFlags;
        cpu.WriteByte(0xC100, initialValue);

        int machineCycles = cpu.Step();

        Assert.Equal(3, machineCycles);
        Assert.Equal(expectedValue, cpu.ReadByte(0xC100));
        Assert.Equal(expectedFlags, cpu.Registers.F);
        Assert.Equal(0x0101, cpu.Registers.PC);
    }

    [Theory]
    [InlineData(0x09, BcRegisterPair, 0x0001, 0x0002, 0xF0, 0x0003, 0x80)]
    [InlineData(0x19, DeRegisterPair, 0x0FFF, 0x0001, 0x80, 0x1000, 0xA0)]
    [InlineData(0x29, HlRegisterPair, 0x8000, 0x8000, 0x00, 0x0000, 0x10)]
    [InlineData(0x39, SpRegisterPair, 0xFFFF, 0x0001, 0xC0, 0x0000, 0xB0)]
    public void Step_AddsRegisterPairToHlAndUpdatesFlags(
        byte opcode,
        byte sourceRegisterPair,
        ushort initialHl,
        ushort sourceValue,
        byte initialFlags,
        ushort expectedHl,
        byte expectedFlags
    )
    {
        Sm83Cpu cpu = CreateCpu(bytes => bytes[0x0100] = opcode);
        cpu.Registers.HL = initialHl;
        cpu.Registers.SetRegisterPair((RegisterPair)sourceRegisterPair, sourceValue);
        cpu.Registers.F = initialFlags;

        int machineCycles = cpu.Step();

        Assert.Equal(2, machineCycles);
        Assert.Equal(expectedHl, cpu.Registers.HL);
        Assert.Equal(expectedFlags, cpu.Registers.F);
        Assert.Equal(0x0101, cpu.Registers.PC);
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

    private static string DescribeErrors(IReadOnlyList<IError> errors) =>
        string.Join(Environment.NewLine, errors.Select(error => error.Message));
}
