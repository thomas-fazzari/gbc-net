// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Apu;
using GbcNet.Core.Apu.Components;

namespace GbcNet.Tests.Apu;

public sealed class ApuStateTests
{
    [Theory]
    [MemberData(nameof(ModelSpecs))]
    public void CaptureRestore_PostBootRoundTripsEveryModel(string model)
    {
        var spec = model switch
        {
            "DMG" => ApuModelSpec.Dmg,
            "CGB" => ApuModelSpec.Cgb,
            "SGB" => ApuModelSpec.Sgb,
            _ => throw new ArgumentOutOfRangeException(nameof(model)),
        };
        ApuController apu = new(spec);
        apu.SetRegisterState(0xFF10, 0x80);
        apu.SetRegisterState(0xFF24, 0x77);
        apu.SetRegisterState(0xFF25, 0xF3);
        apu.SetRegisterState(0xFF26, 0x8A);
        apu.SetRegisterState(0xFF30, 0xAB);

        var checkpoint = apu.CaptureState();
        apu.WriteRegister(0xFF26, 0);
        apu.RestoreState(checkpoint);

        Assert.Equivalent(checkpoint, apu.CaptureState(), strict: true);
        AssertRegisterBehaviorEqual(apu, Restored(spec, checkpoint));
    }

    [Fact]
    public void CaptureState_OwnsRegistersWaveRamAndPendingSamples()
    {
        var apu = CreatePulse(ApuModelSpec.Cgb);
        apu.SetRegisterState(0xFF30, 0xAB);
        apu.Tick(1_000);

        var checkpoint = apu.CaptureState();
        var expectedRegisters = (byte[])checkpoint.Registers.Clone();
        var expectedWaveRam = (byte[])checkpoint.Channel3.WaveRam.Clone();
        var expectedSamples = (ApuStereoSample[])checkpoint.SampleBuffer.BufferedSamples.Clone();
        checkpoint.Registers[0] = 0;
        checkpoint.Channel3.WaveRam[0] = 0;
        if (checkpoint.SampleBuffer.BufferedSamples.Length > 0)
        {
            checkpoint.SampleBuffer.BufferedSamples[0] = default;
        }

        var current = apu.CaptureState();
        Assert.Equal(expectedRegisters, current.Registers);
        Assert.Equal(expectedWaveRam, current.Channel3.WaveRam);
        Assert.Equal(expectedSamples, current.SampleBuffer.BufferedSamples);
    }

    [Fact]
    public void RestoreState_InvalidLateNestedStateIsAtomic()
    {
        var target = CreatePulse(ApuModelSpec.Cgb);
        target.Tick(500);
        var before = target.CaptureState();
        var malformed = CreateNoise(ApuModelSpec.Cgb).CaptureState() with
        {
            OutputFilter = new ApuOutputFilterState(double.NaN, 0),
        };

        Assert.Throws<ArgumentException>(() => target.RestoreState(malformed));
        Assert.Equivalent(before, target.CaptureState(), strict: true);
    }

    [Fact]
    public void RestoreState_ContinuesPulseDutyEnvelopeSweepAndFrameStepExactly()
    {
        var original = CreatePulse(ApuModelSpec.Cgb);
        original.Tick(5);
        TickFrame(original, 3);
        original.Tick(13);

        var restored = Restored(ApuModelSpec.Cgb, original.CaptureState());
        for (var index = 0; index < 12; index++)
        {
            Assert.Equal(TickFrame(original), TickFrame(restored));
            original.Tick(137 + index);
            restored.Tick(137 + index);
            AssertRegisterBehaviorEqual(original, restored);
            Assert.Equal(Drain(original), Drain(restored));
        }
    }

    [Fact]
    public void RestoreState_ContinuesMidWaveRetainedNibbleExactly()
    {
        ApuController original = new(ApuModelSpec.Cgb);
        original.WriteRegister(0xFF26, 0x80);
        original.WriteRegister(0xFF30, 0xAB);
        original.WriteRegister(0xFF1A, 0x80);
        original.WriteRegister(0xFF1B, 0);
        original.WriteRegister(0xFF1C, 0x20);
        original.WriteRegister(0xFF1D, 0xFE);
        original.WriteRegister(0xFF1E, 0x87);
        original.Tick(4);

        var checkpoint = original.CaptureState();
        Assert.Equal(0x0B, checkpoint.Channel3.SampleBuffer);
        var restored = Restored(ApuModelSpec.Cgb, checkpoint);

        original.Tick(9);
        restored.Tick(9);
        Assert.Equal(original.ReadRegister(0xFF77), restored.ReadRegister(0xFF77));
        Assert.Equal(Drain(original), Drain(restored));
    }

