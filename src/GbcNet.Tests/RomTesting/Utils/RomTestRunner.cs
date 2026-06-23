using System.Collections.Concurrent;
using GbcNet.Core;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Hardware;
using GbcNet.Tests.RomTesting.Utils.ResultObservers;

namespace GbcNet.Tests.RomTesting.Utils;

internal static class RomTestRunner
{
    public static RomTestResult Run(
        byte[] rom,
        int maxMachineCycles,
        RomTestProtocol protocol = RomTestProtocol.Blargg,
        HardwareModel hardwareModel = HardwareModel.Dmg
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxMachineCycles);

        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));
        var gameBoy = new GameBoy(cartridge, hardwareModel);
        IRomResultObserver[] observers = protocol switch
        {
            RomTestProtocol.Blargg =>
            [
                new BlarggSerialResultObserver(gameBoy),
                new BlarggExternalRamResultObserver(gameBoy),
            ],
            RomTestProtocol.Mooneye =>
            [
                new MooneyeRegisterSnapshotResultObserver(gameBoy),
                new MooneyeSerialResultObserver(gameBoy),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, message: null),
        };
        var machineCycles = 0;

        while (machineCycles < maxMachineCycles)
        {
            machineCycles += gameBoy.Step();

            var result = CreateTerminalResult(observers, machineCycles);
            if (result is not null)
            {
                return result;
            }
        }

        return RomTestResult.TimedOut(machineCycles, GetSnapshots(observers));
    }

    public static IReadOnlyDictionary<string, RomTestResult> RunAll(
        IReadOnlyList<string> relativePaths,
        Func<string, RomTestResult> run
    )
    {
        ArgumentNullException.ThrowIfNull(relativePaths);
        ArgumentNullException.ThrowIfNull(run);

        var results = new ConcurrentDictionary<string, RomTestResult>(StringComparer.Ordinal);
        Parallel.ForEach(
            relativePaths,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            relativePath => results[relativePath] = run(relativePath)
        );

        return results;
    }

    public static TheoryData<string> CreateTheoryData(IReadOnlyList<string> relativePaths)
    {
        ArgumentNullException.ThrowIfNull(relativePaths);

        var rows = new TheoryData<string>();
        foreach (var relativePath in relativePaths)
        {
            rows.Add(relativePath);
        }

        return rows;
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
                GetSnapshots(observers),
                "ROM result observers disagree."
            );
        }

        return RomTestResult.FromObservations(
            terminalObservations[0].Status.GetValueOrDefault(),
            machineCycles,
            GetSnapshots(observers)
        );
    }

    private static RomTestObservation[] GetSnapshots(IReadOnlyList<IRomResultObserver> observers) =>
        [.. observers.Select(observer => observer.Snapshot)];
}
