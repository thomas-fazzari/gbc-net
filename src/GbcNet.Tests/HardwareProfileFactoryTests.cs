using GbcNet.Core.Apu.Profiles;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Dma.Policies;
using GbcNet.Core.Hardware;
using GbcNet.Core.Hardware.Profiles;
using GbcNet.Core.Ppu.Engines;
using GbcNet.Tests.Cartridges;

namespace GbcNet.Tests;

public sealed class HardwareProfileFactoryTests
{
    [Theory]
    [InlineData(CgbSupport.None)]
    [InlineData(CgbSupport.Enhanced)]
    public void Create_ReturnsDmgProfileForDmgHardwareWhenCartridgeAllowsDmg(CgbSupport cgbSupport)
    {
        var header = CreateHeader(cgbSupport);

        var profile = HardwareProfileFactory.Create(HardwareModel.Dmg, header);

        Assert.Same(DmgHardwareProfile.Instance, profile);
        Assert.Equal(HardwareModel.Dmg, profile.Model);
        Assert.Equal(1, profile.VideoRamBankCount);
        Assert.False(profile.IsVideoRamBankRegisterEnabled);
        Assert.False(profile.IsKey1RegisterEnabled);
        Assert.False(profile.IsColorPaletteRamEnabled);
        Assert.False(profile.IsColorPaletteIndexRegisterEnabled);
        Assert.False(profile.IsCgbHardwareMiscRegisterEnabled);
        Assert.False(profile.IsCgbUndocumentedFf74RegisterEnabled);
        Assert.True(profile.TicksTimerOnTacDisableWhenInputHigh);
        Assert.IsType<DmgPpuEngine>(profile.CreatePpuEngine());
        Assert.IsType<DmgOamDmaTransferPolicy>(profile.CreateOamDmaTransferPolicy());

        var apuProfile = Assert.IsType<DmgApuHardwareProfile>(profile.CreateApuHardwareProfile());

        Assert.Equal(0.999958, apuProfile.GetOutputHighPassChargeFactor(apuProfile.OutputClockHz));
        Assert.Equal(2, profile.WorkRamBankCount);
    }

    [Fact]
    public void Create_RejectsCgbRequiredCartridgeForDmgHardware()
    {
        var header = CreateHeader(CgbSupport.Required);

        var exception = Assert.Throws<NotSupportedException>(() =>
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
        var header = CreateHeader(cgbSupport);

        var profile = HardwareProfileFactory.Create(HardwareModel.Cgb, header);

        var cgbProfile = Assert.IsType<CgbHardwareProfile>(profile);
        Assert.Equal(HardwareModel.Cgb, cgbProfile.Model);
        Assert.Equal(CgbOperatingMode.Cgb, cgbProfile.OperatingMode);
        Assert.Equal(2, cgbProfile.VideoRamBankCount);
        Assert.True(cgbProfile.IsVideoRamBankRegisterEnabled);
        Assert.True(cgbProfile.IsKey1RegisterEnabled);
        Assert.True(cgbProfile.IsColorPaletteRamEnabled);
        Assert.True(cgbProfile.IsColorPaletteIndexRegisterEnabled);
        Assert.True(cgbProfile.IsCgbHardwareMiscRegisterEnabled);
        Assert.True(cgbProfile.IsCgbUndocumentedFf74RegisterEnabled);
        Assert.False(cgbProfile.TicksTimerOnTacDisableWhenInputHigh);
        Assert.IsType<CgbPpuEngine>(cgbProfile.CreatePpuEngine());
        Assert.IsType<CgbOamDmaTransferPolicy>(cgbProfile.CreateOamDmaTransferPolicy());
        var apuProfile = Assert.IsType<CgbApuHardwareProfile>(
            cgbProfile.CreateApuHardwareProfile()
        );
        Assert.Equal(0.998943, apuProfile.GetOutputHighPassChargeFactor(apuProfile.OutputClockHz));
        Assert.Equal(8, cgbProfile.WorkRamBankCount);
    }

    [Fact]
    public void Create_ReturnsDmgCompatibilityProfileForCgbHardwareWhenCartridgeIsDmgOnly()
    {
        var header = CreateHeader(CgbSupport.None);

        var profile = HardwareProfileFactory.Create(HardwareModel.Cgb, header);

        var cgbProfile = Assert.IsType<CgbHardwareProfile>(profile);
        Assert.Equal(HardwareModel.Cgb, cgbProfile.Model);
        Assert.Equal(CgbOperatingMode.DmgCompatibility, cgbProfile.OperatingMode);
        Assert.Equal(1, cgbProfile.VideoRamBankCount);
        Assert.True(cgbProfile.IsVideoRamBankRegisterEnabled);
        Assert.False(cgbProfile.IsKey1RegisterEnabled);
        Assert.False(cgbProfile.IsColorPaletteRamEnabled);
        Assert.True(cgbProfile.IsColorPaletteIndexRegisterEnabled);
        Assert.True(cgbProfile.IsCgbHardwareMiscRegisterEnabled);
        Assert.False(cgbProfile.IsCgbUndocumentedFf74RegisterEnabled);
        Assert.False(cgbProfile.TicksTimerOnTacDisableWhenInputHigh);
        Assert.IsType<CgbDmgCompatibilityPpuEngine>(cgbProfile.CreatePpuEngine());
        Assert.IsType<CgbOamDmaTransferPolicy>(cgbProfile.CreateOamDmaTransferPolicy());
        Assert.IsType<CgbApuHardwareProfile>(cgbProfile.CreateApuHardwareProfile());
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

        var cartridge = ResultAssertions.AssertSuccess(
            Cartridge.Load(TestRomFactory.Create(rom => rom[0x0143] = cgbFlag))
        );

        Assert.Equal(cgbSupport, cartridge.Header.CgbSupport);
        return cartridge.Header;
    }
}
