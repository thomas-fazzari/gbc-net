// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core;
using GbcNet.Core.Hardware;
using GbcNet.Core.Ppu;
using GbcNet.Tests.Cartridges;

namespace GbcNet.Tests.RomTesting.Utils;

internal static class VisualRomTestRunner
{
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
