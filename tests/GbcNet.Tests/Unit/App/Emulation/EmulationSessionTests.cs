// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Emulation;
using GbcNet.App.Saves;
using GbcNet.Core;
using GbcNet.Core.Apu;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Cheats;
using GbcNet.Core.Hardware;
using GbcNet.Core.Memory;

namespace GbcNet.Tests.Unit.App.Emulation;

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

            (audioOutput.ClearCount > clearCount).Should().BeTrue();
        }
        finally
        {
            await session.StopAsync();
        }
    }

    [Fact]
    public async Task SetCheatCodesAsync_SwapsAndClearsGameGenieRomReadsWhilePaused()
    {
        CheatCode.TryParse(CheatCodeType.GameGenie, "0A1-B9F", out var code).Should().BeTrue();
        var gameBoy = new GameBoy(TestRomFactory.LoadCartridge(), HardwareModel.Dmg);
        using var audioOutput = new TestAudioOutput();
        var session = new EmulationSession(
            gameBoy,
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
            await session
                .SetCheatCodesAsync([code])
                .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
            gameBoy.Bus.ReadByte(code.Address).Should().Be(0x0A);

            await session
                .SetCheatCodesAsync([])
                .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
            gameBoy.Bus.ReadByte(code.Address).Should().Be(0x00);
            session.IsPaused.Should().BeTrue();
        }
        finally
        {
            await session.StopAsync();
        }
    }

    [Fact]
    public async Task StopAsync_CompletesWhilePaused()
    {
        var fatalFaultCount = 0;
        using var audioOutput = new TestAudioOutput();

        var session = new EmulationSession(
            new GameBoy(TestRomFactory.LoadCartridge(), HardwareModel.Dmg),
            audioOutput,
            static _ => { },
            _ => Interlocked.Increment(ref fatalFaultCount),
            batterySaveWriter: null
        )
        {
            IsPaused = true,
        };

        await session
            .StopAsync()
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        audioOutput.ClearCount.Should().Be(3);

        fatalFaultCount.Should().Be(0);
    }

    [Fact]
    public async Task StoppedSession_RejectsMachineOperationAsCancellation()
    {
        using var audioOutput = new TestAudioOutput();

        var session = new EmulationSession(
            new GameBoy(TestRomFactory.LoadCartridge(), HardwareModel.Dmg),
            audioOutput,
            static _ => { },
            static _ => { },
            batterySaveWriter: null
        );

        await session.StopAsync();

        await FluentActions
            .Awaiting(() => session.CaptureSaveStateAsync())
            .Should()
            .ThrowExactlyAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExpectedMachineOperationFault_CompletesRequestWithoutStoppingSession()
    {
        var fatalFaultCount = 0;
        using var audioOutput = new TestAudioOutput();

        var session = new EmulationSession(
            new GameBoy(TestRomFactory.LoadCartridge(), HardwareModel.Dmg),
            audioOutput,
            static _ => { },
            _ => Interlocked.Increment(ref fatalFaultCount),
            batterySaveWriter: null
        )
        {
            IsPaused = true,
        };

        try
        {
            await FluentActions
                .Awaiting(() =>
                    session
                        .RestoreSaveStateAsync([])
                        .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken)
                )
                .Should()
                .ThrowExactlyAsync<InvalidDataException>();

            _ = await session
                .CaptureSaveStateAsync()
                .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
            fatalFaultCount.Should().Be(0);
        }
        finally
        {
            await session.StopAsync();
        }
    }

    [Fact]
    public async Task UnexpectedMachineOperationFault_CompletesRequestAndReportsFatalFaultOnce()
    {
        var gameBoy = new GameBoy(TestRomFactory.LoadCartridge(), HardwareModel.Dmg);
        var state = gameBoy.CaptureSaveState();

        var fatalFaultCount = 0;
        var fatalFault = new TaskCompletionSource<Exception>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        using var audioOutput = new ThrowOnceAudioOutput(throwOnClearCall: 2);
        var session = new EmulationSession(
            gameBoy,
            audioOutput,
            static _ => { },
            exception =>
            {
                Interlocked.Increment(ref fatalFaultCount);
                fatalFault.TrySetResult(exception);
            },
            batterySaveWriter: null
        );

        var operationException = (
            await FluentActions
                .Awaiting(() =>
                    session
                        .RestoreSaveStateAsync(state)
                        .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken)
                )
                .Should()
                .ThrowExactlyAsync<TimeoutException>()
        ).Which;
        var reportedException = await fatalFault.Task.WaitAsync(
            TimeSpan.FromSeconds(1),
            TestContext.Current.CancellationToken
        );
        await session.StopAsync();

        reportedException.Should().BeSameAs(operationException);
        fatalFaultCount.Should().Be(1);
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
            await FluentActions
                .Awaiting(() =>
                    session
                        .PrepareToStopAsync()
                        .WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken)
                )
                .Should()
                .ThrowExactlyAsync<IOException>();

            session.IsPaused.Should().BeTrue();
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

    private sealed class ThrowOnceAudioOutput(int throwOnClearCall) : IAudioOutput
    {
        private int _clearCount;

        public void EnqueueSamples(ReadOnlySpan<ApuStereoSample> samples) { }

        public void SetVolume(int volumePercent, bool muted) { }

        public void Clear()
        {
            if (Interlocked.Increment(ref _clearCount) == throwOnClearCall)
            {
                throw new TimeoutException("Synthetic unexpected machine operation failure.");
            }
        }

        public void Dispose() { }
    }
}
