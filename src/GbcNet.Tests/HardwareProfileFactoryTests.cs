using GbcNet.Core.Cartridges;
using GbcNet.Core.Hardware;
using GbcNet.Core.Hardware.Profiles;
using GbcNet.Tests.Cartridges;

namespace GbcNet.Tests;

public sealed class HardwareProfileFactoryTests
{
    [Fact]
    public void Create_ReturnsDmgProfileForDmgHardware()
    {
        Cartridge cartridge = ResultAssertions.AssertSuccess(
            Cartridge.Load(TestRomFactory.Create())
        );

        IHardwareProfile profile = HardwareProfileFactory.Create(
            HardwareModel.Dmg,
            cartridge.Header
        );

        Assert.Same(DmgHardwareProfile.Instance, profile);
        Assert.Equal(HardwareModel.Dmg, profile.Model);
        Assert.Equal(2, profile.WorkRamBankCount);
    }
}
