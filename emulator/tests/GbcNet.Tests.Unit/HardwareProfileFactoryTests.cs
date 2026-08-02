// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Apu;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Hardware;
using GbcNet.Core.Hardware.Profiles;
using GbcNet.Core.Ppu.Engines;

namespace GbcNet.Tests.Unit;

public sealed class HardwareProfileFactoryTests
{
    [Theory]
    [InlineData(CgbSupport.None)]
    [InlineData(CgbSupport.Enhanced)]
    public void Create_ReturnsDmgProfileForDmgHardwareWhenCartridgeAllowsDmg(CgbSupport cgbSupport)
    {
        var header = CreateHeader(cgbSupport);

        var profile = HardwareProfileFactory.Create(HardwareModel.Dmg, header);

        profile.Should().BeSameAs(DmgHardwareProfile.Instance);
        profile.Model.Should().Be(HardwareModel.Dmg);
        profile.VideoRamBankCount.Should().Be(1);
        profile.IsVideoRamBankRegisterEnabled.Should().BeFalse();
        profile.IsKey1RegisterEnabled.Should().BeFalse();
        profile.IsSerialHighSpeedClockEnabled.Should().BeFalse();
        profile.IsColorPaletteRamEnabled.Should().BeFalse();
        profile.IsColorPaletteIndexRegisterEnabled.Should().BeFalse();
        profile.IsCgbHardwareMiscRegisterEnabled.Should().BeFalse();
        profile.IsCgbUndocumentedFf74RegisterEnabled.Should().BeFalse();
        profile.TicksTimerOnTacDisableWhenInputHigh.Should().BeTrue();
        profile.TicksTimerOnTacEnableWhenInputHigh.Should().BeFalse();
        profile.CreatePpuEngine().Should().BeOfType<DmgPpuEngine>();

        var apuSpec = profile.CreateApuModelSpec();

        apuSpec.Should().Be(ApuModelSpec.Dmg);
        apuSpec.GetOutputHighPassChargeFactor(apuSpec.OutputClockHz).Should().Be(0.999958);
        profile.WorkRamBankCount.Should().Be(2);
    }

    [Fact]
    public void Create_RejectsCgbRequiredCartridgeForDmgHardware()
    {
        var header = CreateHeader(CgbSupport.Required);

        var exception = FluentActions
            .Invoking(() => HardwareProfileFactory.Create(HardwareModel.Dmg, header))
            .Should()
            .ThrowExactly<NotSupportedException>()
            .Which;

        exception.Message.Should().Contain("CGB-required");
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

        var cgbProfile = profile.Should().BeOfType<CgbHardwareProfile>().Subject;
        cgbProfile.Model.Should().Be(HardwareModel.Cgb);
        cgbProfile.OperatingMode.Should().Be(CgbOperatingMode.Cgb);
        cgbProfile.VideoRamBankCount.Should().Be(2);
        cgbProfile.IsVideoRamBankRegisterEnabled.Should().BeTrue();
        cgbProfile.IsKey1RegisterEnabled.Should().BeTrue();
        profile.IsSerialHighSpeedClockEnabled.Should().BeTrue();
        cgbProfile.IsColorPaletteRamEnabled.Should().BeTrue();
        cgbProfile.IsColorPaletteIndexRegisterEnabled.Should().BeTrue();
        cgbProfile.IsCgbHardwareMiscRegisterEnabled.Should().BeTrue();
        cgbProfile.IsCgbUndocumentedFf74RegisterEnabled.Should().BeTrue();
        cgbProfile.TicksTimerOnTacDisableWhenInputHigh.Should().BeFalse();
        cgbProfile.TicksTimerOnTacEnableWhenInputHigh.Should().BeTrue();
        cgbProfile.CreatePpuEngine().Should().BeOfType<CgbPpuEngine>();
        var apuSpec = cgbProfile.CreateApuModelSpec();
        apuSpec.Should().Be(ApuModelSpec.Cgb);
        apuSpec.GetOutputHighPassChargeFactor(apuSpec.OutputClockHz).Should().Be(0.998943);
        cgbProfile.WorkRamBankCount.Should().Be(8);
    }

