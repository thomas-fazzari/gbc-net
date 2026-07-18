// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Text.Json;
using System.Text.Json.Nodes;
using GbcNet.Core;
using GbcNet.Core.Hardware;
using GbcNet.Core.Interrupts;
using GbcNet.Core.Memory;
using GbcNet.Tests.Cartridges;

namespace GbcNet.Tests;

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

        Assert.Equal(0x00, gameBoy.Bus.ReadByte(AddressMap.HighRamStart));
        Assert.Equal(0x01, gameBoy.Cpu.Registers.B);
        Assert.Equal(0x0101, gameBoy.Cpu.Registers.PC);
        gameBoy.Step();
        Assert.Equal(0x02, gameBoy.Cpu.Registers.B);
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

        Assert.Equal(0x00, gameBoy.Bus.ReadByte(AddressMap.HighRamStart));
        Assert.Equal(0x00, gameBoy.Cpu.Registers.B);
        Assert.Equal(0x0100, gameBoy.Cpu.Registers.PC);
    }

    [Fact]
    public void RestoreSaveState_PreservesInvalidOpcodeHardLock()
    {
        var source = new GameBoy(TestRomFactory.LoadCartridge(ConfigureRom), HardwareModel.Dmg);
        source.Step();

        var restored = new GameBoy(TestRomFactory.LoadCartridge(ConfigureRom), HardwareModel.Dmg);
        restored.RestoreSaveState(source.CaptureSaveState());
        restored.Bus.Interrupts.Request(InterruptSource.VBlank);

        Assert.Equal(1, restored.Step());
        Assert.True(restored.Cpu.Halted);
        Assert.Equal(0, restored.Bus.Interrupts.InterruptEnable);
        Assert.Equal(0x0101, restored.Cpu.Registers.PC);

        static void ConfigureRom(byte[] bytes) => bytes[0x0100] = 0xD3;
    }

    [Fact]
    public void RestoreSaveState_RejectsMalformedPayload()
    {
        var gameBoy = new GameBoy(TestRomFactory.LoadCartridge(), HardwareModel.Dmg);

        Assert.Throws<InvalidDataException>(() => gameBoy.RestoreSaveState([0xC1]));
    }

    [Fact]
    public void RestoreSaveState_RejectsPayloadWithoutRequiredMember()
    {
        var gameBoy = new GameBoy(TestRomFactory.LoadCartridge(), HardwareModel.Dmg);
        var payload = JsonNode.Parse(gameBoy.CaptureSaveState())!.AsObject();
        payload.Remove("Cpu");

        Assert.Throws<InvalidDataException>(() =>
            gameBoy.RestoreSaveState(JsonSerializer.SerializeToUtf8Bytes(payload))
        );
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

        Assert.Throws<ArgumentException>(() => gameBoy.RestoreState(corruptState));
        Assert.Equal(0xCD, gameBoy.Bus.ReadByte(AddressMap.HighRamStart));
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

        Assert.False(notified);
    }

    [Fact]
    public void StateOperations_RejectCallsBeforeStepCompletes()
    {
        var gameBoy = new GameBoy(
            TestRomFactory.LoadCartridge(bytes => bytes[0x0100] = 0x00),
            HardwareModel.Dmg
        );
        var state = gameBoy.CaptureState();
        Exception? captureException = null;
        Exception? restoreException = null;
        gameBoy.Cpu.InstructionExecuted += (_, _) =>
        {
            captureException = Record.Exception(() => gameBoy.CaptureState());
            restoreException = Record.Exception(() => gameBoy.RestoreState(state));
        };

        gameBoy.Step();

        Assert.IsType<InvalidOperationException>(captureException);
        Assert.IsType<InvalidOperationException>(restoreException);
    }
}
