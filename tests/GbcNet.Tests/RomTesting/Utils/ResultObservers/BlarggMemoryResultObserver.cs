using System.Text;
using GbcNet.Core;
using GbcNet.Core.Memory;

namespace GbcNet.Tests.RomTesting.Utils.ResultObservers;

internal sealed class BlarggMemoryResultObserver : IRomResultObserver
{
    private const string Source = "Blargg memory";
    private const byte RunningStatus = 0x80;
    private const byte PassedStatus = 0x00;
    private const byte Signature0 = 0xDE;
    private const byte Signature1 = 0xB0;
    private const byte Signature2 = 0x61;
    private const ushort StatusAddress = AddressMap.ExternalRamStart;
    private const ushort SignatureAddress = StatusAddress + 1;
    private const ushort TextAddress = StatusAddress + 4;
    private const int TextMaxLength =
        AddressMap.ExternalRamWindowSize - (TextAddress - AddressMap.ExternalRamStart);

    private RomTestObservation _snapshot = new(Source);

    public RomTestObservation Snapshot => _snapshot;

    public RomTestObservation? Observe(GameBoy gameBoy)
    {
        if (!HasSignature(gameBoy))
        {
            _snapshot = new RomTestObservation(Source);
            return null;
        }

        byte statusCode = gameBoy.DebugReadByte(StatusAddress);
        string output = ReadOutput(gameBoy);
        RomTestStatus? status = statusCode switch
        {
            RunningStatus => null,
            PassedStatus => RomTestStatus.Passed,
            _ => RomTestStatus.Failed,
        };
        _snapshot = new RomTestObservation(Source, status, output, statusCode);

        return status is null ? null : new RomTestObservation(Source, status, output, statusCode);
    }

    private static bool HasSignature(GameBoy gameBoy) =>
        gameBoy.DebugReadByte(SignatureAddress) == Signature0
        && gameBoy.DebugReadByte(SignatureAddress + 1) == Signature1
        && gameBoy.DebugReadByte(SignatureAddress + 2) == Signature2;

    private static string ReadOutput(GameBoy gameBoy)
    {
        var output = new StringBuilder();
        for (int offset = 0; offset < TextMaxLength; offset++)
        {
            byte value = gameBoy.DebugReadByte((ushort)(TextAddress + offset));
            if (value == 0)
            {
                break;
            }

            output.Append((char)value);
        }

        return output.ToString();
    }
}
