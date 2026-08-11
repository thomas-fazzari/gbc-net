// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Apu;
using GbcNet.Core.Apu.Components;
using GbcNet.Core.Hardware;

namespace GbcNet.Tests.Unit.Apu;

public sealed class ApuStateTests
{
    [Theory]
    [MemberData(nameof(ModelSpecs))]
    public void CaptureRestore_PostBootRoundTripsEveryModel(HardwareModel model)
    {
        var spec = ModelSpecFor(model);
        ApuController apu = new(spec);
        apu.SetRegisterState(0xFF10, 0x80);
        apu.SetRegisterState(0xFF24, 0x77);
        apu.SetRegisterState(0xFF25, 0xF3);
        apu.SetRegisterState(0xFF26, 0x8A);
        apu.SetRegisterState(0xFF30, 0xAB);

        var checkpoint = apu.CaptureState();
        apu.WriteRegister(0xFF26, 0);
        apu.RestoreState(checkpoint);

        apu.CaptureState()
            .Should()
            .BeEquivalentTo(checkpoint, options => options.WithStrictOrdering());
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
        current.Registers.Should().Equal(expectedRegisters);
        current.Channel3.WaveRam.Should().Equal(expectedWaveRam);
        current.SampleBuffer.BufferedSamples.Should().Equal(expectedSamples);
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

        FluentActions
            .Invoking(() => target.RestoreState(malformed))
            .Should()
            .ThrowExactly<ArgumentException>();
        target
            .CaptureState()
            .Should()
            .BeEquivalentTo(before, options => options.WithStrictOrdering());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MaxValue)]
    public void RestoreState_RejectsUnsafePulseSchedulerAccumulator(int accumulator)
    {
        var apu = CreatePulse(ApuModelSpec.Cgb);
        var state = apu.CaptureState();
        var malformed = state with
        {
            Channel1 = state.Channel1 with { TCycleAccumulator = accumulator },
        };

        FluentActions
            .Invoking(() => apu.RestoreState(malformed))
            .Should()
            .ThrowExactly<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(0, 1, false)]
    [InlineData(8, -1, false)]
    [InlineData(8, 8, true)]
    [InlineData(8, 9, false)]
    public void RestoreState_RejectsUnsafeNoiseSchedulerStateBeforeMutation(
        int timer,
        int accumulator,
        bool isActive
    )
    {
        var target = CreatePulse(ApuModelSpec.Cgb);
        target.Tick(500);
        var before = target.CaptureState();
        var state = CreateNoise(ApuModelSpec.Cgb).CaptureState();
        var malformed = state with
        {
            Channel4 = state.Channel4 with
            {
                Timer = timer,
                TCycleAccumulator = accumulator,
                IsActive = isActive,
            },
        };

        FluentActions
            .Invoking(() => target.RestoreState(malformed))
            .Should()
            .ThrowExactly<ArgumentOutOfRangeException>();
        target
            .CaptureState()
            .Should()
            .BeEquivalentTo(before, options => options.WithStrictOrdering());
    }

    [Fact]
    public void RestoreState_RoundTripsSchedulerBoundaries()
    {
        var pulseState = CreatePulse(ApuModelSpec.Cgb).CaptureState();
        var noiseState = CreateNoise(ApuModelSpec.Cgb).CaptureState();
        ApuControllerState[] states =
        [
            pulseState with
            {
                Channel1 = pulseState.Channel1 with { TCycleAccumulator = 0 },
            },
            pulseState with
            {
                Channel1 = pulseState.Channel1 with { TCycleAccumulator = 3 },
            },
            noiseState with
            {
                Channel4 = noiseState.Channel4 with
                {
                    Timer = 0,
                    TCycleAccumulator = 0,
                    IsActive = false,
                },
            },
            noiseState with
            {
                Channel4 = noiseState.Channel4 with
                {
                    Timer = 8,
                    TCycleAccumulator = 0,
                    IsActive = true,
                },
            },
            noiseState with
            {
                Channel4 = noiseState.Channel4 with
                {
                    Timer = 8,
                    TCycleAccumulator = 7,
                    IsActive = true,
                },
            },
            noiseState with
            {
                Channel4 = noiseState.Channel4 with
                {
                    Timer = 8,
                    TCycleAccumulator = 7,
                    IsActive = false,
                },
            },
        ];

        foreach (var state in states)
        {
            Restored(ApuModelSpec.Cgb, state)
                .CaptureState()
                .Should()
                .BeEquivalentTo(state, options => options.WithStrictOrdering());
        }
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
            TickFrame(restored).Should().Be(TickFrame(original));
            original.Tick(137 + index);
            restored.Tick(137 + index);
            AssertRegisterBehaviorEqual(original, restored);
            Drain(restored).Should().Equal(Drain(original));
        }
    }

    [Fact]
    public void RestoreState_PreservesSweepSubtractionHistory()
    {
        // Pan Docs `audio-details.md`: clearing negate after any subtraction disables CH1.
        ApuController original = new(ApuModelSpec.Cgb);
        original.WriteRegister(0xFF26, 0x80);
        original.WriteRegister(0xFF10, 0x09);
        original.WriteRegister(0xFF12, 0xF0);
        original.WriteRegister(0xFF13, 0x00);
        original.WriteRegister(0xFF14, 0x84);
        var checkpoint = original.CaptureState();
        checkpoint.Channel1Sweep.SubtractionCalculated.Should().BeTrue();
        var restored = Restored(ApuModelSpec.Cgb, checkpoint);

        restored.WriteRegister(0xFF10, 0x01);

        restored.ReadRegister(0xFF26).Should().Be(0xF0);
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
        checkpoint.Channel3.SampleBuffer.Should().Be(0x0B);
        var restored = Restored(ApuModelSpec.Cgb, checkpoint);
        restored
            .CaptureState()
            .Should()
            .BeEquivalentTo(checkpoint, options => options.WithStrictOrdering());

        original.Tick(9);
        restored.Tick(9);
        restored.ReadRegister(0xFF77).Should().Be(original.ReadRegister(0xFF77));
        Drain(restored).Should().Equal(Drain(original));
    }

    [Fact]
    public void RestoreState_PreservesMonochromeWaveRamAccessWindow()
    {
        // Pan Docs `audio-registers.md`: monochrome Wave RAM access is limited to CH3's
        // current read cycle, so this transient window must survive a save-state.
        ApuController original = new(ApuModelSpec.Dmg);
        original.WriteRegister(0xFF30, 0xAB);
        original.WriteRegister(0xFF26, 0x80);
        original.WriteRegister(0xFF1A, 0x80);
        original.WriteRegister(0xFF1D, 0xFE);
        original.WriteRegister(0xFF1E, 0x87);
        original.Tick(4);
        var checkpoint = original.CaptureState();
        checkpoint.Channel3.WaveRamAccessWindowOpen.Should().BeTrue();

        var restored = Restored(ApuModelSpec.Dmg, checkpoint);

        restored.ReadRegister(0xFF3F).Should().Be(0xAB);
        restored.Tick(2);
        restored.ReadRegister(0xFF3F).Should().Be(0xFF);
    }

    [Fact]
    public void RestoreState_RejectsMonochromeWaveRamAccessWindowOnCgbBeforeMutation()
    {
        ApuController target = new(ApuModelSpec.Cgb);
        var before = target.CaptureState();
        var malformed = before with
        {
            Channel3 = before.Channel3 with { WaveRamAccessWindowOpen = true },
        };

        FluentActions
            .Invoking(() => target.RestoreState(malformed))
            .Should()
            .ThrowExactly<ArgumentException>();
        target
            .CaptureState()
            .Should()
            .BeEquivalentTo(before, options => options.WithStrictOrdering());
    }

    [Fact]
    public void RestoreState_RejectsWaveRamAccessWindowWithUnreloadedTimerBeforeMutation()
    {
        ApuController target = new(ApuModelSpec.Dmg);
        target.WriteRegister(0xFF30, 0xAB);
        target.WriteRegister(0xFF26, 0x80);
        target.WriteRegister(0xFF1A, 0x80);
        target.WriteRegister(0xFF1D, 0xFE);
        target.WriteRegister(0xFF1E, 0x87);
        target.Tick(4);
        var state = target.CaptureState();
        var malformed = state with { Channel3 = state.Channel3 with { PeriodTimer = 1 } };

        FluentActions
            .Invoking(() => target.RestoreState(malformed))
            .Should()
            .ThrowExactly<ArgumentException>();
        target
            .CaptureState()
            .Should()
            .BeEquivalentTo(state, options => options.WithStrictOrdering());
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
            TickFrame(restored).Should().Be(TickFrame(original));
            restored.ReadRegister(0xFF77).Should().Be(original.ReadRegister(0xFF77));
            restored.Channel4Volume.Should().Be(original.Channel4Volume);
            Drain(restored).Should().Equal(Drain(original));
        }
    }

    [Fact]
    public void RestoreState_PreservesPendingSampleOrderSchedulerRemainderAndUnequalFilterCapacitors()
    {
        var original = CreatePulse(ApuModelSpec.Cgb);
        original.WriteRegister(0xFF24, 0x70);
        original.Tick(1_001);
        var checkpoint = original.CaptureState();

        checkpoint.SampleBuffer.BufferedSamples.Should().NotBeEmpty();
        checkpoint
            .OutputFilter.RightCapacitor.Should()
            .NotBe(checkpoint.OutputFilter.LeftCapacitor);
        var restored = Restored(ApuModelSpec.Cgb, checkpoint);

        Drain(restored).Should().Equal(Drain(original));
        original.Tick(97);
        restored.Tick(97);
        Drain(restored).Should().Equal(Drain(original));
        restored
            .CaptureState()
            .Should()
            .BeEquivalentTo(original.CaptureState(), options => options.WithStrictOrdering());
    }

    [Fact]
    public void RestoreState_PreservesPoweredOffState()
    {
        ApuController original = new(ApuModelSpec.Dmg);
        original.SetRegisterState(0xFF30, 0xAB);
        original.WriteRegister(0xFF26, 0);

        var restored = Restored(ApuModelSpec.Dmg, original.CaptureState());
        restored.ReadRegister(0xFF26).Should().Be(0x70);
        restored.ReadRegister(0xFF30).Should().Be(0xAB);
        restored.WriteRegister(0xFF24, 0x77);
        restored.ReadRegister(0xFF24).Should().Be(0);
    }

    public static TheoryData<HardwareModel> ModelSpecs =>
        [HardwareModel.Dmg, HardwareModel.Cgb, HardwareModel.Sgb];

    private static ApuModelSpec ModelSpecFor(HardwareModel model) =>
        model switch
        {
            HardwareModel.Dmg => ApuModelSpec.Dmg,
            HardwareModel.Cgb => ApuModelSpec.Cgb,
            HardwareModel.Sgb => ApuModelSpec.Sgb,
            _ => throw new ArgumentOutOfRangeException(nameof(model)),
        };

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
                actual.ReadRegister(address).Should().Be(expected.ReadRegister(address));
            }
        }

        foreach (var address in Enumerable.Range(0xFF30, 0x10).Select(address => (ushort)address))
        {
            actual.ReadRegister(address).Should().Be(expected.ReadRegister(address));
        }

        actual.ReadRegister(0xFF76).Should().Be(expected.ReadRegister(0xFF76));
        actual.ReadRegister(0xFF77).Should().Be(expected.ReadRegister(0xFF77));
    }
}
