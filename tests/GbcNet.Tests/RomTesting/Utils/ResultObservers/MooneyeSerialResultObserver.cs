using System.Globalization;
using System.Runtime.InteropServices;
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

    private bool ContainsReport(ReadOnlySpan<byte> report) =>
        CollectionsMarshal.AsSpan(_output).IndexOf(report) >= 0;

    private string FormatOutput() =>
        string.Join(
            ' ',
            _output.Select(value => value.ToString("X2", CultureInfo.InvariantCulture))
        );
}
