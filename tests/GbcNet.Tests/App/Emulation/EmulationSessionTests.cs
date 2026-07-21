// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Audio;
using GbcNet.App.Emulation;
using GbcNet.App.Saves;
using GbcNet.Core;
using GbcNet.Core.Apu;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Hardware;
using GbcNet.Core.Memory;
using GbcNet.Tests.Cartridges;

namespace GbcNet.Tests.App.Emulation;

public sealed class EmulationSessionTests
{
    [Fact]
    public async Task SaveStateRequests_CompleteWhilePausedAndClearAudioAfterRestore()
    {
        using var audioOutput = new TestAudioOutput();
        var session = new EmulationSession(
            new GameBoy(TestRomFactory.LoadCartridge(), HardwareModel.Dmg),
            audioOutput,
            static _ => { },
            static _ => { },
            batterySaveWriter: null
        )
        {
            IsPaused = true,
        };

        try
        {
            var state = await session
                .CaptureSaveStateAsync()
                .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
            var clearCount = audioOutput.ClearCount;

            await session
                .RestoreSaveStateAsync(state)
                .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

            Assert.True(audioOutput.ClearCount > clearCount);
        }
        finally
        {
            await session.StopAsync();
        }
    }

    [Fact]
    public async Task StopAsync_CompletesWhilePaused()
    {
        using var audioOutput = new TestAudioOutput();
        var session = new EmulationSession(
            new GameBoy(TestRomFactory.LoadCartridge(), HardwareModel.Dmg),
            audioOutput,
            static _ => { },
            static _ => { },
            batterySaveWriter: null
        )
        {
            IsPaused = true,
        };

        await session
            .StopAsync()
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        Assert.Equal(3, audioOutput.ClearCount);
    }

    [Fact]
    public async Task PrepareToStopAsync_KeepsSessionRunningWhenSaveFails()
    {
        var cartridge = TestRomFactory.LoadCartridge(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc1RamBattery;
            bytes[0x0149] = 0x02;
        });
        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);
        var allowWrites = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        CartridgeBatterySaveWriter writer = new(
            cartridge,
            _ =>
                allowWrites.Task.IsCompletedSuccessfully
                    ? Task.CompletedTask
                    : Task.FromException(new IOException("synthetic final write failure")),
            static _ => { }
        );
        using var audioOutput = new TestAudioOutput();
        var session = new EmulationSession(
            new GameBoy(cartridge, HardwareModel.Dmg),
            audioOutput,
            static _ => { },
            static _ => { },
            writer
        )
        {
            IsPaused = true,
        };

        try
        {
            await Assert.ThrowsAsync<IOException>(() =>
                session
                    .PrepareToStopAsync()
                    .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken)
            );

            Assert.True(session.IsPaused);
            _ = await session
                .CaptureSaveStateAsync()
                .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        }
        finally
        {
            allowWrites.TrySetResult();
            await session.StopAsync();
        }
    }

    private sealed class TestAudioOutput : IAudioOutput
    {
        public int ClearCount { get; private set; }

        public void EnqueueSamples(ReadOnlySpan<ApuStereoSample> samples) { }

        public void SetVolume(int volumePercent, bool muted) { }

        public void Clear() => ClearCount++;

        public void Dispose() { }
    }
}
