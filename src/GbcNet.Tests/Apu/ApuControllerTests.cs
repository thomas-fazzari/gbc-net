using GbcNet.Core.Apu;

namespace GbcNet.Tests.Apu;

public sealed class ApuControllerTests
{
    [Theory]
    [InlineData(0xFF10, 0x80, 0x00, 0x80)]
    [InlineData(0xFF10, 0x80, 0x80, 0x80)]
    [InlineData(0xFF1A, 0x7F, 0x00, 0x7F)]
    [InlineData(0xFF1A, 0x7F, 0x7F, 0x7F)]
    [InlineData(0xFF1C, 0x9F, 0x00, 0x9F)]
    [InlineData(0xFF1C, 0x9F, 0x9F, 0x9F)]
    [InlineData(0xFF20, 0xC0, 0x00, 0xC0)]
    [InlineData(0xFF20, 0xC0, 0xC0, 0xC0)]
    [InlineData(0xFF23, 0x3F, 0x00, 0x3F)]
    [InlineData(0xFF23, 0x3F, 0x3F, 0x3F)]
    [InlineData(0xFF26, 0x70, 0x80, 0x70)]
    [InlineData(0xFF26, 0x70, 0xF0, 0x70)]
    public void ReadRegister_ForcesUnusedBitsHigh(
        ushort address,
        byte mask,
        byte writeValue,
        byte expected
    )
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(address, writeValue);

