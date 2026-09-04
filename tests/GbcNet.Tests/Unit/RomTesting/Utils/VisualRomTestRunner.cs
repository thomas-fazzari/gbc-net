// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core;
using GbcNet.Core.Hardware;
using GbcNet.Core.Ppu;

namespace GbcNet.Tests.Unit.RomTesting.Utils;

/// <summary>
/// Runs a ROM until a requested frame count or M-cycle budget is reached.
/// </summary>
internal static class VisualRomTestRunner
{
    /// <summary>
    /// Retains the latest completed frame while advancing one emulation step at a time.
    /// </summary>
    /// <param name="rom">The complete ROM image.</param>
    /// <param name="targetFrame">The positive number of completed frames to wait for.</param>
    /// <param name="maxMachineCycles">
    /// The soft M-cycle limit. The final emulation step may carry the total past this value.
    /// </param>
    /// <param name="hardwareModel">The emulated hardware model.</param>
    /// <returns>A disposable result containing the latest frame and progress counters.</returns>
    public static VisualRomTestResult RunToFrame(
        byte[] rom,
        int targetFrame,
        int maxMachineCycles,
        HardwareModel hardwareModel = HardwareModel.Dmg
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetFrame);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxMachineCycles);

        var cartridge = TestRomFactory.LoadCartridge(rom);
        var gameBoy = new GameBoy(cartridge, hardwareModel);
        LcdFrame? frame = null;
        var frameCount = 0;
        gameBoy.FrameCompleted += completedFrame =>
        {
            frame?.Dispose();
            frame = completedFrame;
            frameCount++;
        };

        var machineCycles = 0;
        while (machineCycles < maxMachineCycles && frameCount < targetFrame)
        {
            machineCycles += gameBoy.Step();
        }

        return new VisualRomTestResult(frame, frameCount, machineCycles);
    }
}
