namespace GbcNet.Core.Sm83.Instructions;

internal static partial class Arithmetic8Instructions
{
    private const byte DecimalLowAdjust = 0x06;
    private const byte DecimalDigitMax = 9;
    private const byte DecimalHighAdjust = 0x60;
    private const byte DecimalHighCarryThreshold = 0x9F;

    private static void DecimalAdjustAccumulator(Cpu cpu)
    {
        int value = cpu.Registers.A;
        var subtract = cpu.Registers.IsFlagSet(CpuFlag.Subtract);
        var carry = cpu.Registers.IsFlagSet(CpuFlag.Carry);
        var halfCarry = cpu.Registers.IsFlagSet(CpuFlag.HalfCarry);

        // DAA uses flags from the previous arithmetic operation to normalize A as packed BCD.
        if (subtract)
        {
            if (halfCarry)
            {
                value -= DecimalLowAdjust;
            }

            if (carry)
            {
                value -= DecimalHighAdjust;
            }
        }
        else
        {
            if (halfCarry || (value & HalfCarryMask) > DecimalDigitMax)
            {
                value += DecimalLowAdjust;
            }

            if (carry || value > DecimalHighCarryThreshold)
            {
                value += DecimalHighAdjust;
                carry = true;
            }
        }

        var result = unchecked((byte)value);
        cpu.Registers.A = result;
        cpu.Registers.SetFlag(CpuFlag.Zero, result == 0);
        cpu.Registers.SetFlag(CpuFlag.HalfCarry, isSet: false);
        cpu.Registers.SetFlag(CpuFlag.Carry, carry);
    }

    private static void ComplementAccumulator(Cpu cpu)
    {
        cpu.Registers.A = (byte)~cpu.Registers.A;
        cpu.Registers.SetFlag(CpuFlag.Subtract, isSet: true);
        cpu.Registers.SetFlag(CpuFlag.HalfCarry, isSet: true);
    }
}
