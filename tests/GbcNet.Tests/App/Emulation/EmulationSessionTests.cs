// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Audio;
using GbcNet.App.Emulation;
using GbcNet.Core;
using GbcNet.Core.Apu;
using GbcNet.Core.Hardware;
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

    private sealed class TestAudioOutput : IAudioOutput
    {
        public int ClearCount { get; private set; }

        public void EnqueueSamples(ReadOnlySpan<ApuStereoSample> samples) { }

        public void Clear() => ClearCount++;

        public void Dispose() { }
    }
}
