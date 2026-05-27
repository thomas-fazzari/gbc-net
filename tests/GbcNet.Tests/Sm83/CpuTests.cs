using GbcNet.Core.Sm83;

namespace GbcNet.Tests.Sm83;

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
        Cpu cpu = CpuTestFactory.CreateCpu();

        Assert.Equal(0x0100, cpu.Registers.PC);
        Assert.Equal(0xFFFE, cpu.Registers.SP);
    }

    [Fact]
    public void Step_ExecutesNop()
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = 0x00);

        int machineCycles = cpu.Step();

        Assert.Equal(1, machineCycles);
        Assert.Equal(0x0101, cpu.Registers.PC);
    }

    [Theory]
    [InlineData(0x02, 0x0104)]
    [InlineData(0xFE, 0x0100)]
    [InlineData(0x80, 0x0082)]
    public void Step_JumpsRelativeToNextInstruction(byte offset, ushort expectedPc)
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = 0x18;
            bytes[0x0101] = offset;
        });
        cpu.Registers.F = 0xF0;

        int machineCycles = cpu.Step();

        Assert.Equal(3, machineCycles);
        Assert.Equal(expectedPc, cpu.Registers.PC);
        Assert.Equal(0xF0, cpu.Registers.F);
    }

    [Theory]
    [InlineData(0x20, 0x00, 0x02, 0x0104, 3)]
    [InlineData(0x20, 0x80, 0x02, 0x0102, 2)]
    [InlineData(0x28, 0x80, 0xFE, 0x0100, 3)]
    [InlineData(0x28, 0x00, 0xFE, 0x0102, 2)]
    [InlineData(0x30, 0x00, 0x02, 0x0104, 3)]
    [InlineData(0x30, 0x10, 0x02, 0x0102, 2)]
    [InlineData(0x38, 0x10, 0xFE, 0x0100, 3)]
    [InlineData(0x38, 0x00, 0xFE, 0x0102, 2)]
    public void Step_ConditionallyJumpsRelativeToNextInstruction(
        byte opcode,
        byte flags,
        byte offset,
        ushort expectedPc,
        int expectedMachineCycles
    )
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = opcode;
            bytes[0x0101] = offset;
        });
        cpu.Registers.F = flags;

        int machineCycles = cpu.Step();

        Assert.Equal(expectedMachineCycles, machineCycles);
        Assert.Equal(expectedPc, cpu.Registers.PC);
        Assert.Equal(flags, cpu.Registers.F);
    }

    [Fact]
    public void Step_LoadsImmediate16IntoRegisterPairs()
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes =>
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
        Cpu cpu = CpuTestFactory.CreateCpu(bytes =>
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
        Cpu cpu = CpuTestFactory.CreateCpu(bytes =>
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
        Cpu cpu = CpuTestFactory.CreateCpu(bytes =>
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
        Cpu cpu = CpuTestFactory.CreateCpu(bytes =>
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

    [Theory]
    [InlineData(0x41, CRegister, 0x23, BRegister)]
    [InlineData(0x5A, DRegister, 0x34, ERegister)]
    [InlineData(0x7C, HRegister, 0x56, ARegister)]
    public void Step_LoadsRegisterIntoRegisterWithoutChangingFlags(
        byte opcode,
        byte source,
        byte sourceValue,
        byte destination
    )
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = opcode);
        var sourceRegister = (Register8)source;
        var destinationRegister = (Register8)destination;
        cpu.Registers.SetRegister(sourceRegister, sourceValue);
        cpu.Registers.F = 0xF0;

        int machineCycles = cpu.Step();

        Assert.Equal(1, machineCycles);
        Assert.Equal(sourceValue, cpu.Registers.GetRegister(destinationRegister));
        Assert.Equal(0xF0, cpu.Registers.F);
        Assert.Equal(0x0101, cpu.Registers.PC);
    }

    [Theory]
    [InlineData(0x46, BRegister)]
    [InlineData(0x7E, ARegister)]
    public void Step_LoadsMemoryAtHlIntoRegisterWithoutChangingFlags(byte opcode, byte destination)
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = opcode);
        var destinationRegister = (Register8)destination;
        cpu.Registers.HL = 0xC123;
        cpu.Registers.F = 0xF0;
        cpu.WriteByte(0xC123, 0x9A);

        int machineCycles = cpu.Step();

        Assert.Equal(2, machineCycles);
        Assert.Equal(0x9A, cpu.Registers.GetRegister(destinationRegister));
        Assert.Equal(0xF0, cpu.Registers.F);
        Assert.Equal(0x0101, cpu.Registers.PC);
    }

    [Theory]
    [InlineData(0x70, BRegister, 0x12)]
    [InlineData(0x77, ARegister, 0x34)]
    public void Step_LoadsRegisterIntoMemoryAtHlWithoutChangingFlags(
        byte opcode,
        byte source,
        byte sourceValue
    )
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = opcode);
        var sourceRegister = (Register8)source;
        cpu.Registers.HL = 0xC123;
        cpu.Registers.SetRegister(sourceRegister, sourceValue);
        cpu.Registers.F = 0xF0;

        int machineCycles = cpu.Step();

        Assert.Equal(2, machineCycles);
        Assert.Equal(sourceValue, cpu.ReadByte(0xC123));
        Assert.Equal(0xF0, cpu.Registers.F);
        Assert.Equal(0x0101, cpu.Registers.PC);
    }

    [Theory]
    [InlineData(0x80, BRegister, 0xF0, 0x12, 0x23, 0x35, 0x00)]
    [InlineData(0x81, CRegister, 0xF0, 0x8F, 0x71, 0x00, 0xB0)]
    [InlineData(0x87, ARegister, 0xF0, 0x80, 0x80, 0x00, 0x90)]
    [InlineData(0x88, BRegister, 0xE0, 0x12, 0x23, 0x35, 0x00)]
    [InlineData(0x89, CRegister, 0x10, 0x0F, 0x00, 0x10, 0x20)]
    [InlineData(0x8F, ARegister, 0x10, 0xFF, 0xFF, 0xFF, 0x30)]
    [InlineData(0x90, BRegister, 0x00, 0x35, 0x23, 0x12, 0x40)]
    [InlineData(0x91, CRegister, 0x00, 0x10, 0x01, 0x0F, 0x60)]
    [InlineData(0x97, ARegister, 0x00, 0x42, 0x42, 0x00, 0xC0)]
    [InlineData(0x92, DRegister, 0x00, 0x00, 0x01, 0xFF, 0x70)]
    [InlineData(0x98, BRegister, 0x00, 0x35, 0x23, 0x12, 0x40)]
    [InlineData(0x99, CRegister, 0x10, 0x10, 0x0F, 0x00, 0xE0)]
    [InlineData(0x9F, ARegister, 0x10, 0x00, 0x00, 0xFF, 0x70)]
    [InlineData(0xA0, BRegister, 0xF0, 0xF0, 0x0F, 0x00, 0xA0)]
    [InlineData(0xA1, CRegister, 0xF0, 0xF3, 0x33, 0x33, 0x20)]
    [InlineData(0xA7, ARegister, 0xF0, 0x5A, 0x5A, 0x5A, 0x20)]
    [InlineData(0xA8, BRegister, 0xF0, 0xF0, 0x0F, 0xFF, 0x00)]
    [InlineData(0xA9, CRegister, 0xF0, 0x33, 0x33, 0x00, 0x80)]
    [InlineData(0xAF, ARegister, 0xF0, 0x5A, 0x5A, 0x00, 0x80)]
    [InlineData(0xB0, BRegister, 0xF0, 0xF0, 0x0F, 0xFF, 0x00)]
    [InlineData(0xB1, CRegister, 0xF0, 0x00, 0x00, 0x00, 0x80)]
    [InlineData(0xB7, ARegister, 0xF0, 0x5A, 0x5A, 0x5A, 0x00)]
    [InlineData(0xB8, BRegister, 0xF0, 0x35, 0x23, 0x35, 0x40)]
    [InlineData(0xB9, CRegister, 0xF0, 0x10, 0x01, 0x10, 0x60)]
    [InlineData(0xBF, ARegister, 0xF0, 0x42, 0x42, 0x42, 0xC0)]
    [InlineData(0xBA, DRegister, 0xF0, 0x00, 0x01, 0x00, 0x70)]
    public void Step_ExecutesAccumulatorRegisterOperandAndUpdatesFlags(
        byte opcode,
        byte source,
        byte initialFlags,
        byte initialA,
        byte sourceValue,
        byte expectedA,
        byte expectedFlags
    )
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = opcode);
        var sourceRegister = (Register8)source;
        cpu.Registers.A = initialA;
        cpu.Registers.SetRegister(sourceRegister, sourceValue);
        cpu.Registers.F = initialFlags;

        int machineCycles = cpu.Step();

        Assert.Equal(1, machineCycles);
        Assert.Equal(expectedA, cpu.Registers.A);
        Assert.Equal(expectedFlags, cpu.Registers.F);
        Assert.Equal(0x0101, cpu.Registers.PC);
    }

    [Theory]
    [InlineData(0xC6, 0xF0, 0x12, 0x23, 0x35, 0x00)]
    [InlineData(0xCE, 0x10, 0x0F, 0x00, 0x10, 0x20)]
    [InlineData(0xD6, 0x00, 0x10, 0x01, 0x0F, 0x60)]
    [InlineData(0xDE, 0x10, 0x10, 0x0F, 0x00, 0xE0)]
    [InlineData(0xE6, 0xF0, 0xF0, 0x0F, 0x00, 0xA0)]
    [InlineData(0xEE, 0xF0, 0x33, 0x33, 0x00, 0x80)]
    [InlineData(0xF6, 0xF0, 0x00, 0x00, 0x00, 0x80)]
    [InlineData(0xFE, 0xF0, 0x42, 0x42, 0x42, 0xC0)]
    public void Step_ExecutesAccumulatorImmediateOperandAndUpdatesFlags(
        byte opcode,
        byte initialFlags,
        byte initialA,
        byte value,
        byte expectedA,
        byte expectedFlags
    )
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes =>
        {
            bytes[0x0100] = opcode;
            bytes[0x0101] = value;
        });
        cpu.Registers.A = initialA;
        cpu.Registers.F = initialFlags;

        int machineCycles = cpu.Step();

        Assert.Equal(2, machineCycles);
        Assert.Equal(expectedA, cpu.Registers.A);
        Assert.Equal(expectedFlags, cpu.Registers.F);
        Assert.Equal(0x0102, cpu.Registers.PC);
    }

    [Fact]
    public void Step_AddsMemoryAtHlToAccumulatorAndUpdatesFlags()
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = 0x86);
        cpu.Registers.A = 0x0F;
        cpu.Registers.HL = 0xC123;
        cpu.Registers.F = 0xF0;
        cpu.WriteByte(0xC123, 0x01);

        int machineCycles = cpu.Step();

        Assert.Equal(2, machineCycles);
        Assert.Equal(0x10, cpu.Registers.A);
        Assert.Equal(0x20, cpu.Registers.F);
        Assert.Equal(0x01, cpu.ReadByte(0xC123));
        Assert.Equal(0x0101, cpu.Registers.PC);
    }

    [Fact]
    public void Step_AddsMemoryAtHlAndCarryToAccumulatorAndUpdatesFlags()
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = 0x8E);
        cpu.Registers.A = 0xFE;
        cpu.Registers.HL = 0xC123;
        cpu.Registers.F = (byte)CpuFlag.Carry;
        cpu.WriteByte(0xC123, 0x01);

        int machineCycles = cpu.Step();

        Assert.Equal(2, machineCycles);
        Assert.Equal(0x00, cpu.Registers.A);
        Assert.Equal(0xB0, cpu.Registers.F);
        Assert.Equal(0x01, cpu.ReadByte(0xC123));
        Assert.Equal(0x0101, cpu.Registers.PC);
    }

    [Fact]
    public void Step_SubtractsMemoryAtHlFromAccumulatorAndUpdatesFlags()
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = 0x96);
        cpu.Registers.A = 0x20;
        cpu.Registers.HL = 0xC123;
        cpu.Registers.F = 0x00;
        cpu.WriteByte(0xC123, 0x01);

        int machineCycles = cpu.Step();

        Assert.Equal(2, machineCycles);
        Assert.Equal(0x1F, cpu.Registers.A);
        Assert.Equal(0x60, cpu.Registers.F);
        Assert.Equal(0x01, cpu.ReadByte(0xC123));
        Assert.Equal(0x0101, cpu.Registers.PC);
    }

    [Fact]
    public void Step_SubtractsMemoryAtHlAndCarryFromAccumulatorAndUpdatesFlags()
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = 0x9E);
        cpu.Registers.A = 0x01;
        cpu.Registers.HL = 0xC123;
        cpu.Registers.F = (byte)CpuFlag.Carry;
        cpu.WriteByte(0xC123, 0x00);

        int machineCycles = cpu.Step();

        Assert.Equal(2, machineCycles);
        Assert.Equal(0x00, cpu.Registers.A);
        Assert.Equal(0xC0, cpu.Registers.F);
        Assert.Equal(0x00, cpu.ReadByte(0xC123));
        Assert.Equal(0x0101, cpu.Registers.PC);
    }

    [Fact]
    public void Step_AndsMemoryAtHlWithAccumulatorAndUpdatesFlags()
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = 0xA6);
        cpu.Registers.A = 0xF0;
        cpu.Registers.HL = 0xC123;
        cpu.Registers.F = 0xF0;
        cpu.WriteByte(0xC123, 0x0F);

        int machineCycles = cpu.Step();

        Assert.Equal(2, machineCycles);
        Assert.Equal(0x00, cpu.Registers.A);
        Assert.Equal(0xA0, cpu.Registers.F);
        Assert.Equal(0x0F, cpu.ReadByte(0xC123));
        Assert.Equal(0x0101, cpu.Registers.PC);
    }

    [Fact]
    public void Step_XorsMemoryAtHlWithAccumulatorAndUpdatesFlags()
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = 0xAE);
        cpu.Registers.A = 0xF0;
        cpu.Registers.HL = 0xC123;
        cpu.Registers.F = 0xF0;
        cpu.WriteByte(0xC123, 0x0F);

        int machineCycles = cpu.Step();

        Assert.Equal(2, machineCycles);
        Assert.Equal(0xFF, cpu.Registers.A);
        Assert.Equal(0x00, cpu.Registers.F);
        Assert.Equal(0x0F, cpu.ReadByte(0xC123));
        Assert.Equal(0x0101, cpu.Registers.PC);
    }

    [Fact]
    public void Step_OrsMemoryAtHlWithAccumulatorAndUpdatesFlags()
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = 0xB6);
        cpu.Registers.A = 0xF0;
        cpu.Registers.HL = 0xC123;
        cpu.Registers.F = 0xF0;
        cpu.WriteByte(0xC123, 0x0F);

        int machineCycles = cpu.Step();

        Assert.Equal(2, machineCycles);
        Assert.Equal(0xFF, cpu.Registers.A);
        Assert.Equal(0x00, cpu.Registers.F);
        Assert.Equal(0x0F, cpu.ReadByte(0xC123));
        Assert.Equal(0x0101, cpu.Registers.PC);
    }

    [Fact]
    public void Step_ComparesMemoryAtHlWithAccumulatorAndUpdatesFlags()
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = 0xBE);
        cpu.Registers.A = 0x20;
        cpu.Registers.HL = 0xC123;
        cpu.Registers.F = 0xF0;
        cpu.WriteByte(0xC123, 0x01);

        int machineCycles = cpu.Step();

        Assert.Equal(2, machineCycles);
        Assert.Equal(0x20, cpu.Registers.A);
        Assert.Equal(0x60, cpu.Registers.F);
        Assert.Equal(0x01, cpu.ReadByte(0xC123));
        Assert.Equal(0x0101, cpu.Registers.PC);
    }

    [Theory]
    [InlineData(0x07, 0x80, 0xE0, 0x01, 0x10)]
    [InlineData(0x07, 0x01, 0xF0, 0x02, 0x00)]
    [InlineData(0x0F, 0x01, 0xE0, 0x80, 0x10)]
    [InlineData(0x0F, 0x80, 0xF0, 0x40, 0x00)]
    [InlineData(0x17, 0x80, 0x00, 0x00, 0x10)]
    [InlineData(0x17, 0x00, 0x10, 0x01, 0x00)]
    [InlineData(0x1F, 0x01, 0x00, 0x00, 0x10)]
    [InlineData(0x1F, 0x00, 0x10, 0x80, 0x00)]
    public void Step_RotatesAccumulatorAndUpdatesFlags(
        byte opcode,
        byte initialA,
        byte initialFlags,
        byte expectedA,
        byte expectedFlags
    )
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = opcode);
        cpu.Registers.A = initialA;
        cpu.Registers.F = initialFlags;

        int machineCycles = cpu.Step();

        Assert.Equal(1, machineCycles);
        Assert.Equal(expectedA, cpu.Registers.A);
        Assert.Equal(expectedFlags, cpu.Registers.F);
        Assert.Equal(0x0101, cpu.Registers.PC);
    }

    [Theory]
    [InlineData(0x00, 0x00, 0x00, 0x80)]
    [InlineData(0x0A, 0x00, 0x10, 0x00)]
    [InlineData(0x10, 0x20, 0x16, 0x00)]
    [InlineData(0xA0, 0x00, 0x00, 0x90)]
    [InlineData(0x23, 0x10, 0x83, 0x10)]
    [InlineData(0x0F, 0x60, 0x09, 0x40)]
    [InlineData(0x33, 0x50, 0xD3, 0x50)]
    [InlineData(0x00, 0x70, 0x9A, 0x50)]
    public void Step_DecimalAdjustsAccumulatorAndUpdatesFlags(
        byte initialA,
        byte initialFlags,
        byte expectedA,
        byte expectedFlags
    )
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = 0x27);
        cpu.Registers.A = initialA;
        cpu.Registers.F = initialFlags;

        int machineCycles = cpu.Step();

        Assert.Equal(1, machineCycles);
        Assert.Equal(expectedA, cpu.Registers.A);
        Assert.Equal(expectedFlags, cpu.Registers.F);
        Assert.Equal(0x0101, cpu.Registers.PC);
    }

    [Theory]
    [InlineData(0x55, 0x90, 0xAA, 0xF0)]
    [InlineData(0xFF, 0x00, 0x00, 0x60)]
    public void Step_ComplementsAccumulatorAndUpdatesFlags(
        byte initialA,
        byte initialFlags,
        byte expectedA,
        byte expectedFlags
    )
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = 0x2F);
        cpu.Registers.A = initialA;
        cpu.Registers.F = initialFlags;

        int machineCycles = cpu.Step();

        Assert.Equal(1, machineCycles);
        Assert.Equal(expectedA, cpu.Registers.A);
        Assert.Equal(expectedFlags, cpu.Registers.F);
        Assert.Equal(0x0101, cpu.Registers.PC);
    }

    [Theory]
    [InlineData(0x37, 0xE0, 0x90)]
    [InlineData(0x37, 0x00, 0x10)]
    [InlineData(0x3F, 0xF0, 0x80)]
    [InlineData(0x3F, 0x80, 0x90)]
    public void Step_UpdatesCarryFlagOperations(byte opcode, byte initialFlags, byte expectedFlags)
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = opcode);
        cpu.Registers.A = 0x42;
        cpu.Registers.F = initialFlags;

        int machineCycles = cpu.Step();

        Assert.Equal(1, machineCycles);
        Assert.Equal(0x42, cpu.Registers.A);
        Assert.Equal(expectedFlags, cpu.Registers.F);
        Assert.Equal(0x0101, cpu.Registers.PC);
    }

    [Fact]
    public void Step_LoadsThroughHlAndUpdatesHl()
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes =>
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
        Cpu cpu = CpuTestFactory.CreateCpu(bytes =>
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
        Cpu cpu = CpuTestFactory.CreateCpu(bytes =>
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
        Cpu cpu = CpuTestFactory.CreateCpu(bytes =>
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
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = opcode);
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
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = opcode);
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
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = opcode);
        cpu.Registers.HL = initialHl;
        cpu.Registers.SetRegisterPair((RegisterPair)sourceRegisterPair, sourceValue);
        cpu.Registers.F = initialFlags;

        int machineCycles = cpu.Step();

        Assert.Equal(2, machineCycles);
        Assert.Equal(expectedHl, cpu.Registers.HL);
        Assert.Equal(expectedFlags, cpu.Registers.F);
        Assert.Equal(0x0101, cpu.Registers.PC);
    }

    [Theory]
    [InlineData(0x76, "Opcode 0x76 is not supported yet.")]
    [InlineData(0xFF, "Opcode 0xFF is not supported yet.")]
    public void Step_RejectsUnsupportedOpcode(byte opcode, string expectedMessage)
    {
        Cpu cpu = CpuTestFactory.CreateCpu(bytes => bytes[0x0100] = opcode);

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => cpu.Step());

        Assert.Equal(expectedMessage, exception.Message);
    }
}
