using GbcNet.Core.Hardware;
using GbcNet.Tests.RomTesting.Utils;

namespace GbcNet.Tests.RomTesting.Blargg;

[Collection<RomTestingGroup>]
public sealed class BlarggInterruptTimingRomTests
{
    private const string RomPath = "RomTesting/Resources/Blargg/interrupt_time/interrupt_time.gb";
    private const int MaxMachineCycles = 50_000_000;

    [Fact]
    public void InterruptTimingRomPasses()
    {
        byte[] rom = File.ReadAllBytes(RomPath);

        RomTestResult result = RomTestRunner.Run(
            rom,
            MaxMachineCycles,
            hardwareModel: HardwareModel.Cgb
        );

        RomTestAssertions.AssertPassed(result);
    }
}
