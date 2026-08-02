// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Sm83;

/// <summary>
/// Stores SM83 CPU registers and exposes 16-bit register pairs.
/// </summary>
internal sealed class Registers
{
    /// <summary>
    /// Keeps only the four SM83 flag bits stored in F.
    /// </summary>
    private const byte FlagsMask = 0xF0;

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
        get;
        set => field = (byte)(value & FlagsMask);
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
    /// Captures the register file without allocating.
    /// </summary>
    internal RegistersState CaptureState() => new(AF, BC, DE, HL, PC, SP);

    /// <summary>
    /// Restores the register file from a captured state.
    /// </summary>
    internal void RestoreState(RegistersState state)
    {
        AF = state.AF;
        BC = state.BC;
        DE = state.DE;
        HL = state.HL;
        PC = state.PC;
        SP = state.SP;
    }

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
    /// Evaluates an SM83 condition code against the current Z and C flags.
    /// </summary>
    public bool IsConditionMet(ConditionCode conditionCode) =>
        conditionCode switch
        {
            ConditionCode.NotZero => !IsFlagSet(CpuFlag.Zero),
            ConditionCode.Zero => IsFlagSet(CpuFlag.Zero),
            ConditionCode.NotCarry => !IsFlagSet(CpuFlag.Carry),
            ConditionCode.Carry => IsFlagSet(CpuFlag.Carry),
            _ => throw new ArgumentOutOfRangeException(nameof(conditionCode)),
        };

    /// <summary>
    /// Reads an SM83 r8 register.
    /// </summary>
    public byte GetRegister(Register8 register) =>
        register switch
        {
            Register8.B => B,
            Register8.C => C,
            Register8.D => D,
            Register8.E => E,
            Register8.H => H,
            Register8.L => L,
            Register8.A => A,
            _ => throw new ArgumentOutOfRangeException(nameof(register)),
        };

    /// <summary>
    /// Writes an SM83 r8 register.
    /// </summary>
    public void SetRegister(Register8 register, byte value)
    {
        switch (register)
        {
            case Register8.B:
                B = value;
                return;
            case Register8.C:
                C = value;
                return;
            case Register8.D:
                D = value;
                return;
            case Register8.E:
                E = value;
                return;
            case Register8.H:
                H = value;
                return;
            case Register8.L:
                L = value;
                return;
            case Register8.A:
                A = value;
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(register));
        }
    }

    /// <summary>
    /// Reads an SM83 r16 register pair.
    /// </summary>
    public ushort GetRegisterPair(RegisterPair registerPair) =>
        registerPair switch
        {
            RegisterPair.BC => BC,
            RegisterPair.DE => DE,
            RegisterPair.HL => HL,
            RegisterPair.SP => SP,
            _ => throw new ArgumentOutOfRangeException(nameof(registerPair)),
        };

    /// <summary>
    /// Writes an SM83 r16 register pair.
    /// </summary>
    public void SetRegisterPair(RegisterPair registerPair, ushort value)
    {
        switch (registerPair)
        {
            case RegisterPair.BC:
                BC = value;
                return;
            case RegisterPair.DE:
                DE = value;
                return;
            case RegisterPair.HL:
                HL = value;
                return;
            case RegisterPair.SP:
                SP = value;
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(registerPair));
        }
    }

    /// <summary>
    /// Reads an SM83 r16stk register pair.
    /// </summary>
    public ushort GetStackRegisterPair(StackRegisterPair registerPair) =>
        registerPair switch
        {
            StackRegisterPair.BC => BC,
            StackRegisterPair.DE => DE,
            StackRegisterPair.HL => HL,
            StackRegisterPair.AF => AF,
            _ => throw new ArgumentOutOfRangeException(nameof(registerPair)),
        };

    /// <summary>
    /// Writes an SM83 r16stk register pair.
    /// </summary>
    public void SetStackRegisterPair(StackRegisterPair registerPair, ushort value)
    {
        switch (registerPair)
        {
            case StackRegisterPair.BC:
                BC = value;
                return;
            case StackRegisterPair.DE:
                DE = value;
                return;
            case StackRegisterPair.HL:
                HL = value;
                return;
            case StackRegisterPair.AF:
                AF = value;
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
[Flags]
internal enum CpuFlag : byte
{
    /// <summary>
    /// No flags set.
    /// </summary>
    None = 0,

    /// <summary>
    /// Carry flag, bit 4.
    /// </summary>
    Carry = 1 << 4,

    /// <summary>
    /// Half-carry flag, bit 5.
    /// </summary>
    HalfCarry = 1 << 5,

    /// <summary>
    /// Subtract flag, bit 6.
    /// </summary>
    Subtract = 1 << 6,

    /// <summary>
    /// Zero flag, bit 7.
    /// </summary>
    Zero = 1 << 7,
}

/// <summary>
/// SM83 8-bit CPU registers used by r8 instructions.
/// </summary>
/// <remarks>
/// Values match the r8 opcode encoding for real registers; encoding value 6 is [HL], not a CPU
/// register.
/// </remarks>
internal enum Register8 : byte
{
    /// <summary>
    /// B register.
    /// </summary>
    B = 0,

    /// <summary>
    /// C register.
    /// </summary>
    C = 1,

    /// <summary>
    /// D register.
    /// </summary>
    D = 2,

    /// <summary>
    /// E register.
    /// </summary>
    E = 3,

    /// <summary>
    /// H register.
    /// </summary>
    H = 4,

    /// <summary>
    /// L register.
    /// </summary>
    L = 5,

    /// <summary>
    /// Accumulator register.
    /// </summary>
    A = 7,
}

/// <summary>
/// SM83 r8 instruction operands encoded in opcode bits.
/// </summary>
internal enum Register8Operand : byte
{
    /// <summary>
    /// B register.
    /// </summary>
    B = 0,

    /// <summary>
    /// C register.
    /// </summary>
    C = 1,

    /// <summary>
    /// D register.
    /// </summary>
    D = 2,

    /// <summary>
    /// E register.
    /// </summary>
    E = 3,

    /// <summary>
    /// H register.
    /// </summary>
    H = 4,

    /// <summary>
    /// L register.
    /// </summary>
    L = 5,

    /// <summary>
    /// Byte stored at the address in HL.
    /// </summary>
    AddressHl = 6,

    /// <summary>
    /// Accumulator register.
    /// </summary>
    A = 7,
}

/// <summary>
/// SM83 16-bit register pairs used by r16 instructions.
/// </summary>
internal enum RegisterPair : byte
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

/// <summary>
/// SM83 16-bit register pairs used by stack instructions.
/// </summary>
internal enum StackRegisterPair : byte
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
    /// Accumulator and flags register pair.
    /// </summary>
    AF = 3,
}

/// <summary>
/// Captures the SM83 register file.
/// </summary>
internal readonly record struct RegistersState(
    ushort AF,
    ushort BC,
    ushort DE,
    ushort HL,
    ushort PC,
    ushort SP
);
