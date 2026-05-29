using System.Globalization;
using System.Text;
using GbcNet.Core;
using GbcNet.Core.Sm83;

namespace GbcNet.Tests.RomTesting.Utils.ResultObservers;

internal sealed class MooneyeBreakpointResultObserver : IRomResultObserver, ICpuInstructionObserver
{
    private const string Source = "Mooneye breakpoint";
    private const byte LoadBFromBOpcode = 0x40;
    private const byte FailureByte = 0x42;

    private static readonly byte[] PassReport = [0x03, 0x05, 0x08, 0x0D, 0x15, 0x22];

    public MooneyeBreakpointResultObserver(GameBoy gameBoy)
    {
        gameBoy.Cpu.InstructionObserver = this;
    }

    public RomTestObservation Snapshot { get; private set; } = new(Source);

    public RomTestObservation? Observe() => Snapshot.Status is null ? null : Snapshot;

    void ICpuInstructionObserver.OnInstructionExecuted(byte opcode, Registers registers)
    {
        if (opcode is not LoadBFromBOpcode)
        {
            return;
        }

        Span<byte> report =
        [
            registers.B,
            registers.C,
            registers.D,
            registers.E,
            registers.H,
            registers.L,
        ];
        RomTestStatus? status = GetStatus(report);
        if (status is not null)
        {
            Snapshot = new RomTestObservation(Source, status, FormatReport(report));
        }
    }

    private static RomTestStatus? GetStatus(ReadOnlySpan<byte> report)
    {
        if (report.SequenceEqual(PassReport))
        {
            return RomTestStatus.Passed;
        }

        foreach (byte value in report)
        {
            if (value is not FailureByte)
            {
                return null;
            }
        }

        return RomTestStatus.Failed;
    }

    private static string FormatReport(ReadOnlySpan<byte> report)
    {
        var output = new StringBuilder();
        for (int index = 0; index < report.Length; index++)
        {
            if (index > 0)
            {
                output.Append(' ');
            }

            output.Append(report[index].ToString("X2", CultureInfo.InvariantCulture));
        }

        return output.ToString();
    }
}