        Assert.Equal(expected, (byte)(apu.ReadRegister(address) & mask));
    }

    [Fact]
    public void SetRegisterState_CanSeedAudioMasterStatusBits()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.SetRegisterState(0xFF26, 0x81);

        Assert.Equal(0xF1, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void WriteRegister_CannotSetAudioMasterStatusBits()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x81);

        Assert.Equal(0xF0, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void WriteRegister_IgnoresNonMasterRegistersWhenPoweredOff()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF24, 0x77);
        apu.WriteRegister(0xFF25, 0xFF);

        Assert.Equal(0x00, apu.ReadRegister(0xFF24));
        Assert.Equal(0x00, apu.ReadRegister(0xFF25));
    }

    [Fact]
    public void WriteRegister_AcceptsNonMasterRegistersWhenPoweredOn()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF24, 0x77);
        apu.WriteRegister(0xFF25, 0xFF);

        Assert.Equal(0x77, apu.ReadRegister(0xFF24));
        Assert.Equal(0xFF, apu.ReadRegister(0xFF25));
    }

    [Fact]
    public void WriteRegister_PoweringOffClearsPoweredRegisters()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF24, 0x77);
        apu.WriteRegister(0xFF25, 0xFF);

        apu.WriteRegister(0xFF26, 0x00);
        apu.WriteRegister(0xFF26, 0x80);

        Assert.Equal(0x00, apu.ReadRegister(0xFF24));
        Assert.Equal(0x00, apu.ReadRegister(0xFF25));
        Assert.Equal(0xF0, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void WriteRegister_PoweringOffClearsChannelStatusBits()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.SetRegisterState(0xFF26, 0x8F);

        apu.WriteRegister(0xFF26, 0x00);

        Assert.Equal(0x70, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void WriteRegister_TriggeringChannel1WithDacEnabledSetsAudioMasterChannel1Status()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF12, 0xF0);
        apu.WriteRegister(0xFF14, 0x80);

        Assert.Equal(0xF1, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void WriteRegister_Channel1SweepImmediateOverflowClearsChannel1Status()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF10, 0x01);
        apu.WriteRegister(0xFF12, 0xF0);
        apu.WriteRegister(0xFF13, 0x00);
        apu.WriteRegister(0xFF14, 0x87);

        Assert.Equal(0xF0, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void TickSystemCounter_Channel1SweepWritesValidNewPeriod()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF10, 0x19);
        apu.WriteRegister(0xFF12, 0xF0);
        apu.WriteRegister(0xFF13, 0x00);
        apu.WriteRegister(0xFF14, 0x84);

        ApuFrameSequencerEvents events;
        do
        {
            events = apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: false));
        } while (!events.SweepClock);

        Assert.Equal(0x0200, apu.Channel1Period);
        Assert.Equal(0xF1, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void TickSystemCounter_Channel1SweepOverflowClearsChannel1Status()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF10, 0x11);
        apu.WriteRegister(0xFF12, 0xF0);
        apu.WriteRegister(0xFF13, 0x00);
        apu.WriteRegister(0xFF14, 0x84);

        ApuFrameSequencerEvents events;
        do
        {
            events = apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: false));
        } while (!events.SweepClock);

        Assert.Equal(0x0600, apu.Channel1Period);
        Assert.Equal(0xF0, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void TickSystemCounter_Channel1SweepWithShiftZeroDoesNotWriteBackPeriod()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF10, 0x10);
        apu.WriteRegister(0xFF12, 0xF0);
        apu.WriteRegister(0xFF13, 0x00);
        apu.WriteRegister(0xFF14, 0x84);

        ApuFrameSequencerEvents events;
        do
        {
            events = apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: false));
        } while (!events.SweepClock);

        Assert.Equal(0x0400, apu.Channel1Period);
        Assert.Equal(0xF1, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void WriteRegister_TriggeringChannel2WithDacEnabledSetsAudioMasterChannel2Status()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF17, 0xF0);
        apu.WriteRegister(0xFF19, 0x80);

        Assert.Equal(0xF2, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void WriteRegister_TriggeringChannel2WithDacDisabledKeepsChannel2Inactive()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF17, 0x00);
        apu.WriteRegister(0xFF19, 0x80);

        Assert.Equal(0xF0, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void WriteRegister_DisablingChannel2DacClearsChannel2Status()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF17, 0xF0);
        apu.WriteRegister(0xFF19, 0x80);

        apu.WriteRegister(0xFF17, 0x00);

        Assert.Equal(0xF0, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void WriteRegister_PoweringOffDisablesChannel2()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF17, 0xF0);
        apu.WriteRegister(0xFF19, 0x80);

        apu.WriteRegister(0xFF26, 0x00);
        apu.WriteRegister(0xFF26, 0x80);

        Assert.Equal(0xF0, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void TickSystemCounter_DisablesChannel2WhenLengthExpires()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF16, 0x3F);
        apu.WriteRegister(0xFF17, 0xF0);
        apu.WriteRegister(0xFF19, 0xC0);

        apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: false));

        Assert.Equal(0xF0, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void TickSystemCounter_KeepsChannel2ActiveWhenLengthDisabled()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF16, 0x3F);
        apu.WriteRegister(0xFF17, 0xF0);
        apu.WriteRegister(0xFF19, 0x80);

        apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: false));

        Assert.Equal(0xF2, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void WriteRegister_TriggeringChannel2ReloadsExpiredLengthCounter()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF16, 0x3F);
        apu.WriteRegister(0xFF17, 0xF0);
        apu.WriteRegister(0xFF19, 0xC0);
        apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: false));

        apu.WriteRegister(0xFF19, 0xC0);
        for (var lengthEvents = 0; lengthEvents < 63; )
        {
            if (
                apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: false)).LengthClock
            )
            {
                lengthEvents++;
            }
        }

        Assert.Equal(0xF2, apu.ReadRegister(0xFF26));

        ApuFrameSequencerEvents events;
        do
        {
            events = apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: false));
        } while (!events.LengthClock);

        Assert.Equal(0xF0, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void WriteRegister_TriggeringChannel2LoadsEnvelopeInitialVolume()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF17, 0xA2);
        apu.WriteRegister(0xFF19, 0x80);

        Assert.Equal(10, apu.Channel2Volume);
    }

    [Fact]
    public void TickSystemCounter_IncreasesChannel2VolumeAtEnvelopePace()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF17, 0x1A);
        apu.WriteRegister(0xFF19, 0x80);

        for (var envelopeEvents = 0; envelopeEvents < 2; )
        {
            if (
                apu.TickSystemCounter(
                    new ApuTickInputs(1 << 12, CgbDoubleSpeed: false)
                ).EnvelopeClock
            )
            {
                envelopeEvents++;
            }
        }

        Assert.Equal(2, apu.Channel2Volume);
    }

    [Fact]
    public void TickSystemCounter_DecreasesChannel2VolumeAtEnvelopePace()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF17, 0x21);
        apu.WriteRegister(0xFF19, 0x80);

        ApuFrameSequencerEvents events;
        do
        {
            events = apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: false));
        } while (!events.EnvelopeClock);

        Assert.Equal(1, apu.Channel2Volume);
    }

    [Fact]
    public void TickSystemCounter_DoesNotChangeChannel2VolumeWhenEnvelopePaceIsZero()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF17, 0x58);
        apu.WriteRegister(0xFF19, 0x80);

        for (var envelopeEvents = 0; envelopeEvents < 2; )
        {
            if (
                apu.TickSystemCounter(
                    new ApuTickInputs(1 << 12, CgbDoubleSpeed: false)
                ).EnvelopeClock
            )
            {
                envelopeEvents++;
            }
        }

        Assert.Equal(5, apu.Channel2Volume);
    }

    [Theory]
    [InlineData(0xF9, 15)]
    [InlineData(0x01, 0)]
    public void TickSystemCounter_DoesNotChangeChannel2VolumePastEnvelopeBounds(
        byte envelope,
        byte expectedVolume
    )
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF17, envelope);
        apu.WriteRegister(0xFF19, 0x80);

        ApuFrameSequencerEvents events;
        do
        {
            events = apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: false));
        } while (!events.EnvelopeClock);

        Assert.Equal(expectedVolume, apu.Channel2Volume);
    }

    [Fact]
    public void Channel2DigitalOutput_ReturnsZeroWhenChannel2IsInactive()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF17, 0xA0);

        Assert.Equal(0, apu.Channel2DigitalOutput);
    }

    [Fact]
    public void Channel2DigitalOutput_ReturnsZeroWhenPulseChannelFirstStartsEvenIfDutyStepIsHigh()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF16, 0x40);
        apu.WriteRegister(0xFF17, 0xA0);
        apu.WriteRegister(0xFF18, 0xFF);
        apu.WriteRegister(0xFF19, 0x87);

        Assert.Equal(0, apu.Channel2DigitalOutput);
    }

    [Fact]
    public void Channel2DigitalOutput_UsesDutyPatternAndVolume()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF16, 0xC0);
        apu.WriteRegister(0xFF17, 0xA0);
        apu.WriteRegister(0xFF18, 0xFF);
        apu.WriteRegister(0xFF19, 0x87);

        Assert.Equal(0, apu.Channel2DigitalOutput);

        apu.Tick(4);

        Assert.Equal(10, apu.Channel2DigitalOutput);
    }

    [Fact]
    public void Tick_AdvancesChannel2DutyStepFasterForHigherPeriodValues()
    {
        ApuController fastApu = new(ApuModelSpec.Dmg);
        fastApu.WriteRegister(0xFF26, 0x80);
        fastApu.WriteRegister(0xFF16, 0xC0);
        fastApu.WriteRegister(0xFF17, 0xF0);
        fastApu.WriteRegister(0xFF18, 0xFF);
        fastApu.WriteRegister(0xFF19, 0x87);

        ApuController slowApu = new(ApuModelSpec.Dmg);
        slowApu.WriteRegister(0xFF26, 0x80);
        slowApu.WriteRegister(0xFF16, 0xC0);
        slowApu.WriteRegister(0xFF17, 0xF0);
        slowApu.WriteRegister(0xFF18, 0xFE);
        slowApu.WriteRegister(0xFF19, 0x87);

        fastApu.Tick(4);
        slowApu.Tick(4);

        Assert.Equal(15, fastApu.Channel2DigitalOutput);
        Assert.Equal(0, slowApu.Channel2DigitalOutput);

        slowApu.Tick(4);

        Assert.Equal(15, slowApu.Channel2DigitalOutput);
    }

    [Fact]
    public void WriteRegister_TriggeringChannel2DoesNotResetDutyStep()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF16, 0xC0);
        apu.WriteRegister(0xFF17, 0xA0);
        apu.WriteRegister(0xFF18, 0xFF);
        apu.WriteRegister(0xFF19, 0x87);
        apu.Tick(4);

        apu.WriteRegister(0xFF19, 0x87);

        Assert.Equal(10, apu.Channel2DigitalOutput);
    }

    [Fact]
    public void GetMixedStereoSample_MixesChannel1UsingNr50AndNr51()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF11, 0xC0);
        apu.WriteRegister(0xFF12, 0x40);
        apu.WriteRegister(0xFF13, 0xFF);
        apu.WriteRegister(0xFF14, 0x87);
        apu.Tick(4);
        apu.WriteRegister(0xFF24, 0x00);
        apu.WriteRegister(0xFF25, 0x11);

        Assert.Equal(new ApuMixedStereoSample(4, 4), apu.GetMixedStereoSample());
    }

    [Fact]
    public void GetMixedStereoSample_MixesChannel1AndChannel2Independently()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF11, 0xC0);
        apu.WriteRegister(0xFF12, 0x40);
        apu.WriteRegister(0xFF13, 0xFF);
        apu.WriteRegister(0xFF14, 0x87);
        apu.WriteRegister(0xFF16, 0xC0);
        apu.WriteRegister(0xFF17, 0x60);
        apu.WriteRegister(0xFF18, 0xFF);
        apu.WriteRegister(0xFF19, 0x87);
        apu.Tick(4);
        apu.WriteRegister(0xFF24, 0x00);
        apu.WriteRegister(0xFF25, 0x03);

        Assert.Equal(new ApuMixedStereoSample(0, 10), apu.GetMixedStereoSample());
    }

    [Fact]
    public void GetMixedStereoSample_ReturnsSilenceWhenChannel2IsInactive()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF24, 0x77);
        apu.WriteRegister(0xFF25, 0x22);

        Assert.Equal(new ApuMixedStereoSample(0, 0), apu.GetMixedStereoSample());
    }

    [Fact]
    public void GetMixedStereoSample_ReturnsSilenceWhenChannel2IsNotRouted()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF16, 0xC0);
        apu.WriteRegister(0xFF17, 0xA0);
        apu.WriteRegister(0xFF18, 0xFF);
        apu.WriteRegister(0xFF19, 0x87);
        apu.Tick(4);
        apu.WriteRegister(0xFF24, 0x77);
        apu.WriteRegister(0xFF25, 0x00);

        Assert.Equal(new ApuMixedStereoSample(0, 0), apu.GetMixedStereoSample());
    }

    [Theory]
    [InlineData(0x00, 0x22, 10, 10)]
    [InlineData(0x77, 0x22, 80, 80)]
    [InlineData(0x70, 0x22, 80, 10)]
    [InlineData(0x06, 0x02, 0, 70)]
    [InlineData(0x60, 0x20, 70, 0)]
    public void GetMixedStereoSample_MixesChannel2UsingNr50AndNr51(
        byte masterVolume,
        byte panning,
        int expectedLeft,
        int expectedRight
    )
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF16, 0xC0);
        apu.WriteRegister(0xFF17, 0xA0);
        apu.WriteRegister(0xFF18, 0xFF);
        apu.WriteRegister(0xFF19, 0x87);
        apu.Tick(4);
        apu.WriteRegister(0xFF24, masterVolume);
        apu.WriteRegister(0xFF25, panning);

        Assert.Equal(
            new ApuMixedStereoSample(expectedLeft, expectedRight),
            apu.GetMixedStereoSample()
        );
    }

    [Fact]
    public void WriteRegister_PoweringOffResetsChannel2DutyStep()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF16, 0xC0);
        apu.WriteRegister(0xFF17, 0xA0);
        apu.WriteRegister(0xFF18, 0xFF);
        apu.WriteRegister(0xFF19, 0x87);
        apu.Tick(4);

        apu.WriteRegister(0xFF26, 0x00);
        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF16, 0xC0);
        apu.WriteRegister(0xFF17, 0xA0);
        apu.WriteRegister(0xFF18, 0xFF);
        apu.WriteRegister(0xFF19, 0x87);

        Assert.Equal(0, apu.Channel2DigitalOutput);
    }

    [Fact]
    public void WaveRam_InactiveReadWriteIsNormal()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF30, 0xAB);
        apu.WriteRegister(0xFF3F, 0xCD);

        Assert.Equal(0xAB, apu.ReadRegister(0xFF30));
        Assert.Equal(0xCD, apu.ReadRegister(0xFF3F));
    }

    [Fact]
    public void WaveRam_ActiveCpuReadReturnsFfAndWriteIsIgnored()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF30, 0xAB);
        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF1A, 0x80);
        apu.WriteRegister(0xFF1E, 0x80);

        apu.WriteRegister(0xFF30, 0xCD);

        Assert.Equal(0xFF, apu.ReadRegister(0xFF30));
        apu.WriteRegister(0xFF1A, 0x00);
        Assert.Equal(0xAB, apu.ReadRegister(0xFF30));
    }

    [Fact]
    public void SetRegisterState_CanSeedWaveRamWhileChannel3IsActive()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF30, 0xAB);
        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF1A, 0x80);
        apu.WriteRegister(0xFF1E, 0x80);

        apu.SetRegisterState(0xFF30, 0xCD);
        apu.WriteRegister(0xFF1A, 0x00);

        Assert.Equal(0xCD, apu.ReadRegister(0xFF30));
    }

    [Fact]
    public void WriteRegister_DisablingChannel3DacClearsChannel3Status()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF1A, 0x80);
        apu.WriteRegister(0xFF1E, 0x80);

        apu.WriteRegister(0xFF1A, 0x00);

        Assert.Equal(0xF0, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void WriteRegister_TriggeringChannel3WithDacEnabledSetsAudioMasterChannel3Status()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF1A, 0x80);
        apu.WriteRegister(0xFF1E, 0x80);

        Assert.Equal(0xF4, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void WriteRegister_TriggeringChannel3WithDacDisabledKeepsChannel3Inactive()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF1A, 0x00);
        apu.WriteRegister(0xFF1E, 0x80);

        Assert.Equal(0xF0, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void TickSystemCounter_DisablesChannel3WhenLengthExpires()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF1A, 0x80);
        apu.WriteRegister(0xFF1B, 0xFF);
        apu.WriteRegister(0xFF1E, 0xC0);

        apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: false));

        Assert.Equal(0xF0, apu.ReadRegister(0xFF26));
    }

    [Theory]
    [InlineData(0x00, 0)]
    [InlineData(0x20, 12)]
    [InlineData(0x40, 6)]
    [InlineData(0x60, 3)]
    public void Channel3DigitalOutput_AppliesNr32OutputLevel(byte outputLevel, byte expected)
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF30, 0x0C);
        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF1A, 0x80);
        apu.WriteRegister(0xFF1C, outputLevel);
        apu.WriteRegister(0xFF1D, 0xFF);
        apu.WriteRegister(0xFF1E, 0x87);
        apu.Tick(2);

        Assert.Equal(expected, apu.Channel3DigitalOutput);
    }

    [Fact]
    public void WriteRegister_TriggeringChannel3KeepsOldSampleBufferUntilFirstWaveTick()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF30, 0x0C);
        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF1A, 0x80);
        apu.WriteRegister(0xFF1C, 0x20);
        apu.WriteRegister(0xFF1D, 0xFF);
        apu.WriteRegister(0xFF1E, 0x87);

        Assert.Equal(0, apu.Channel3DigitalOutput);
    }

    [Fact]
    public void Tick_Channel3FirstWaveTickReadsLowerNibbleOfFf30()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF30, 0xAB);
        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF1A, 0x80);
        apu.WriteRegister(0xFF1C, 0x20);
        apu.WriteRegister(0xFF1D, 0xFF);
        apu.WriteRegister(0xFF1E, 0x87);

        apu.Tick(2);

        Assert.Equal(0x0B, apu.Channel3DigitalOutput);
    }

    [Fact]
    public void Tick_AdvancesChannel3WaveFasterForHigherPeriodValues()
    {
        ApuController fastApu = new(ApuModelSpec.Dmg);
        fastApu.WriteRegister(0xFF30, 0x01);
        fastApu.WriteRegister(0xFF26, 0x80);
        fastApu.WriteRegister(0xFF1A, 0x80);
        fastApu.WriteRegister(0xFF1C, 0x20);
        fastApu.WriteRegister(0xFF1D, 0xFF);
        fastApu.WriteRegister(0xFF1E, 0x87);

        ApuController slowApu = new(ApuModelSpec.Dmg);
        slowApu.WriteRegister(0xFF30, 0x01);
        slowApu.WriteRegister(0xFF26, 0x80);
        slowApu.WriteRegister(0xFF1A, 0x80);
        slowApu.WriteRegister(0xFF1C, 0x20);
        slowApu.WriteRegister(0xFF1D, 0xFE);
        slowApu.WriteRegister(0xFF1E, 0x87);

        fastApu.Tick(2);
        slowApu.Tick(2);

        Assert.Equal(1, fastApu.Channel3DigitalOutput);
        Assert.Equal(0, slowApu.Channel3DigitalOutput);

        slowApu.Tick(2);

        Assert.Equal(1, slowApu.Channel3DigitalOutput);
    }

    [Theory]
    [InlineData(0x00, 0x44, 4, 4)]
    [InlineData(0x77, 0x44, 32, 32)]
    [InlineData(0x70, 0x44, 32, 4)]
    [InlineData(0x06, 0x04, 0, 28)]
    [InlineData(0x60, 0x40, 28, 0)]
    public void GetMixedStereoSample_MixesChannel3UsingNr50AndNr51(
        byte masterVolume,
        byte panning,
        int expectedLeft,
        int expectedRight
    )
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF30, 0x04);
        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF1A, 0x80);
        apu.WriteRegister(0xFF1C, 0x20);
        apu.WriteRegister(0xFF1D, 0xFF);
        apu.WriteRegister(0xFF1E, 0x87);
        apu.Tick(2);
        apu.WriteRegister(0xFF24, masterVolume);
        apu.WriteRegister(0xFF25, panning);

        Assert.Equal(
            new ApuMixedStereoSample(expectedLeft, expectedRight),
            apu.GetMixedStereoSample()
        );
    }

    [Fact]
    public void WriteRegister_PoweringOffClearsChannel3StateButNotWaveRam()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF30, 0x0C);
        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF1A, 0x80);
        apu.WriteRegister(0xFF1C, 0x20);
        apu.WriteRegister(0xFF1D, 0xFF);
        apu.WriteRegister(0xFF1E, 0x87);
        apu.Tick(2);

        apu.WriteRegister(0xFF26, 0x00);
        apu.WriteRegister(0xFF26, 0x80);

        Assert.Equal(0x0C, apu.ReadRegister(0xFF30));

        apu.WriteRegister(0xFF1A, 0x80);
        apu.WriteRegister(0xFF1C, 0x20);
        apu.WriteRegister(0xFF1D, 0xFF);
        apu.WriteRegister(0xFF1E, 0x87);

        Assert.Equal(0, apu.Channel3DigitalOutput);
    }

    [Fact]
    public void WriteRegister_TriggeringChannel4WithDacEnabledSetsAudioMasterChannel4Status()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF21, 0xF0);
        apu.WriteRegister(0xFF23, 0x80);

        Assert.Equal(0xF8, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void WriteRegister_TriggeringChannel4WithDacDisabledKeepsChannel4Inactive()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF21, 0x00);
        apu.WriteRegister(0xFF23, 0x80);

        Assert.Equal(0xF0, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void WriteRegister_DisablingChannel4DacClearsChannel4Status()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF21, 0xF0);
        apu.WriteRegister(0xFF23, 0x80);

        apu.WriteRegister(0xFF21, 0x00);

        Assert.Equal(0xF0, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void TickSystemCounter_DisablesChannel4WhenLengthExpires()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF20, 0x3F);
        apu.WriteRegister(0xFF21, 0xF0);
        apu.WriteRegister(0xFF23, 0xC0);

        apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: false));

        Assert.Equal(0xF0, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void TickSystemCounter_IncreasesChannel4VolumeAtEnvelopePace()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF21, 0x1A);
        apu.WriteRegister(0xFF23, 0x80);

        for (var envelopeEvents = 0; envelopeEvents < 2; )
        {
            if (
                apu.TickSystemCounter(
                    new ApuTickInputs(1 << 12, CgbDoubleSpeed: false)
                ).EnvelopeClock
            )
            {
                envelopeEvents++;
            }
        }

        Assert.Equal(2, apu.Channel4Volume);
    }

    [Fact]
    public void TickSystemCounter_DecreasesChannel4VolumeAtEnvelopePace()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF21, 0x21);
        apu.WriteRegister(0xFF23, 0x80);

        ApuFrameSequencerEvents events;
        do
        {
            events = apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: false));
        } while (!events.EnvelopeClock);

        Assert.Equal(1, apu.Channel4Volume);
    }

    [Fact]
    public void TickSystemCounter_DoesNotChangeChannel4VolumeWhenEnvelopePaceIsZero()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF21, 0x58);
        apu.WriteRegister(0xFF23, 0x80);

        for (var envelopeEvents = 0; envelopeEvents < 2; )
        {
            if (
                apu.TickSystemCounter(
                    new ApuTickInputs(1 << 12, CgbDoubleSpeed: false)
                ).EnvelopeClock
            )
            {
                envelopeEvents++;
            }
        }

        Assert.Equal(5, apu.Channel4Volume);
    }

    [Theory]
    [InlineData(0xF9, 15)]
    [InlineData(0x11, 0)]
    public void TickSystemCounter_DoesNotChangeChannel4VolumePastEnvelopeBounds(
        byte envelope,
        byte expectedVolume
    )
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF21, envelope);
        apu.WriteRegister(0xFF23, 0x80);

        for (var envelopeEvents = 0; envelopeEvents < 2; )
        {
            if (
                apu.TickSystemCounter(
                    new ApuTickInputs(1 << 12, CgbDoubleSpeed: false)
                ).EnvelopeClock
            )
            {
                envelopeEvents++;
            }
        }

        Assert.Equal(expectedVolume, apu.Channel4Volume);
    }

    [Fact]
    public void Tick_Channel4LfsrAdvancesAfterExpectedTimer()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF21, 0xF0);
        apu.WriteRegister(0xFF22, 0x01);
        apu.WriteRegister(0xFF23, 0x80);

        apu.Tick(255);

        Assert.Equal(0, apu.Channel4DigitalOutput);

        apu.Tick(1);

        Assert.Equal(15, apu.Channel4DigitalOutput);
    }

    [Fact]
    public void Tick_Channel4WidthModeUsesSevenBitFeedbackPath()
    {
        ApuController wideApu = new(ApuModelSpec.Dmg);
        wideApu.WriteRegister(0xFF26, 0x80);
        wideApu.WriteRegister(0xFF21, 0xF0);
        wideApu.WriteRegister(0xFF22, 0x08);
        wideApu.WriteRegister(0xFF23, 0x80);

        ApuController normalApu = new(ApuModelSpec.Dmg);
        normalApu.WriteRegister(0xFF26, 0x80);
        normalApu.WriteRegister(0xFF21, 0xF0);
        normalApu.WriteRegister(0xFF22, 0x00);
        normalApu.WriteRegister(0xFF23, 0x80);

        wideApu.Tick(64);
        normalApu.Tick(64);

        Assert.Equal(15, wideApu.Channel4DigitalOutput);
        Assert.Equal(0, normalApu.Channel4DigitalOutput);
    }

    [Theory]
    [InlineData(0xE8)]
    [InlineData(0xF8)]
    public void Tick_Channel4ShiftFourteenOrFifteenDoesNotClockLfsr(byte frequency)
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF21, 0xF0);
        apu.WriteRegister(0xFF22, frequency);
        apu.WriteRegister(0xFF23, 0x80);

        apu.Tick(4096);

        Assert.Equal(0, apu.Channel4DigitalOutput);
    }

    [Theory]
    [InlineData(0x00, 0x88, 15, 15)]
    [InlineData(0x77, 0x88, 120, 120)]
    [InlineData(0x70, 0x88, 120, 15)]
    [InlineData(0x06, 0x08, 0, 105)]
    [InlineData(0x60, 0x80, 105, 0)]
    public void GetMixedStereoSample_MixesChannel4UsingNr50AndNr51(
        byte masterVolume,
        byte panning,
        int expectedLeft,
        int expectedRight
    )
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF21, 0xF0);
        apu.WriteRegister(0xFF23, 0x80);
        apu.Tick(128);
        apu.WriteRegister(0xFF24, masterVolume);
        apu.WriteRegister(0xFF25, panning);

        Assert.Equal(
            new ApuMixedStereoSample(expectedLeft, expectedRight),
            apu.GetMixedStereoSample()
        );
    }

    [Fact]
    public void Tick_BuffersApuSamplesThroughSampleBuffer()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        Span<ApuStereoSample> destination = stackalloc ApuStereoSample[1];

        apu.Tick(87);

        Assert.Equal(0, apu.DrainBufferedSamples(destination));

        apu.Tick(1);

        Assert.Equal(1, apu.DrainBufferedSamples(destination));
    }

    [Fact]
    public void DrainBufferedSamples_ReturnsSilenceWhenDacEnabledChannelIsNotRouted()
    {
        ApuController apu = new(ApuModelSpec.Dmg);
        var destination = new ApuStereoSample[1];

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF12, 0xF0);
        apu.WriteRegister(0xFF14, 0x80);
        apu.WriteRegister(0xFF25, 0x00);

        apu.Tick(88);

        Assert.Equal(1, apu.DrainBufferedSamples(destination));
        Assert.Equal([default], destination);
    }

    [Fact]
    public void DrainBufferedSamples_IgnoresRoutedChannelWithDacDisabled()
    {
        ApuController apu = new(ApuModelSpec.Dmg);
        var destination = new ApuStereoSample[1];

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF12, 0x00);
        apu.WriteRegister(0xFF14, 0x80);
        apu.WriteRegister(0xFF17, 0xF0);
        apu.WriteRegister(0xFF19, 0x80);
        apu.WriteRegister(0xFF25, 0x11);

        apu.Tick(88);

        Assert.Equal(1, apu.DrainBufferedSamples(destination));
        Assert.Equal([default], destination);
    }

    [Fact]
    public void DrainBufferedSamples_ReturnsCurrentMixerSamplesAndClearsBuffer()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF11, 0xC0);
        apu.WriteRegister(0xFF12, 0x40);
        apu.WriteRegister(0xFF13, 0xFF);
        apu.WriteRegister(0xFF14, 0x87);
        apu.WriteRegister(0xFF24, 0x00);
        apu.WriteRegister(0xFF25, 0x11);

        var destination = new ApuStereoSample[1];

        apu.Tick(88);

        Assert.Equal(1, apu.DrainBufferedSamples(destination));
        Assert.Equal([new ApuStereoSample(478, 478)], destination);
        Assert.Equal(0, apu.DrainBufferedSamples(destination));
    }

    [Fact]
    public void TickSystemCounter_AdvancesDivApuStepOnNormalSpeedDivBit4FallingEdge()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        var events = apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: false));

        Assert.Equal(1, apu.DivApuStep);
        Assert.True(events.LengthClock);
        Assert.False(events.SweepClock);
        Assert.False(events.EnvelopeClock);
    }

    [Fact]
    public void TickSystemCounter_AdvancesDivApuStepOnDoubleSpeedDivBit5FallingEdge()
    {
        ApuController apu = new(ApuModelSpec.Cgb);

        var events = apu.TickSystemCounter(new ApuTickInputs(1 << 13, CgbDoubleSpeed: true));

        Assert.Equal(1, apu.DivApuStep);
        Assert.True(events.LengthClock);
        Assert.False(events.SweepClock);
        Assert.False(events.EnvelopeClock);
    }

    [Fact]
    public void TickSystemCounter_IgnoresNormalSpeedDivBit4FallingEdgeInDoubleSpeed()
    {
        ApuController apu = new(ApuModelSpec.Cgb);

        var events = apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: true));

        Assert.Equal(0, apu.DivApuStep);
        Assert.Equal(default, events);
    }

    [Fact]
    public void TickSystemCounter_IgnoresOtherSystemCounterFallingEdges()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        var events = apu.TickSystemCounter(new ApuTickInputs(1 << 11, CgbDoubleSpeed: false));

        Assert.Equal(0, apu.DivApuStep);
        Assert.Equal(default, events);
    }

    [Theory]
    [InlineData(1, 1, true, false, false)]
    [InlineData(2, 2, false, false, false)]
    [InlineData(3, 3, true, true, false)]
    [InlineData(4, 4, false, false, false)]
    [InlineData(5, 5, true, false, false)]
    [InlineData(6, 6, false, false, false)]
    [InlineData(7, 7, true, true, true)]
    [InlineData(8, 0, false, false, false)]
    public void TickSystemCounter_ReturnsFrameSequencerEventsForNewDivApuStep(
        int ticks,
        byte expectedStep,
        bool expectedLength,
        bool expectedSweep,
        bool expectedEnvelope
    )
    {
        ApuController apu = new(ApuModelSpec.Dmg);
        ApuFrameSequencerEvents events = default;

        for (var tick = 0; tick < ticks; tick++)
        {
            events = apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: false));
        }

        Assert.Equal(expectedStep, apu.DivApuStep);
        Assert.Equal(expectedLength, events.LengthClock);
        Assert.Equal(expectedSweep, events.SweepClock);
        Assert.Equal(expectedEnvelope, events.EnvelopeClock);
    }

    [Theory]
    [InlineData(0xFF10)]
    [InlineData(0xFF14)]
    [InlineData(0xFF1E)]
    [InlineData(0xFF26)]
    [InlineData(0xFF30)]
    [InlineData(0xFF3F)]
    public void ContainsRegister_ReturnsTrueForApuRegisters(ushort address)
    {
        Assert.True(ApuController.ContainsRegister(address));
    }

    [Theory]
    [InlineData(0xFF15)]
    [InlineData(0xFF1F)]
    public void ContainsRegister_ReturnsFalseForUnusedApuAddresses(ushort address)
    {
        Assert.False(ApuController.ContainsRegister(address));
    }
}
