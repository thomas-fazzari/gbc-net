using System.Globalization;
using System.Runtime.InteropServices;
using GbcNet.Core;

namespace GbcNet.Tests.RomTesting.Utils.ResultObservers;

internal sealed class MooneyeSerialResultObserver : IRomResultObserver
{
    private const string Source = "Mooneye serial";
    private const byte FailureByte = 0x42;

    private static readonly byte[] _passReport = [0x03, 0x05, 0x08, 0x0D, 0x15, 0x22];
    private static readonly byte[] _failReport = [.. Enumerable.Repeat(FailureByte, 6)];

    private readonly List<byte> _output = [];

    public MooneyeSerialResultObserver(GameBoy gameBoy)
    {
        gameBoy.SerialByteTransferred += (_, e) => _output.Add(e.TransferredByte);
    }

    public RomTestObservation Snapshot => new(Source, Output: FormatOutput());

    public RomTestObservation? Observe()
    {
        if (ContainsReport(_passReport))
        {
            return new RomTestObservation(Source, RomTestStatus.Passed, FormatOutput());
        }

        return ContainsReport(_failReport)
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
