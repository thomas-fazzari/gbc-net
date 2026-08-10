// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Apu;
using GbcNet.Core.Hardware;

namespace GbcNet.Tests.Unit.Apu;

public sealed class ApuControllerTests
{
    public static TheoryData<HardwareModel> SweepModels =>
        [HardwareModel.Dmg, HardwareModel.Cgb, HardwareModel.Sgb];

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

        ((byte)(apu.ReadRegister(address) & mask)).Should().Be(expected);
    }

    [Fact]
    public void SetRegisterState_CanSeedAudioMasterStatusBits()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.SetRegisterState(0xFF26, 0x81);

        apu.ReadRegister(0xFF26).Should().Be(0xF1);
    }

    [Fact]
    public void WriteRegister_CannotSetAudioMasterStatusBits()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x81);

        apu.ReadRegister(0xFF26).Should().Be(0xF0);
    }

    [Fact]
    public void WriteRegister_IgnoresNonMasterRegistersWhenPoweredOff()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF24, 0x77);
        apu.WriteRegister(0xFF25, 0xFF);

        apu.ReadRegister(0xFF24).Should().Be(0x00);
        apu.ReadRegister(0xFF25).Should().Be(0x00);
    }

    [Fact]
    public void WriteRegister_AcceptsNonMasterRegistersWhenPoweredOn()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF24, 0x77);
        apu.WriteRegister(0xFF25, 0xFF);

        apu.ReadRegister(0xFF24).Should().Be(0x77);
        apu.ReadRegister(0xFF25).Should().Be(0xFF);
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

        apu.ReadRegister(0xFF24).Should().Be(0x00);
        apu.ReadRegister(0xFF25).Should().Be(0x00);
        apu.ReadRegister(0xFF26).Should().Be(0xF0);
    }

    [Fact]
    public void WriteRegister_PoweringOffClearsChannelStatusBits()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.SetRegisterState(0xFF26, 0x8F);

        apu.WriteRegister(0xFF26, 0x00);

        apu.ReadRegister(0xFF26).Should().Be(0x70);
    }

    [Fact]
    public void WriteRegister_TriggeringChannel1WithDacEnabledSetsAudioMasterChannel1Status()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF12, 0xF0);
        apu.WriteRegister(0xFF14, 0x80);

        apu.ReadRegister(0xFF26).Should().Be(0xF1);
    }

    [Theory]
    [MemberData(nameof(SweepModels))]
    public void WriteRegister_Channel1SweepImmediateOverflowClearsChannel1Status(
        HardwareModel model
    )
    {
        // Pan Docs `audio-details.md`: triggering runs an immediate overflow check.
        var apu = TriggerChannel1(model, sweep: 0x01, period: 0x0700);

        apu.ReadRegister(0xFF26).Should().Be(0xF0);
    }

    [Fact]
    public void TickSystemCounter_Channel1SweepWritesValidNewPeriod()
    {
        var apu = TriggerChannel1(HardwareModel.Dmg, sweep: 0x19, period: 0x0400);

        ClockSweep(apu);

        apu.Channel1Period.Should().Be(0x0200);
        apu.ReadRegister(0xFF26).Should().Be(0xF1);
    }

    [Fact]
    public void TickSystemCounter_Channel1SweepOverflowClearsChannel1Status()
    {
        var apu = TriggerChannel1(HardwareModel.Dmg, sweep: 0x11, period: 0x0400);

        ClockSweep(apu);

        apu.Channel1Period.Should().Be(0x0600);
        apu.ReadRegister(0xFF26).Should().Be(0xF0);
    }

    [Fact]
    public void TickSystemCounter_Channel1SweepWithShiftZeroDoesNotWriteBackPeriod()
    {
        var apu = TriggerChannel1(HardwareModel.Dmg, sweep: 0x10, period: 0x0400);

        ClockSweep(apu);

        apu.Channel1Period.Should().Be(0x0400);
        apu.ReadRegister(0xFF26).Should().Be(0xF1);
    }

    [Theory]
    [MemberData(nameof(SweepModels))]
    public void WriteRegister_Channel1SweepPaceZeroReloadsWhenPaceBecomesActive(HardwareModel model)
    {
        // Pan Docs `audio-details.md`: a sweep pace of zero reloads the timer as eight.
        // Pan Docs `audio-registers.md`: changing zero to a non-zero pace reloads immediately.
        var apu = TriggerChannel1(model, sweep: 0x01, period: 0x0200);
        apu.CaptureState().Channel1Sweep.Timer.Should().Be(8);

        ClockSweep(apu);
        apu.CaptureState().Channel1Sweep.Timer.Should().Be(7);
        apu.WriteRegister(0xFF10, 0x31);
        apu.CaptureState().Channel1Sweep.Timer.Should().Be(3);

        ClockSweep(apu);
        ClockSweep(apu);
        apu.Channel1Period.Should().Be(0x0200);
        ClockSweep(apu);
        apu.Channel1Period.Should().Be(0x0300);
    }

    [Theory]
    [MemberData(nameof(SweepModels))]
    public void WriteRegister_ClearingSweepNegateAfterSubtractionDisablesChannel1(
        HardwareModel model
    )
    {
        // Pan Docs `audio-details.md`: clearing negate after a subtraction disables CH1.
        var apu = TriggerChannel1(model, sweep: 0x09, period: 0x0400);
        apu.ReadRegister(0xFF26).Should().Be(0xF1);

        apu.WriteRegister(0xFF10, 0x01);

        apu.ReadRegister(0xFF26).Should().Be(0xF0);
    }

    [Fact]
    public void WriteRegister_ClearingSweepNegateBeforeSubtractionKeepsChannel1Active()
    {
        var apu = TriggerChannel1(HardwareModel.Dmg, sweep: 0x08, period: 0x0400);

        apu.WriteRegister(0xFF10, 0x00);

        apu.ReadRegister(0xFF26).Should().Be(0xF1);
    }

    [Fact]
    public void WriteRegister_TriggeringChannel2WithDacEnabledSetsAudioMasterChannel2Status()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF17, 0xF0);
        apu.WriteRegister(0xFF19, 0x80);

        apu.ReadRegister(0xFF26).Should().Be(0xF2);
    }

    [Fact]
    public void WriteRegister_TriggeringChannel2WithDacDisabledKeepsChannel2Inactive()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF17, 0x00);
        apu.WriteRegister(0xFF19, 0x80);

        apu.ReadRegister(0xFF26).Should().Be(0xF0);
    }

    [Fact]
    public void WriteRegister_DisablingChannel2DacClearsChannel2Status()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF17, 0xF0);
        apu.WriteRegister(0xFF19, 0x80);

        apu.WriteRegister(0xFF17, 0x00);

        apu.ReadRegister(0xFF26).Should().Be(0xF0);
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

        apu.ReadRegister(0xFF26).Should().Be(0xF0);
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

        apu.ReadRegister(0xFF26).Should().Be(0xF0);
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

        apu.ReadRegister(0xFF26).Should().Be(0xF2);
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

        apu.ReadRegister(0xFF26).Should().Be(0xF2);

        ApuFrameSequencerEvents events;
        do
        {
            events = apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: false));
        } while (!events.LengthClock);

        apu.ReadRegister(0xFF26).Should().Be(0xF0);
    }

    [Fact]
    public void WriteRegister_TriggeringChannel2LoadsEnvelopeInitialVolume()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF17, 0xA2);
        apu.WriteRegister(0xFF19, 0x80);

        apu.Channel2Volume.Should().Be(10);
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

        apu.Channel2Volume.Should().Be(2);
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

        apu.Channel2Volume.Should().Be(1);
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

        apu.Channel2Volume.Should().Be(5);
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

        apu.Channel2Volume.Should().Be(expectedVolume);
    }

    [Fact]
    public void Channel2DigitalOutput_ReturnsZeroWhenChannel2IsInactive()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF17, 0xA0);

        apu.Channel2DigitalOutput.Should().Be(0);
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

        apu.Channel2DigitalOutput.Should().Be(0);
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

        apu.Channel2DigitalOutput.Should().Be(0);

        apu.Tick(4);

        apu.Channel2DigitalOutput.Should().Be(10);
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

        fastApu.Channel2DigitalOutput.Should().Be(15);
        slowApu.Channel2DigitalOutput.Should().Be(0);

        slowApu.Tick(4);

        slowApu.Channel2DigitalOutput.Should().Be(15);
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

        apu.Channel2DigitalOutput.Should().Be(10);
    }

    [Fact]
    public void DrainBufferedSamples_MixesChannel1UsingNr50AndNr51()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF11, 0xC0);
        apu.WriteRegister(0xFF12, 0x40);
        apu.WriteRegister(0xFF13, 0xFF);
        apu.WriteRegister(0xFF14, 0x87);
        apu.WriteRegister(0xFF24, 0x00);
        apu.WriteRegister(0xFF25, 0x11);

        DrainNextSample(apu).Should().Be(new ApuStereoSample(478, 478));
    }

    [Fact]
    public void DrainBufferedSamples_MixesChannel1AndChannel2Independently()
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
        apu.WriteRegister(0xFF24, 0x00);
        apu.WriteRegister(0xFF25, 0x03);

        DrainNextSample(apu).Should().Be(new ApuStereoSample(0, 683));
    }

    [Fact]
    public void DrainBufferedSamples_ReturnsSilenceWhenChannel2IsInactive()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF24, 0x77);
        apu.WriteRegister(0xFF25, 0x22);

        DrainNextSample(apu).Should().Be(default(ApuStereoSample));
    }

    [Fact]
    public void DrainBufferedSamples_ReturnsSilenceWhenChannel2IsNotRouted()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF16, 0xC0);
        apu.WriteRegister(0xFF17, 0xA0);
        apu.WriteRegister(0xFF18, 0xFF);
        apu.WriteRegister(0xFF19, 0x87);
        apu.WriteRegister(0xFF24, 0x77);
        apu.WriteRegister(0xFF25, 0x00);

        DrainNextSample(apu).Should().Be(default(ApuStereoSample));
    }

    [Theory]
    [InlineData(0x00, 0x22, -341, -341)]
    [InlineData(0x77, 0x22, -2731, -2731)]
    [InlineData(0x70, 0x22, -2731, -341)]
    [InlineData(0x06, 0x02, 0, -2389)]
    [InlineData(0x60, 0x20, -2389, 0)]
    public void DrainBufferedSamples_MixesChannel2UsingNr50AndNr51(
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
        apu.WriteRegister(0xFF24, masterVolume);
        apu.WriteRegister(0xFF25, panning);

        DrainNextSample(apu).Should().Be(new ApuStereoSample(expectedLeft, expectedRight));
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

        apu.Channel2DigitalOutput.Should().Be(0);
    }

    [Fact]
    public void WaveRam_InactiveReadWriteIsNormal()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF30, 0xAB);
        apu.WriteRegister(0xFF3F, 0xCD);

        apu.ReadRegister(0xFF30).Should().Be(0xAB);
        apu.ReadRegister(0xFF3F).Should().Be(0xCD);
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

        apu.ReadRegister(0xFF30).Should().Be(0xFF);
        apu.WriteRegister(0xFF1A, 0x00);
        apu.ReadRegister(0xFF30).Should().Be(0xAB);
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

        apu.ReadRegister(0xFF30).Should().Be(0xCD);
    }

    [Fact]
    public void WriteRegister_DisablingChannel3DacClearsChannel3Status()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF1A, 0x80);
        apu.WriteRegister(0xFF1E, 0x80);

        apu.WriteRegister(0xFF1A, 0x00);

        apu.ReadRegister(0xFF26).Should().Be(0xF0);
    }

    [Fact]
    public void WriteRegister_TriggeringChannel3WithDacEnabledSetsAudioMasterChannel3Status()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF1A, 0x80);
        apu.WriteRegister(0xFF1E, 0x80);

        apu.ReadRegister(0xFF26).Should().Be(0xF4);
    }

    [Fact]
    public void WriteRegister_TriggeringChannel3WithDacDisabledKeepsChannel3Inactive()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF1A, 0x00);
        apu.WriteRegister(0xFF1E, 0x80);

        apu.ReadRegister(0xFF26).Should().Be(0xF0);
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

        apu.ReadRegister(0xFF26).Should().Be(0xF0);
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

        apu.Channel3DigitalOutput.Should().Be(expected);
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

        apu.Channel3DigitalOutput.Should().Be(0);
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

        apu.Channel3DigitalOutput.Should().Be(0x0B);
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

        fastApu.Channel3DigitalOutput.Should().Be(1);
        slowApu.Channel3DigitalOutput.Should().Be(0);

        slowApu.Tick(2);

        slowApu.Channel3DigitalOutput.Should().Be(1);
    }

    [Theory]
    [InlineData(0x00, 0x44, 478, 478)]
    [InlineData(0x77, 0x44, 3823, 3823)]
    [InlineData(0x70, 0x44, 3823, 478)]
    [InlineData(0x06, 0x04, 0, 3345)]
    [InlineData(0x60, 0x40, 3345, 0)]
    public void DrainBufferedSamples_MixesChannel3UsingNr50AndNr51(
        byte masterVolume,
        byte panning,
        int expectedLeft,
        int expectedRight
    )
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        FillWaveRam(apu, 0x44);
        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF1A, 0x80);
        apu.WriteRegister(0xFF1C, 0x20);
        apu.WriteRegister(0xFF1D, 0xFF);
        apu.WriteRegister(0xFF1E, 0x87);
        apu.WriteRegister(0xFF24, masterVolume);
        apu.WriteRegister(0xFF25, panning);

        DrainNextSample(apu).Should().Be(new ApuStereoSample(expectedLeft, expectedRight));
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

        apu.ReadRegister(0xFF30).Should().Be(0x0C);

        apu.WriteRegister(0xFF1A, 0x80);
        apu.WriteRegister(0xFF1C, 0x20);
        apu.WriteRegister(0xFF1D, 0xFF);
        apu.WriteRegister(0xFF1E, 0x87);

        apu.Channel3DigitalOutput.Should().Be(0);
    }

    [Fact]
    public void WriteRegister_TriggeringChannel4WithDacEnabledSetsAudioMasterChannel4Status()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF21, 0xF0);
        apu.WriteRegister(0xFF23, 0x80);

        apu.ReadRegister(0xFF26).Should().Be(0xF8);
    }

    [Fact]
    public void WriteRegister_TriggeringChannel4WithDacDisabledKeepsChannel4Inactive()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF21, 0x00);
        apu.WriteRegister(0xFF23, 0x80);

        apu.ReadRegister(0xFF26).Should().Be(0xF0);
    }

    [Fact]
    public void WriteRegister_DisablingChannel4DacClearsChannel4Status()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF21, 0xF0);
        apu.WriteRegister(0xFF23, 0x80);

        apu.WriteRegister(0xFF21, 0x00);

        apu.ReadRegister(0xFF26).Should().Be(0xF0);
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

        apu.ReadRegister(0xFF26).Should().Be(0xF0);
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

        apu.Channel4Volume.Should().Be(2);
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

        apu.Channel4Volume.Should().Be(1);
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

        apu.Channel4Volume.Should().Be(5);
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

        apu.Channel4Volume.Should().Be(expectedVolume);
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

        apu.Channel4DigitalOutput.Should().Be(0);

        apu.Tick(1);

        apu.Channel4DigitalOutput.Should().Be(15);
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

        wideApu.Channel4DigitalOutput.Should().Be(15);
        normalApu.Channel4DigitalOutput.Should().Be(0);
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

        apu.Channel4DigitalOutput.Should().Be(0);
    }

    [Theory]
    [InlineData(0x00, 0x88, -1024, -1024)]
    [InlineData(0x77, 0x88, -8192, -8192)]
    [InlineData(0x70, 0x88, -8192, -1024)]
    [InlineData(0x06, 0x08, 0, -7168)]
    [InlineData(0x60, 0x80, -7168, 0)]
    public void DrainBufferedSamples_MixesChannel4UsingNr50AndNr51(
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
        apu.WriteRegister(0xFF24, masterVolume);
        apu.WriteRegister(0xFF25, panning);

        DrainNextSample(apu, tCycles: 128)
            .Should()
            .Be(new ApuStereoSample(expectedLeft, expectedRight));
    }

    [Fact]
    public void Tick_BuffersApuSamplesThroughSampleBuffer()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        Span<ApuStereoSample> destination = stackalloc ApuStereoSample[1];

        apu.Tick(87);

        apu.DrainBufferedSamples(destination).Should().Be(0);

        apu.Tick(1);

        apu.DrainBufferedSamples(destination).Should().Be(1);
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

        apu.DrainBufferedSamples(destination).Should().Be(1);
        destination.Should().Equal(default(ApuStereoSample));
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

        apu.DrainBufferedSamples(destination).Should().Be(1);
        destination.Should().Equal(default(ApuStereoSample));
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

        apu.DrainBufferedSamples(destination).Should().Be(1);
        destination.Should().Equal(new ApuStereoSample(478, 478));
        apu.DrainBufferedSamples(destination).Should().Be(0);
    }

    [Fact]
    public void TickSystemCounter_AdvancesDivApuStepOnNormalSpeedDivBit4FallingEdge()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        var events = apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: false));

        apu.DivApuStep.Should().Be(1);
        events.LengthClock.Should().BeTrue();
        events.SweepClock.Should().BeFalse();
        events.EnvelopeClock.Should().BeFalse();
    }

    [Fact]
    public void TickSystemCounter_AdvancesDivApuStepOnDoubleSpeedDivBit5FallingEdge()
    {
        ApuController apu = new(ApuModelSpec.Cgb);

        var events = apu.TickSystemCounter(new ApuTickInputs(1 << 13, CgbDoubleSpeed: true));

        apu.DivApuStep.Should().Be(1);
        events.LengthClock.Should().BeTrue();
        events.SweepClock.Should().BeFalse();
        events.EnvelopeClock.Should().BeFalse();
    }

    [Fact]
    public void TickSystemCounter_IgnoresNormalSpeedDivBit4FallingEdgeInDoubleSpeed()
    {
        ApuController apu = new(ApuModelSpec.Cgb);

        var events = apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: true));

        apu.DivApuStep.Should().Be(0);
        events.Should().Be(default(ApuFrameSequencerEvents));
    }

    [Fact]
    public void TickSystemCounter_IgnoresOtherSystemCounterFallingEdges()
    {
        ApuController apu = new(ApuModelSpec.Dmg);

        var events = apu.TickSystemCounter(new ApuTickInputs(1 << 11, CgbDoubleSpeed: false));

        apu.DivApuStep.Should().Be(0);
        events.Should().Be(default(ApuFrameSequencerEvents));
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

        apu.DivApuStep.Should().Be(expectedStep);
        events.LengthClock.Should().Be(expectedLength);
        events.SweepClock.Should().Be(expectedSweep);
        events.EnvelopeClock.Should().Be(expectedEnvelope);
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
        ApuController.ContainsRegister(address).Should().BeTrue();
    }

    [Theory]
    [InlineData(0xFF15)]
    [InlineData(0xFF1F)]
    public void ContainsRegister_ReturnsFalseForUnusedApuAddresses(ushort address)
    {
        ApuController.ContainsRegister(address).Should().BeFalse();
    }

    private static ApuStereoSample DrainNextSample(ApuController apu, int tCycles = 88)
    {
        var destination = new ApuStereoSample[1];

        apu.Tick(tCycles);

        apu.DrainBufferedSamples(destination).Should().Be(1);
        return destination[0];
    }

    private static void FillWaveRam(ApuController apu, byte value)
    {
        for (ushort address = 0xFF30; address <= 0xFF3F; address++)
        {
            apu.WriteRegister(address, value);
        }
    }

    private static ApuController TriggerChannel1(HardwareModel model, byte sweep, ushort period)
    {
        ApuController apu = new(GetModelSpec(model));
        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF10, sweep);
        apu.WriteRegister(0xFF12, 0xF0);
        apu.WriteRegister(0xFF13, (byte)period);
        apu.WriteRegister(0xFF14, (byte)(0x80 | (period >> 8)));
        return apu;
    }

    private static void ClockSweep(ApuController apu)
    {
        ApuFrameSequencerEvents events;
        do
        {
            events = apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: false));
        } while (!events.SweepClock);
    }

    private static ApuModelSpec GetModelSpec(HardwareModel model) =>
        model switch
        {
            HardwareModel.Dmg => ApuModelSpec.Dmg,
            HardwareModel.Cgb => ApuModelSpec.Cgb,
            HardwareModel.Sgb => ApuModelSpec.Sgb,
            _ => throw new ArgumentOutOfRangeException(nameof(model)),
        };
}
