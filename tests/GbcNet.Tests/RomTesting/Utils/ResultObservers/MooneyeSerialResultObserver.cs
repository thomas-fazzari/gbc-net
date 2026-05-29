using System.Globalization;
using GbcNet.Core;

namespace GbcNet.Tests.RomTesting.Utils.ResultObservers;

internal sealed class MooneyeSerialResultObserver : IRomResultObserver
{
    private const string Source = "Mooneye serial";
    private static readonly byte[] PassReport = [0x03, 0x05, 0x08, 0x0D, 0x15, 0x22];
    private static readonly byte[] FailReport = [0x42, 0x42, 0x42, 0x42, 0x42, 0x42];

    private readonly List<byte> _output = [];

    public MooneyeSerialResultObserver(GameBoy gameBoy)
    {
        gameBoy.SerialByteTransferred += (_, e) => _output.Add(e.Value);
    }

    public RomTestObservation Snapshot => new(Source, Output: FormatOutput());

    public RomTestObservation? Observe()
    {
        if (ContainsReport(PassReport))
        {
            return new RomTestObservation(Source, RomTestStatus.Passed, FormatOutput());
        }

        return ContainsReport(FailReport)
            ? new RomTestObservation(Source, RomTestStatus.Failed, FormatOutput())
            : null;
    }

    private bool ContainsReport(ReadOnlySpan<byte> report)
    {
        for (int offset = 0; offset <= _output.Count - report.Length; offset++)
        {
            if (MatchesReportAt(offset, report))
            {
                return true;
            }
        }

        return false;
    }

    private bool MatchesReportAt(int offset, ReadOnlySpan<byte> report)
    {
        for (int index = 0; index < report.Length; index++)
        {
            if (_output[offset + index] != report[index])
            {
                return false;
            }
        }

        return true;
    }

    private string FormatOutput() =>
        string.Join(
            ' ',
            _output.Select(value => value.ToString("X2", CultureInfo.InvariantCulture))
        );
}
