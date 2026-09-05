// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using BenchmarkDotNet.Attributes;
using GbcNet.App.Saves;
using GbcNet.Core;
using GbcNet.Core.Hardware;

namespace GbcNet.Benchmarks;

[MemoryDiagnoser]
public class SaveStateBenchmarks
{
    private BenchmarkMachine _machine = null!;
    private byte[] _payload = null!;
    private byte[] _compressedPayload = null!;

    [Params(HardwareModel.Dmg, HardwareModel.Cgb, HardwareModel.Sgb)]
    public HardwareModel Model { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _machine = new BenchmarkMachine(Model);
        _payload = GameBoyStateCodec.Encode(_machine.InitialState);
        _compressedPayload = SaveStateFileService.Compress(_payload);
        var restoredPayload = SaveStateFileService.Decompress(_compressedPayload, _payload.Length);
        _machine.GameBoy.RestoreSaveState(restoredPayload);

        if (!_payload.AsSpan().SequenceEqual(_machine.GameBoy.CaptureSaveState()))
        {
            throw new InvalidOperationException(
                "Benchmark save-state round trip changed the state."
            );
        }

        Console.WriteLine(
            $"// Payload bytes: {_payload.Length}, compressed: {_compressedPayload.Length}"
        );
    }

    [Benchmark]
    public GameBoyState CaptureState() => _machine.GameBoy.CaptureState();

    [Benchmark]
    public byte[] EncodeState() => GameBoyStateCodec.Encode(_machine.InitialState);

    [Benchmark]
    public byte[] CaptureSaveState() => _machine.GameBoy.CaptureSaveState();

    [Benchmark]
    public GameBoyState DecodeState() => GameBoyStateCodec.Decode(_payload);

    [Benchmark]
    public void RestoreSaveState() => _machine.GameBoy.RestoreSaveState(_payload);

    [Benchmark]
    public byte[] CompressPayload() => SaveStateFileService.Compress(_payload);

    [Benchmark]
    public byte[] DecompressPayload() =>
        SaveStateFileService.Decompress(_compressedPayload, _payload.Length);
}
