// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using System.Security.Cryptography;
using GbcNet.Core;
using GbcNet.Core.Apu;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Hardware;
using GbcNet.Core.Ppu;

namespace GbcNet.Benchmarks;

internal sealed class BenchmarkMachine
{
    private const int WarmupFrames = 60;

    private readonly ApuStereoSample[] _audioSamples = new ApuStereoSample[2048];
    private int _completedFrames;

    internal BenchmarkMachine(HardwareModel model)
    {
        var romName = model switch
        {
            HardwareModel.Dmg or HardwareModel.Sgb => "dmg-acid2.gb",
            HardwareModel.Cgb => "cgb-acid2.gbc",
            _ => throw new ArgumentOutOfRangeException(nameof(model), model, message: null),
        };
        var rom = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Roms", romName));
        GameBoy = new GameBoy(Cartridge.LoadOrThrow(rom), model);
        GameBoy.FrameCompleted += OnFrameCompleted;
        AdvanceFrames(WarmupFrames);
        InitialState = GameBoy.CaptureState();

        Console.WriteLine($"// Workload: {model}, {romName}, warmup={WarmupFrames} frames");
        Console.WriteLine($"// ROM SHA256: {Convert.ToHexString(SHA256.HashData(rom))}");
    }

    internal GameBoy GameBoy { get; }

    internal GameBoyState InitialState { get; }

    internal void Reset()
    {
        GameBoy.RestoreState(InitialState);
        _completedFrames = 0;
    }

    internal long AdvanceFrames(int frameCount)
    {
        var targetFrame = _completedFrames + frameCount;
        var maximumMachineCycles = (long)frameCount * GameBoyTiming.DoubleCpuHz;
        long machineCycles = 0;

        while (_completedFrames < targetFrame && machineCycles < maximumMachineCycles)
        {
            var previousFrame = _completedFrames;
            machineCycles += GameBoy.Step();
            if (_completedFrames != previousFrame)
            {
                while (GameBoy.DrainAudioSamples(_audioSamples) == _audioSamples.Length) { }
            }
        }

        if (_completedFrames != targetFrame)
        {
            throw new InvalidOperationException(
                "Benchmark ROM did not produce the requested frames."
            );
        }

        return machineCycles;
    }

    internal void VerifyRepeatability(int frameCount)
    {
        Reset();
        var firstCycles = AdvanceFrames(frameCount);
        var firstState = GameBoy.CaptureSaveState();
        Reset();
        var secondCycles = AdvanceFrames(frameCount);
        var secondState = GameBoy.CaptureSaveState();

        if (firstCycles != secondCycles || !firstState.AsSpan().SequenceEqual(secondState))
        {
            throw new InvalidOperationException("Benchmark frame workload is not repeatable.");
        }

        Reset();
    }

    private void OnFrameCompleted(LcdFrame frame)
    {
        frame.Dispose();
        _completedFrames++;
    }
}
