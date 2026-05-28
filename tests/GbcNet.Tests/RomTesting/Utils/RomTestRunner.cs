using System.Text;
using GbcNet.Core;
using GbcNet.Core.Cartridges;

namespace GbcNet.Tests.RomTesting.Utils;

internal static class RomTestRunner
{
    private const string PassedMarker = "Passed";
    private const string FailedMarker = "Failed";

    public static RomTestResult Run(byte[] rom, int maxMachineCycles)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxMachineCycles);

        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        var serialOutput = new StringBuilder();
        string output = string.Empty;
        int machineCycles = 0;

        gameBoy.SerialByteTransferred += (_, e) =>
        {
            serialOutput.Append((char)e.Value);
            output = serialOutput.ToString();
        };

        while (machineCycles < maxMachineCycles)
        {
            machineCycles += gameBoy.Step();

            if (output.Contains(PassedMarker, StringComparison.Ordinal))
            {
                return new RomTestResult(RomTestStatus.Passed, output, machineCycles);
            }

            if (output.Contains(FailedMarker, StringComparison.Ordinal))
            {
                return new RomTestResult(RomTestStatus.Failed, output, machineCycles);
            }
        }

        return new RomTestResult(RomTestStatus.TimedOut, output, machineCycles);
    }
}
