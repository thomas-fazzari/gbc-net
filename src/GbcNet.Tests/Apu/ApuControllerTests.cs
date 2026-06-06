using GbcNet.Core.Apu;
using GbcNet.Core.Apu.Profiles;

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
        ApuController apu = new(new DmgApuHardwareProfile());

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(address, writeValue);

        Assert.Equal(expected, (byte)(apu.ReadRegister(address) & mask));
    }

    [Fact]
    public void SetRegisterState_CanSeedAudioMasterStatusBits()
    {
        ApuController apu = new(new DmgApuHardwareProfile());

        apu.SetRegisterState(0xFF26, 0x81);

        Assert.Equal(0xF1, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void WriteRegister_CannotSetAudioMasterStatusBits()
    {
        ApuController apu = new(new DmgApuHardwareProfile());

        apu.WriteRegister(0xFF26, 0x81);

        Assert.Equal(0xF0, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void WriteRegister_IgnoresNonMasterRegistersWhenPoweredOff()
    {
        ApuController apu = new(new DmgApuHardwareProfile());

        apu.WriteRegister(0xFF24, 0x77);
        apu.WriteRegister(0xFF25, 0xFF);

        Assert.Equal(0x00, apu.ReadRegister(0xFF24));
        Assert.Equal(0x00, apu.ReadRegister(0xFF25));
    }

    [Fact]
    public void WriteRegister_AcceptsNonMasterRegistersWhenPoweredOn()
    {
        ApuController apu = new(new DmgApuHardwareProfile());

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF24, 0x77);
        apu.WriteRegister(0xFF25, 0xFF);

        Assert.Equal(0x77, apu.ReadRegister(0xFF24));
        Assert.Equal(0xFF, apu.ReadRegister(0xFF25));
    }

    [Fact]
    public void WriteRegister_PoweringOffClearsPoweredRegisters()
    {
        ApuController apu = new(new DmgApuHardwareProfile());

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
        ApuController apu = new(new DmgApuHardwareProfile());

        apu.SetRegisterState(0xFF26, 0x8F);

        apu.WriteRegister(0xFF26, 0x00);

        Assert.Equal(0x70, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void WriteRegister_TriggeringChannel2WithDacEnabledSetsAudioMasterChannel2Status()
    {
        ApuController apu = new(new DmgApuHardwareProfile());

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF17, 0xF0);
        apu.WriteRegister(0xFF19, 0x80);

        Assert.Equal(0xF2, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void WriteRegister_TriggeringChannel2WithDacDisabledKeepsChannel2Inactive()
    {
        ApuController apu = new(new DmgApuHardwareProfile());

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF17, 0x00);
        apu.WriteRegister(0xFF19, 0x80);

        Assert.Equal(0xF0, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void WriteRegister_DisablingChannel2DacClearsChannel2Status()
    {
        ApuController apu = new(new DmgApuHardwareProfile());

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF17, 0xF0);
        apu.WriteRegister(0xFF19, 0x80);

        apu.WriteRegister(0xFF17, 0x00);

        Assert.Equal(0xF0, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void WriteRegister_PoweringOffDisablesChannel2()
    {
        ApuController apu = new(new DmgApuHardwareProfile());

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
        ApuController apu = new(new DmgApuHardwareProfile());

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
        ApuController apu = new(new DmgApuHardwareProfile());

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
        ApuController apu = new(new DmgApuHardwareProfile());

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF16, 0x3F);
        apu.WriteRegister(0xFF17, 0xF0);
        apu.WriteRegister(0xFF19, 0xC0);
        apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: false));

        apu.WriteRegister(0xFF19, 0xC0);
        for (int lengthEvents = 0; lengthEvents < 63; )
        {
            if (apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: false)).Length)
            {
                lengthEvents++;
            }
        }

        Assert.Equal(0xF2, apu.ReadRegister(0xFF26));

        ApuFrameSequencerEvents events;
        do
        {
            events = apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: false));
        } while (!events.Length);

        Assert.Equal(0xF0, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void WriteRegister_TriggeringChannel2LoadsEnvelopeInitialVolume()
    {
        ApuController apu = new(new DmgApuHardwareProfile());

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF17, 0xA2);
        apu.WriteRegister(0xFF19, 0x80);

        Assert.Equal(10, apu.Channel2Volume);
    }

    [Fact]
    public void TickSystemCounter_IncreasesChannel2VolumeAtEnvelopePace()
    {
        ApuController apu = new(new DmgApuHardwareProfile());

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF17, 0x1A);
        apu.WriteRegister(0xFF19, 0x80);

        for (int envelopeEvents = 0; envelopeEvents < 2; )
        {
            if (apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: false)).Envelope)
            {
                envelopeEvents++;
            }
        }

        Assert.Equal(2, apu.Channel2Volume);
    }

    [Fact]
    public void TickSystemCounter_DecreasesChannel2VolumeAtEnvelopePace()
    {
        ApuController apu = new(new DmgApuHardwareProfile());

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF17, 0x21);
        apu.WriteRegister(0xFF19, 0x80);

        ApuFrameSequencerEvents events;
        do
        {
            events = apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: false));
        } while (!events.Envelope);

        Assert.Equal(1, apu.Channel2Volume);
    }

    [Fact]
    public void TickSystemCounter_DoesNotChangeChannel2VolumeWhenEnvelopePaceIsZero()
    {
        ApuController apu = new(new DmgApuHardwareProfile());

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF17, 0x58);
        apu.WriteRegister(0xFF19, 0x80);

        for (int envelopeEvents = 0; envelopeEvents < 2; )
        {
            if (apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: false)).Envelope)
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
        ApuController apu = new(new DmgApuHardwareProfile());

        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF17, envelope);
        apu.WriteRegister(0xFF19, 0x80);

        ApuFrameSequencerEvents events;
        do
        {
            events = apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: false));
        } while (!events.Envelope);

        Assert.Equal(expectedVolume, apu.Channel2Volume);
    }

    [Fact]
    public void TickSystemCounter_AdvancesDivApuStepOnNormalSpeedDivBit4FallingEdge()
    {
        ApuController apu = new(new DmgApuHardwareProfile());

        ApuFrameSequencerEvents events = apu.TickSystemCounter(
            new ApuTickInputs(1 << 12, CgbDoubleSpeed: false)
        );

        Assert.Equal(1, apu.DivApuStep);
        Assert.True(events.Length);
        Assert.False(events.Sweep);
        Assert.False(events.Envelope);
    }

    [Fact]
    public void TickSystemCounter_IgnoresOtherSystemCounterFallingEdges()
    {
        ApuController apu = new(new DmgApuHardwareProfile());

        ApuFrameSequencerEvents events = apu.TickSystemCounter(
            new ApuTickInputs(1 << 11, CgbDoubleSpeed: false)
        );

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
        ApuController apu = new(new DmgApuHardwareProfile());
        ApuFrameSequencerEvents events = default;

        for (int tick = 0; tick < ticks; tick++)
        {
            events = apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: false));
        }

        Assert.Equal(expectedStep, apu.DivApuStep);
        Assert.Equal(expectedLength, events.Length);
        Assert.Equal(expectedSweep, events.Sweep);
        Assert.Equal(expectedEnvelope, events.Envelope);
    }

    [Theory]
    [InlineData(0xFF10)]
    [InlineData(0xFF14)]
    [InlineData(0xFF1E)]
    [InlineData(0xFF26)]
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
