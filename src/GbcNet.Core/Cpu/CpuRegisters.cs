namespace GbcNet.Core.Cpu;

/// <summary>
/// Stores SM83 CPU registers and exposes 16-bit register pairs.
/// </summary>
internal sealed class CpuRegisters
{
    /// <summary>
    /// Keeps only the four SM83 flag bits stored in F.
    /// </summary>
    private const byte FlagsMask = 0xF0;

    /// <summary>
    /// Stores F while forcing unused bits 3-0 to zero.
    /// </summary>
    private byte _flagsRegister;

    /// <summary>
    /// Accumulator register.
    /// </summary>
    public byte A { get; set; }

    /// <summary>
    /// High byte of BC.
    /// </summary>
    public byte B { get; set; }

    /// <summary>
    /// Low byte of BC.
    /// </summary>
    public byte C { get; set; }

    /// <summary>
    /// High byte of DE.
    /// </summary>
    public byte D { get; set; }

    /// <summary>
    /// Low byte of DE.
    /// </summary>
    public byte E { get; set; }

    /// <summary>
    /// High byte of HL.
    /// </summary>
    public byte H { get; set; }

    /// <summary>
    /// Low byte of HL.
    /// </summary>
    public byte L { get; set; }

    /// <summary>
    /// Flags register.
    /// </summary>
    /// <remarks>
    /// Only bits 7-4 are used by SM83.
    /// </remarks>
    public byte F
    {
        get => _flagsRegister;
        set => _flagsRegister = (byte)(value & FlagsMask);
    }

    /// <summary>
    /// Accumulator and flags register pair.
    /// </summary>
    public ushort AF
    {
        get => JoinBytes(A, F);
        set
        {
            A = HighByte(value);
            F = LowByte(value);
        }
    }

    /// <summary>
    /// BC register pair.
    /// </summary>
    public ushort BC
    {
        get => JoinBytes(B, C);
        set
        {
            B = HighByte(value);
            C = LowByte(value);
        }
    }

    /// <summary>
    /// DE register pair.
    /// </summary>
    public ushort DE
    {
        get => JoinBytes(D, E);
        set
        {
            D = HighByte(value);
            E = LowByte(value);
        }
    }

    /// <summary>
    /// HL register pair.
    /// </summary>
    public ushort HL
    {
        get => JoinBytes(H, L);
        set
        {
            H = HighByte(value);
            L = LowByte(value);
        }
    }

    /// <summary>
    /// Program counter.
    /// </summary>
    public ushort PC { get; set; }

    /// <summary>
    /// Stack pointer.
    /// </summary>
    public ushort SP { get; set; }

    /// <summary>
    /// Returns whether a CPU flag is set in F.
    /// </summary>
    public bool IsFlagSet(CpuFlag flag) => (F & (byte)flag) != 0;

    /// <summary>
    /// Sets or clears a CPU flag while preserving the other flag bits.
    /// </summary>
    public void SetFlag(CpuFlag flag, bool isSet)
    {
        F = isSet ? (byte)(F | (byte)flag) : (byte)(F & ~(byte)flag);
    }

    /// <summary>
    /// Reads an SM83 r16 register pair.
    /// </summary>
    public ushort GetRegisterPair(Sm83RegisterPair registerPair) =>
        registerPair switch
        {
            Sm83RegisterPair.BC => BC,
            Sm83RegisterPair.DE => DE,
            Sm83RegisterPair.HL => HL,
            Sm83RegisterPair.SP => SP,
            _ => throw new ArgumentOutOfRangeException(nameof(registerPair)),
        };

    /// <summary>
    /// Writes an SM83 r16 register pair.
    /// </summary>
    public void SetRegisterPair(Sm83RegisterPair registerPair, ushort value)
    {
        switch (registerPair)
        {
            case Sm83RegisterPair.BC:
                BC = value;
                return;
            case Sm83RegisterPair.DE:
                DE = value;
                return;
            case Sm83RegisterPair.HL:
                HL = value;
                return;
            case Sm83RegisterPair.SP:
                SP = value;
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(registerPair));
        }
    }

    /// <summary>
    /// Gets the high byte of a 16-bit register pair value.
    /// </summary>
    private static byte HighByte(ushort value) => (byte)(value >> 8);

    /// <summary>
    /// Gets the low byte of a 16-bit register pair value.
    /// </summary>
    private static byte LowByte(ushort value) => (byte)value;

    /// <summary>
    /// Combines high and low register bytes into a 16-bit register pair value.
    /// </summary>
    private static ushort JoinBytes(byte highByte, byte lowByte) =>
        (ushort)((highByte << 8) | lowByte);
}

/// <summary>
/// SM83 flags stored in the upper nibble of the F register.
/// </summary>
internal enum CpuFlag : byte
{
    /// <summary>
    /// Carry flag, bit 4.
    /// </summary>
    Carry = 0x10,

    /// <summary>
    /// Half-carry flag, bit 5.
    /// </summary>
    HalfCarry = 0x20,

    /// <summary>
    /// Subtract flag, bit 6.
    /// </summary>
    Subtract = 0x40,

    /// <summary>
    /// Zero flag, bit 7.
    /// </summary>
    Zero = 0x80,
}

/// <summary>
/// SM83 16-bit register pairs used by r16 instructions.
/// </summary>
internal enum Sm83RegisterPair : byte
{
    /// <summary>
    /// BC register pair.
    /// </summary>
    BC = 0,

    /// <summary>
    /// DE register pair.
    /// </summary>
    DE = 1,

    /// <summary>
    /// HL register pair.
    /// </summary>
    HL = 2,

    /// <summary>
    /// Stack pointer.
    /// </summary>
    SP = 3,
}
