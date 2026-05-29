using GbcNet.Core;
using GbcNet.Core.Cartridges;
using GbcNet.Tests.RomTesting.Utils.ResultObservers;

namespace GbcNet.Tests.RomTesting.Utils;

internal static class RomTestRunner
{
    public static RomTestResult Run(byte[] rom, int maxMachineCycles)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxMachineCycles);

        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        IRomResultObserver[] observers =
        [
            new BlarggSerialResultObserver(gameBoy),
            new BlarggMemoryResultObserver(gameBoy),
        ];
        int machineCycles = 0;

        while (machineCycles < maxMachineCycles)
        {
            machineCycles += gameBoy.Step();

            RomTestResult? result = CreateTerminalResult(observers, machineCycles);
            if (result is not null)
            {
                return result;
            }
        }

        return RomTestResult.TimedOut(machineCycles, GetSnapshots(observers));
    }

    private static RomTestResult? CreateTerminalResult(
        IReadOnlyList<IRomResultObserver> observers,
        int machineCycles
    )
    {
        RomTestObservation[] terminalObservations =
        [
            .. observers.Select(observer => observer.Observe()).OfType<RomTestObservation>(),
        ];

        if (terminalObservations.Length == 0)
        {
            return null;
        }

        if (terminalObservations.Select(result => result.Status).Distinct().Skip(1).Any())
        {
            return RomTestResult.FromObservations(
                RomTestStatus.Failed,
                machineCycles,
                terminalObservations,
                "ROM result observers disagree."
            );
        }

        return RomTestResult.FromObservations(
            terminalObservations[0].Status.GetValueOrDefault(),
            machineCycles,
            terminalObservations
        );
    }

    private static RomTestObservation[] GetSnapshots(IReadOnlyList<IRomResultObserver> observers) =>
        [.. observers.Select(observer => observer.Snapshot)];
}
