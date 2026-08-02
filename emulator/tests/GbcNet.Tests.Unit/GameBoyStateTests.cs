// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using System.Text.Json.Nodes;
using GbcNet.Core;
using GbcNet.Core.Cheats;
using GbcNet.Core.Hardware;
using GbcNet.Core.Interrupts;
using GbcNet.Core.Memory;

namespace GbcNet.Tests.Unit;

public sealed class GameBoyStateTests
{
    [Fact]
    public void RestoreState_RestoresIndependentMachineContinuation()
    {
        var cartridge = TestRomFactory.LoadCartridge(bytes =>
        {
            bytes[0x0100] = 0x04;
            bytes[0x0101] = 0x04;
        });
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);

        gameBoy.Step();
        var state = gameBoy.CaptureState();
        gameBoy.Bus.WriteByte(AddressMap.HighRamStart, 0xAB);
        gameBoy.Step();

        gameBoy.RestoreState(state);

        gameBoy.Bus.ReadByte(AddressMap.HighRamStart).Should().Be(0x00);
        gameBoy.Cpu.Registers.B.Should().Be(0x01);
        gameBoy.Cpu.Registers.PC.Should().Be(0x0101);
        gameBoy.Step();
        gameBoy.Cpu.Registers.B.Should().Be(0x02);
    }

    [Fact]
    public void RestoreSaveState_DecodesCompleteContinuation()
    {
        var cartridge = TestRomFactory.LoadCartridge(bytes => bytes[0x0100] = 0x04);
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        var state = gameBoy.CaptureSaveState();
        gameBoy.Bus.WriteByte(AddressMap.HighRamStart, 0xAB);
        gameBoy.Step();

        gameBoy.RestoreSaveState(state);

        gameBoy.Bus.ReadByte(AddressMap.HighRamStart).Should().Be(0x00);
        gameBoy.Cpu.Registers.B.Should().Be(0x00);
        gameBoy.Cpu.Registers.PC.Should().Be(0x0100);
    }

    [Fact]
    public void RestoreSaveState_PreservesInvalidOpcodeHardLock()
    {
        var source = new GameBoy(TestRomFactory.LoadCartridge(ConfigureRom), HardwareModel.Dmg);
        source.Step();

        var restored = new GameBoy(TestRomFactory.LoadCartridge(ConfigureRom), HardwareModel.Dmg);
        restored.RestoreSaveState(source.CaptureSaveState());
        restored.Bus.Interrupts.Request(InterruptSource.VBlank);

        restored.Step().Should().Be(1);
        restored.Cpu.Halted.Should().BeTrue();
        restored.Bus.Interrupts.InterruptEnable.Should().Be(0);
        restored.Cpu.Registers.PC.Should().Be(0x0101);

        static void ConfigureRom(byte[] bytes) => bytes[0x0100] = 0xD3;
    }

    [Fact]
    public void RestoreSaveState_RejectsMalformedPayload()
    {
        var gameBoy = new GameBoy(TestRomFactory.LoadCartridge(), HardwareModel.Dmg);

        FluentActions
            .Invoking(() => gameBoy.RestoreSaveState([0xC1]))
            .Should()
            .ThrowExactly<InvalidDataException>();
    }

    [Fact]
    public void RestoreSaveState_RejectsPayloadWithoutRequiredMember()
    {
        var gameBoy = new GameBoy(TestRomFactory.LoadCartridge(), HardwareModel.Dmg);
        var payload = JsonNode.Parse(gameBoy.CaptureSaveState())!.AsObject();
        payload.Remove("Cpu");

        FluentActions
            .Invoking(() => gameBoy.RestoreSaveState(JsonSerializer.SerializeToUtf8Bytes(payload)))
            .Should()
            .ThrowExactly<InvalidDataException>();
    }

    [Fact]
    public void RestoreState_RejectsCorruptionBeforeMutatingMachine()
    {
        var gameBoy = new GameBoy(TestRomFactory.LoadCartridge(), HardwareModel.Dmg);
        gameBoy.Bus.WriteByte(AddressMap.HighRamStart, 0xAB);
        var state = gameBoy.CaptureState();
        var corruptState = new GameBoyState(
            HardwareModel.Dmg,
            state.Cpu,
            state.Bus with
            {
                HighRam = new MappedMemoryState([0x00]),
            }
        );
        gameBoy.Bus.WriteByte(AddressMap.HighRamStart, 0xCD);

        FluentActions
            .Invoking(() => gameBoy.RestoreState(corruptState))
            .Should()
            .ThrowExactly<ArgumentException>();
        gameBoy.Bus.ReadByte(AddressMap.HighRamStart).Should().Be(0xCD);
    }

    [Fact]
    public void RestoreState_DoesNotNotifySerialObservers()
    {
        var gameBoy = new GameBoy(TestRomFactory.LoadCartridge(), HardwareModel.Dmg);
        gameBoy.Bus.WriteByte(AddressMap.SerialTransferDataRegister, 0xAB);
        gameBoy.Bus.WriteByte(AddressMap.SerialTransferControlRegister, 0x81);
        var state = gameBoy.CaptureState();
        var notified = false;
        gameBoy.SerialByteTransferred += _ => notified = true;

        gameBoy.RestoreState(state);

        notified.Should().BeFalse();
    }

    [Theory]
    [InlineData(HardwareModel.Dmg)]
    [InlineData(HardwareModel.Cgb)]
    [InlineData(HardwareModel.Sgb)]
    public void RestoreSaveState_ReplaysIdenticalContinuation(HardwareModel hardwareModel)
    {
        var cartridge = TestRomFactory.LoadCartridge(
            TestRomFactory.Create(bytes =>
            {
                bytes[0x0100] = 0xC3;
                bytes[0x0101] = 0x50;
                bytes[0x0102] = 0x01;
                bytes[0x0150] = 0x04;
                bytes[0x0151] = 0x78;
                bytes[0x0152] = 0xEA;
                bytes[0x0153] = 0x00;
                bytes[0x0154] = 0xC0;
                bytes[0x0155] = 0x18;
                bytes[0x0156] = 0xF9;

                if (hardwareModel is HardwareModel.Cgb)
                {
                    bytes[0x0143] = 0x80;
                }
                else if (hardwareModel is HardwareModel.Sgb)
                {
                    bytes[0x0146] = 0x03;
                    bytes[0x014B] = 0x33;
                }
            })
        );
        var gameBoy = new GameBoy(cartridge, hardwareModel);
        byte[]? latestFrame = null;
        gameBoy.FrameCompleted += frame =>
        {
            using (frame)
            {
                latestFrame = frame.Pixels.ToArray();
            }
        };

        for (var step = 0; step < 10_000; step++)
        {
            gameBoy.Step();
        }

        var saveState = gameBoy.CaptureSaveState();
        latestFrame = null;
        for (var step = 0; step < 20_000; step++)
        {
            gameBoy.Step();
        }

        var expectedState = gameBoy.CaptureSaveState();
        var expectedFrame = latestFrame.Should().BeOfType<byte[]>().Subject;

        gameBoy.RestoreSaveState(saveState);
        latestFrame = null;
        for (var step = 0; step < 20_000; step++)
        {
            gameBoy.Step();
        }

        gameBoy.CaptureSaveState().Should().Equal(expectedState);
        latestFrame.Should().BeOfType<byte[]>().Subject.Should().Equal(expectedFrame);
    }

    [Fact]
    public void RestoreSaveState_KeepsCurrentCheatCodesAndRestoresBootRomGating()
    {
        var gameBoy = new GameBoy(
            TestRomFactory.LoadCartridge(bytes => bytes[0x0100] = 0x55),
            HardwareModel.Cgb,
            new BootRomOptions { CgbBootRom = BootRomTestFactory.CreateCgb() }
        );
        gameBoy.Cheats.SetCodes([Parse("AA1-00F")]);
        var state = gameBoy.CaptureSaveState();

        gameBoy.Cheats.SetCodes([Parse("BB1-00F")]);
        gameBoy.Bus.WriteByte(AddressMap.BootRomDisableRegister, 0x01);
        gameBoy.Bus.ReadByte(0x0100).Should().Be(0xBB);

        gameBoy.RestoreSaveState(state);

        gameBoy.Bus.ReadByte(0x0100).Should().Be(0x55);

        gameBoy.Bus.WriteByte(AddressMap.BootRomDisableRegister, 0x01);

        gameBoy.Bus.ReadByte(0x0100).Should().Be(0xBB);
    }

    private static CheatCode Parse(string text)
    {
        CheatCode.TryParse(CheatCodeType.GameGenie, text, out var code).Should().BeTrue();
        return code;
    }

    [Fact]
    public void StateOperations_RejectCallsBeforeStepCompletes()
    {
        var gameBoy = new GameBoy(
            TestRomFactory.LoadCartridge(bytes =>
            {
                bytes[0x0100] = 0x00;
                bytes[0x01B9] = 0x44;
            }),
            HardwareModel.Dmg
        );
        var state = gameBoy.CaptureState();
        var originalCode = Parse("0A1-B9F");
        var replacementCode = Parse("0C1-B9F");
        gameBoy.Cheats.SetCodes([originalCode]);
        Exception? captureException = null;
        Exception? restoreException = null;
        Exception? gameGenieException = null;
        gameBoy.Cpu.InstructionExecuted += (_, _) =>
        {
            captureException = Record.Exception(gameBoy.CaptureState);
            restoreException = Record.Exception(() => gameBoy.RestoreState(state));
            gameGenieException = Record.Exception(() => gameBoy.Cheats.SetCodes([replacementCode]));
        };

        gameBoy.Step();

        captureException.Should().BeOfType<InvalidOperationException>();
        gameGenieException.Should().BeOfType<InvalidOperationException>();
        gameBoy.Bus.ReadByte(0x01B9).Should().Be(0x0A);
        restoreException.Should().BeOfType<InvalidOperationException>();
    }
}