    [Fact]
    public void RestoreState_ContinuesNoiseLfsrWidthAndEnvelopeExactly()
    {
        var original = CreateNoise(ApuModelSpec.Cgb);
        original.Tick(83);
        TickFrame(original, 7);
        var restored = Restored(ApuModelSpec.Cgb, original.CaptureState());

        for (var index = 0; index < 8; index++)
        {
            original.Tick(31 + index);
            restored.Tick(31 + index);
            Assert.Equal(TickFrame(original), TickFrame(restored));
            Assert.Equal(original.ReadRegister(0xFF77), restored.ReadRegister(0xFF77));
            Assert.Equal(original.Channel4Volume, restored.Channel4Volume);
            Assert.Equal(Drain(original), Drain(restored));
        }
    }

    [Fact]
    public void RestoreState_PreservesPendingSampleOrderSchedulerRemainderAndUnequalFilterCapacitors()
    {
        var original = CreatePulse(ApuModelSpec.Cgb);
        original.WriteRegister(0xFF24, 0x70);
        original.Tick(1_001);
        var checkpoint = original.CaptureState();

        Assert.NotEmpty(checkpoint.SampleBuffer.BufferedSamples);
        Assert.NotEqual(
            checkpoint.OutputFilter.LeftCapacitor,
            checkpoint.OutputFilter.RightCapacitor
        );
        var restored = Restored(ApuModelSpec.Cgb, checkpoint);

        Assert.Equal(Drain(original), Drain(restored));
        original.Tick(97);
        restored.Tick(97);
        Assert.Equal(Drain(original), Drain(restored));
        Assert.Equivalent(original.CaptureState(), restored.CaptureState(), strict: true);
    }

    [Fact]
    public void RestoreState_PreservesPoweredOffState()
    {
        ApuController original = new(ApuModelSpec.Dmg);
        original.SetRegisterState(0xFF30, 0xAB);
        original.WriteRegister(0xFF26, 0);

        var restored = Restored(ApuModelSpec.Dmg, original.CaptureState());
        Assert.Equal(0x70, restored.ReadRegister(0xFF26));
        Assert.Equal(0xAB, restored.ReadRegister(0xFF30));
        restored.WriteRegister(0xFF24, 0x77);
        Assert.Equal(0, restored.ReadRegister(0xFF24));
    }

    public static TheoryData<string> ModelSpecs => ["DMG", "CGB", "SGB"];

    private static ApuController CreatePulse(ApuModelSpec spec)
    {
        ApuController apu = new(spec);
        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF24, 0x77);
        apu.WriteRegister(0xFF25, 0x11);
        apu.WriteRegister(0xFF10, 0x21);
        apu.WriteRegister(0xFF11, 0x80);
        apu.WriteRegister(0xFF12, 0xA2);
        apu.WriteRegister(0xFF13, 0xF8);
        apu.WriteRegister(0xFF14, 0x87);
        return apu;
    }

    private static ApuController CreateNoise(ApuModelSpec spec)
    {
        ApuController apu = new(spec);
        apu.WriteRegister(0xFF26, 0x80);
        apu.WriteRegister(0xFF24, 0x77);
        apu.WriteRegister(0xFF25, 0x88);
        apu.WriteRegister(0xFF21, 0xF2);
        apu.WriteRegister(0xFF22, 0x2F);
        apu.WriteRegister(0xFF23, 0x80);
        return apu;
    }

    private static ApuController Restored(ApuModelSpec spec, ApuControllerState state)
    {
        ApuController apu = new(spec);
        apu.RestoreState(state);
        return apu;
    }

    private static ApuFrameSequencerEvents TickFrame(ApuController apu, int count = 1)
    {
        var events = default(ApuFrameSequencerEvents);
        for (var index = 0; index < count; index++)
        {
            events = apu.TickSystemCounter(new ApuTickInputs(1 << 12, CgbDoubleSpeed: false));
        }

        return events;
    }

    private static ApuStereoSample[] Drain(ApuController apu)
    {
        var samples = new ApuStereoSample[512];
        return samples[..apu.DrainBufferedSamples(samples)];
    }

    private static void AssertRegisterBehaviorEqual(ApuController expected, ApuController actual)
    {
        foreach (var address in Enumerable.Range(0xFF10, 0x17).Select(address => (ushort)address))
        {
            if (address is not (0xFF15 or 0xFF1F))
            {
                Assert.Equal(expected.ReadRegister(address), actual.ReadRegister(address));
            }
        }

        foreach (var address in Enumerable.Range(0xFF30, 0x10).Select(address => (ushort)address))
        {
            Assert.Equal(expected.ReadRegister(address), actual.ReadRegister(address));
        }

        Assert.Equal(expected.ReadRegister(0xFF76), actual.ReadRegister(0xFF76));
        Assert.Equal(expected.ReadRegister(0xFF77), actual.ReadRegister(0xFF77));
    }
}