    [Fact]
    public void Create_ReturnsDmgCompatibilityProfileForCgbHardwareWhenCartridgeIsDmgOnly()
    {
        var header = CreateHeader(CgbSupport.None);

        var profile = HardwareProfileFactory.Create(HardwareModel.Cgb, header);

        var cgbProfile = profile.Should().BeOfType<CgbHardwareProfile>().Subject;
        cgbProfile.Model.Should().Be(HardwareModel.Cgb);
        cgbProfile.OperatingMode.Should().Be(CgbOperatingMode.DmgCompatibility);
        cgbProfile.VideoRamBankCount.Should().Be(1);
        cgbProfile.IsVideoRamBankRegisterEnabled.Should().BeTrue();
        cgbProfile.IsKey1RegisterEnabled.Should().BeFalse();
        profile.IsSerialHighSpeedClockEnabled.Should().BeFalse();
        cgbProfile.IsColorPaletteRamEnabled.Should().BeFalse();
        cgbProfile.IsColorPaletteIndexRegisterEnabled.Should().BeTrue();
        cgbProfile.IsCgbHardwareMiscRegisterEnabled.Should().BeTrue();
        cgbProfile.IsCgbUndocumentedFf74RegisterEnabled.Should().BeFalse();
        cgbProfile.TicksTimerOnTacDisableWhenInputHigh.Should().BeFalse();
        cgbProfile.TicksTimerOnTacEnableWhenInputHigh.Should().BeTrue();
        cgbProfile.CreatePpuEngine().Should().BeOfType<CgbDmgCompatibilityPpuEngine>();
        cgbProfile.CreateApuModelSpec().Should().Be(ApuModelSpec.Cgb);
        cgbProfile.WorkRamBankCount.Should().Be(8);
    }

    [Fact]
    public void Create_ReturnsSgbProfileForSgbHardwareWhenCartridgeAllowsDmg()
    {
        var header = CreateHeader(CgbSupport.None);

        var profile = HardwareProfileFactory.Create(HardwareModel.Sgb, header);

        profile.Should().BeSameAs(SgbHardwareProfile.Instance);
        profile.Model.Should().Be(HardwareModel.Sgb);
        profile.VideoRamBankCount.Should().Be(1);
        profile.IsVideoRamBankRegisterEnabled.Should().BeFalse();
        profile.IsKey1RegisterEnabled.Should().BeFalse();
        profile.IsSerialHighSpeedClockEnabled.Should().BeFalse();
        profile.IsColorPaletteRamEnabled.Should().BeFalse();
        profile.IsColorPaletteIndexRegisterEnabled.Should().BeFalse();
        profile.TicksTimerOnTacDisableWhenInputHigh.Should().BeTrue();
        profile.TicksTimerOnTacEnableWhenInputHigh.Should().BeFalse();
        profile.CreatePpuEngine().Should().BeOfType<DmgPpuEngine>();
        profile.CreateApuModelSpec().Should().Be(ApuModelSpec.Sgb);
        profile.WorkRamBankCount.Should().Be(2);
    }

    [Fact]
    public void Create_RejectsCgbRequiredCartridgeForSgbHardware()
    {
        var header = CreateHeader(CgbSupport.Required);

        var exception = FluentActions
            .Invoking(() => HardwareProfileFactory.Create(HardwareModel.Sgb, header))
            .Should()
            .ThrowExactly<NotSupportedException>()
            .Which;

        exception.Message.Should().Contain("CGB-required");
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

        var cartridge = TestRomFactory.LoadCartridge(rom => rom[0x0143] = cgbFlag);

        cartridge.Header.CgbSupport.Should().Be(cgbSupport);
        return cartridge.Header;
    }
}
