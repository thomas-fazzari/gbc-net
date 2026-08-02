// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Emulation;
using GbcNet.App.Saves;
using GbcNet.Core;
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
        audioOutput.ClearCount.Should().Be(3);
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
}
