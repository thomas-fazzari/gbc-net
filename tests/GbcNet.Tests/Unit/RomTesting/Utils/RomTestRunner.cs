// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core;
using GbcNet.Core.Hardware;
using GbcNet.Tests.Unit.RomTesting.Utils.ResultObservers;

namespace GbcNet.Tests.Unit.RomTesting.Utils;

/// <summary>
/// Runs test ROMs until their protocol reports a terminal state or their M-cycle budget expires.
/// </summary>
internal static class RomTestRunner
{
    /// <summary>
    /// Runs one ROM and combines the final snapshots from its result channels.
    /// </summary>
    /// <param name="rom">The complete ROM image.</param>
    /// <param name="maxMachineCycles">
    /// The soft M-cycle limit. The final emulation step may carry the total past this value.
    /// </param>
    /// <param name="protocol">The protocol used to detect pass and fail reports.</param>
    /// <param name="hardwareModel">The emulated hardware model.</param>
    /// <returns>The terminal or timed-out ROM result.</returns>
    public static RomTestResult Run(
        byte[] rom,
        int maxMachineCycles,
        RomTestProtocol protocol = RomTestProtocol.Blargg,
        HardwareModel hardwareModel = HardwareModel.Dmg
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxMachineCycles);

        var cartridge = TestRomFactory.LoadCartridge(rom);
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

        return new RomTestResult(RomTestStatus.TimedOut, machineCycles, GetSnapshots(observers));
    }

    /// <summary>
    /// Runs a set of ROM paths in parallel and indexes each result by its relative path.
    /// </summary>
    /// <param name="relativePaths">The stable path keys to run.</param>
    /// <param name="run">The thread-safe operation that runs one path.</param>
    /// <returns>A case-sensitive result map.</returns>
    public static IReadOnlyDictionary<string, RomTestResult> RunAll(
        IReadOnlyList<string> relativePaths,
        Func<string, RomTestResult> run
    )
    {
        ArgumentNullException.ThrowIfNull(relativePaths);
        ArgumentNullException.ThrowIfNull(run);

        return relativePaths
            .AsParallel()
            .WithDegreeOfParallelism(Environment.ProcessorCount)
            .ToDictionary(static relativePath => relativePath, run, StringComparer.Ordinal);
    }

    /// <summary>
    /// Creates xUnit theory rows and lazily runs every ROM when the result map is first read.
    /// </summary>
    /// <param name="relativePaths">Paths relative to <paramref name="romDirectory"/>.</param>
    /// <param name="romDirectory">The directory that contains the ROM files.</param>
    /// <param name="maxMachineCycles">The soft M-cycle limit for each ROM.</param>
    /// <param name="protocol">The protocol used to detect pass and fail reports.</param>
    /// <param name="hardwareModel">The emulated hardware model.</param>
    public static RomSuite CreateSuite(
        IReadOnlyList<string> relativePaths,
        string romDirectory,
        int maxMachineCycles,
        RomTestProtocol protocol = RomTestProtocol.Blargg,
        HardwareModel hardwareModel = HardwareModel.Dmg
    )
    {
        ArgumentNullException.ThrowIfNull(relativePaths);
        ArgumentNullException.ThrowIfNull(romDirectory);

        var rows = new TheoryData<string>();
        foreach (var relativePath in relativePaths)
        {
            rows.Add(relativePath);
        }

        return new(
            rows,
            new Lazy<IReadOnlyDictionary<string, RomTestResult>>(() =>
                RunAll(
                    relativePaths,
                    relativePath =>
                    {
                        var romPath = Path.Combine(romDirectory, relativePath);
                        var rom = File.ReadAllBytes(romPath);

                        return Run(rom, maxMachineCycles, protocol, hardwareModel);
                    }
                )
            )
        );
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
            return new RomTestResult(
                RomTestStatus.Failed,
                machineCycles,
                GetSnapshots(observers),
                "ROM result observers disagree."
            );
        }

        return new RomTestResult(
            terminalObservations[0].Status.GetValueOrDefault(),
            machineCycles,
            GetSnapshots(observers)
        );
    }

    private static RomTestObservation[] GetSnapshots(IReadOnlyList<IRomResultObserver> observers) =>
        [.. observers.Select(observer => observer.Snapshot)];
}
