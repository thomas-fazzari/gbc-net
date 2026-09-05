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
