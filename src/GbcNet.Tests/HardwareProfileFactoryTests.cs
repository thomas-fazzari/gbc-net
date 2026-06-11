using GbcNet.Core.Cartridges;
using GbcNet.Core.Hardware;
using GbcNet.Core.Hardware.Profiles;
using GbcNet.Tests.Cartridges;

namespace GbcNet.Tests;

public sealed class HardwareProfileFactoryTests
{
    [Theory]
    [InlineData(CgbSupport.None)]
    [InlineData(CgbSupport.Enhanced)]
    public void Create_ReturnsDmgProfileForDmgHardwareWhenCartridgeAllowsDmg(CgbSupport cgbSupport)
    {
        CartridgeHeader header = CreateHeader(cgbSupport);

        IHardwareProfile profile = HardwareProfileFactory.Create(HardwareModel.Dmg, header);

        Assert.Same(DmgHardwareProfile.Instance, profile);
        Assert.Equal(HardwareModel.Dmg, profile.Model);
        Assert.Equal(1, profile.VideoRamBankCount);
        Assert.False(profile.IsColorPaletteRamEnabled);
        Assert.Equal(2, profile.WorkRamBankCount);
    }

    [Fact]
    public void Create_RejectsCgbRequiredCartridgeForDmgHardware()
    {
        CartridgeHeader header = CreateHeader(CgbSupport.Required);

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() =>
            HardwareProfileFactory.Create(HardwareModel.Dmg, header)
        );

        Assert.Contains("CGB-required", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CgbSupport.Enhanced)]
    [InlineData(CgbSupport.Required)]
    public void Create_ReturnsCgbModeProfileForCgbHardwareWhenCartridgeUsesCgb(
        CgbSupport cgbSupport
    )
    {
        CartridgeHeader header = CreateHeader(cgbSupport);

        IHardwareProfile profile = HardwareProfileFactory.Create(HardwareModel.Cgb, header);

        CgbHardwareProfile cgbProfile = Assert.IsType<CgbHardwareProfile>(profile);
        Assert.Equal(HardwareModel.Cgb, cgbProfile.Model);
        Assert.Equal(CgbOperatingMode.Cgb, cgbProfile.OperatingMode);
        Assert.Equal(2, cgbProfile.VideoRamBankCount);
        Assert.True(cgbProfile.IsColorPaletteRamEnabled);
        Assert.Equal(8, cgbProfile.WorkRamBankCount);
    }

    [Fact]
    public void Create_ReturnsDmgCompatibilityProfileForCgbHardwareWhenCartridgeIsDmgOnly()
    {
        CartridgeHeader header = CreateHeader(CgbSupport.None);

        IHardwareProfile profile = HardwareProfileFactory.Create(HardwareModel.Cgb, header);

        CgbHardwareProfile cgbProfile = Assert.IsType<CgbHardwareProfile>(profile);
        Assert.Equal(HardwareModel.Cgb, cgbProfile.Model);
        Assert.Equal(CgbOperatingMode.DmgCompatibility, cgbProfile.OperatingMode);
        Assert.Equal(1, cgbProfile.VideoRamBankCount);
        Assert.False(cgbProfile.IsColorPaletteRamEnabled);
        Assert.Equal(8, cgbProfile.WorkRamBankCount);
    }

    private static CartridgeHeader CreateHeader(CgbSupport cgbSupport)
    {
        byte cgbFlag = cgbSupport switch
        {
            CgbSupport.None => 0x00,
            CgbSupport.Enhanced => 0x80,
            CgbSupport.Required => 0xC0,
            _ => throw new ArgumentOutOfRangeException(
                nameof(cgbSupport),
                cgbSupport,
                "Unsupported CGB support value."
            ),
        };

        Cartridge cartridge = ResultAssertions.AssertSuccess(
            Cartridge.Load(TestRomFactory.Create(rom => rom[0x0143] = cgbFlag))
        );

        Assert.Equal(cgbSupport, cartridge.Header.CgbSupport);
        return cartridge.Header;
    }
}
