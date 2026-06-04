using GbcNet.Core;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Hardware;
using GbcNet.Core.Ppu;

namespace GbcNet.Tests.RomTesting.Utils;

internal static class VisualRomTestRunner
{
    public static VisualRomTestResult RunToFrame(byte[] rom, int targetFrame, int maxMachineCycles)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetFrame);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxMachineCycles);

        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        LcdFrame? frame = null;
        int frameCount = 0;
        gameBoy.FrameCompleted += (_, args) =>
        {
            frame = args.Frame;
            frameCount++;
        };

        int machineCycles = 0;
        while (machineCycles < maxMachineCycles && frameCount < targetFrame)
        {
            machineCycles += gameBoy.Step();
        }

        return new VisualRomTestResult(frame, frameCount, machineCycles);
    }
}
