// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using BenchmarkDotNet.Attributes;
using GbcNet.Core.Hardware;

namespace GbcNet.Benchmarks;

[MemoryDiagnoser]
public class FrameBenchmarks
{
    private const int FramesPerIteration = 600;
    private BenchmarkMachine _machine = null!;

    [Params(HardwareModel.Dmg, HardwareModel.Cgb, HardwareModel.Sgb)]
    public HardwareModel Model { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _machine = new BenchmarkMachine(Model);
        _machine.VerifyRepeatability(FramesPerIteration);
    }

    [IterationSetup]
    public void Reset() => _machine.Reset();

    [Benchmark(OperationsPerInvoke = FramesPerIteration)]
    public long EmulateFrame() => _machine.AdvanceFrames(FramesPerIteration);
}
