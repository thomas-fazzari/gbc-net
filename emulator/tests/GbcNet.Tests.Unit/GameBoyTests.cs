// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core;
using GbcNet.Core.Apu;
using GbcNet.Core.Hardware;
using GbcNet.Core.Joypad;
using GbcNet.Core.Memory;
using GbcNet.Core.Ppu;
using static GbcNet.Tests.Shared.Opcodes;
using static GbcNet.Tests.Unit.Sgb.SgbTestHelpers;

namespace GbcNet.Tests.Unit;

public sealed class GameBoyTests
{
    private const byte JumpImmediate16Opcode = 0xC3;
    private const byte LoadAImmediate8Opcode = 0x3E;
    private const byte LoadHighMemoryAImmediate8Opcode = 0xE0;
    private const byte LcdControlEnabled = 0x91;

    [Fact]
    public void Step_ReturnsCpuMachineCyclesAndTicksTimer()
    {
        var cartridge = TestRomFactory.LoadCartridge();
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        gameBoy.Bus.WriteByte(AddressMap.TimerControlRegister, 0b0000_0101);

        var machineCycles = gameBoy.Step();
        gameBoy.Step();
        gameBoy.Step();
        gameBoy.Step();

        machineCycles.Should().Be(1);
        gameBoy.Bus.ReadByte(AddressMap.TimerCounterRegister).Should().Be(0x01);
    }

    [Fact]
    public void Step_ReturnsZeroAfterCpuEntersStop()
    {
        var cartridge = TestRomFactory.LoadCartridge(bytes =>
        {
            bytes[0x0100] = StopOpcode;
            bytes[0x0101] = 0x00;
        });
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);

        gameBoy.Step().Should().Be(2);
        gameBoy.Step().Should().Be(0);
    }

    [Fact]
    public void CpuMachineCyclesPerSecond_DoublesAfterCgbSpeedSwitch()
    {
        var cartridge = TestRomFactory.LoadCartridge(bytes =>
        {
            bytes[0x0100] = StopOpcode;
            bytes[0x0101] = 0x00;
            bytes[0x0143] = 0xC0;
        });
        var gameBoy = new GameBoy(cartridge, HardwareModel.Cgb);

        gameBoy.CpuMachineCyclesPerSecond.Should().Be(GameBoyTiming.NormalCpuHz);

        gameBoy.Bus.WriteByte(AddressMap.Key1Register, 0x01);
        gameBoy.Step();

        gameBoy.CpuMachineCyclesPerSecond.Should().Be(GameBoyTiming.DoubleCpuHz);
    }

    [Fact]
    public void Step_ConsumesCgbSpeedSwitchPauseWithoutAdvancingDividerThenResumesCpu()
    {
        var cartridge = TestRomFactory.LoadCartridge(bytes =>
        {
            bytes[0x0100] = StopOpcode;
            bytes[0x0101] = 0x00;
            bytes[0x0102] = IncBOpcode;
            bytes[0x0143] = 0xC0;
        });
        var gameBoy = new GameBoy(cartridge, HardwareModel.Cgb);
        gameBoy.Bus.Clock.SetCounter(0xABCC);
        gameBoy.Bus.WriteByte(AddressMap.Key1Register, 0x01);

        gameBoy.Step().Should().Be(2);
        var dividerAfterStop = gameBoy.Bus.ReadByte(AddressMap.DividerRegister);

        gameBoy.Bus.Clock.CgbDoubleSpeed.Should().BeTrue();
        gameBoy.Bus.ReadByte(AddressMap.Key1Register).Should().Be(0xFE);
        gameBoy.Bus.Clock.SpeedSwitchPauseCycles.Should().Be(2050);

        var pauseMachineCycles = 0;
        for (var cycle = 0; cycle < 2050; cycle++)
        {
            pauseMachineCycles += gameBoy.Step();
        }

        pauseMachineCycles.Should().Be(2050);
        gameBoy.Bus.Clock.SpeedSwitchPauseCycles.Should().Be(0);
        gameBoy.Bus.ReadByte(AddressMap.DividerRegister).Should().Be(dividerAfterStop);
        gameBoy.Cpu.Registers.B.Should().Be(0);
        gameBoy.Cpu.Registers.PC.Should().Be(0x0102);

        gameBoy.Step().Should().Be(1);

        gameBoy.Cpu.Registers.B.Should().Be(1);
        gameBoy.Cpu.Registers.PC.Should().Be(0x0103);
    }

    [Fact]
    public void Step_TicksSerial()
    {
        var cartridge = TestRomFactory.LoadCartridge(bytes => bytes[0x0100] = HaltOpcode);
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        byte? transferredByte = null;
        gameBoy.SerialByteTransferred += transferredByteValue =>
            transferredByte = transferredByteValue;
        gameBoy.Bus.WriteByte(AddressMap.SerialTransferDataRegister, 0x41);
        gameBoy.Bus.WriteByte(AddressMap.SerialTransferControlRegister, 0x81);

        for (var step = 0; step < 1024; step++)
        {
            gameBoy.Step();
        }

        gameBoy.Bus.ReadByte(AddressMap.SerialTransferDataRegister).Should().Be(0xFF);
        gameBoy.Bus.ReadByte(AddressMap.SerialTransferControlRegister).Should().Be(0x7F);
        transferredByte.Should().Be(0x41);
    }

    [Fact]
    public void Constructor_AppliesDmgPostBootState()
    {
        var cartridge = TestRomFactory.LoadCartridge();

        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);

        gameBoy.HardwareModel.Should().Be(HardwareModel.Dmg);
        gameBoy.Bus.ReadByte(AddressMap.DividerRegister).Should().Be(0xAB);
        gameBoy.Bus.ReadByte(AddressMap.InterruptFlagRegister).Should().Be(0xE1);
    }

    [Fact]
    public void Constructor_WithEmptyBootRomSlotAppliesPostBootState()
    {
        var cartridge = TestRomFactory.LoadCartridge();

        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg, new BootRomOptions());

        gameBoy.Cpu.Registers.PC.Should().Be(0x0100);
        gameBoy.Bus.ReadByte(AddressMap.DividerRegister).Should().Be(0xAB);
    }

    [Fact]
    public void Constructor_WithDmgBootRomStartsAtResetVector()
    {
        var cartridge = TestRomFactory.LoadCartridge();
        var bootRom = BootRomTestFactory.CreateDmg(bytes => bytes[0x0000] = IncBOpcode);

        var gameBoy = new GameBoy(
            cartridge,
            HardwareModel.Dmg,
            new BootRomOptions { DmgBootRom = bootRom }
        );

        gameBoy.Cpu.Registers.PC.Should().Be(0x0000);
        gameBoy.Bus.ReadByte(0x0000).Should().Be(IncBOpcode);

        gameBoy.Step();

        gameBoy.Cpu.Registers.B.Should().Be(0x01);
        gameBoy.Cpu.Registers.PC.Should().Be(0x0001);
    }

    [Fact]
    public void Constructor_RejectsInvalidSelectedBootRomSize()
    {
        var cartridge = TestRomFactory.LoadCartridge();

        var exception = FluentActions
            .Invoking(() =>
                new GameBoy(
                    cartridge,
                    HardwareModel.Dmg,
                    new BootRomOptions { DmgBootRom = new byte[255] }
                )
            )
            .Should()
            .ThrowExactly<ArgumentException>()
            .Which;

        exception.Message.Should().Contain("Dmg boot ROM must be 256 bytes");
    }

    [Fact]
    public void Step_UnmapsBootRomWhenFf50IsWritten()
    {
        var cartridge = TestRomFactory.LoadCartridge(bytes => bytes[0x0000] = HaltOpcode);
        var bootRom = BootRomTestFactory.CreateDmg(bytes =>
        {
            bytes[0x0000] = LoadAImmediate8Opcode;
            bytes[0x0001] = 0x01;
            bytes[0x0002] = LoadHighMemoryAImmediate8Opcode;
            bytes[0x0003] = 0x50;
            bytes[0x0004] = JumpImmediate16Opcode;
            bytes[0x0005] = 0x00;
            bytes[0x0006] = 0x01;
        });
        var gameBoy = new GameBoy(
            cartridge,
            HardwareModel.Dmg,
            new BootRomOptions { DmgBootRom = bootRom }
        );

        gameBoy.Bus.ReadByte(0x0000).Should().Be(LoadAImmediate8Opcode);

        gameBoy.Step();
        gameBoy.Step();

        gameBoy.Bus.ReadByte(0x0000).Should().Be(HaltOpcode);
    }

    [Fact]
    public void Constructor_CgbHardwareWithDmgCartridgeMapsCgbBootRom()
    {
        var cartridge = TestRomFactory.LoadCartridge(bytes => bytes[0x0100] = HaltOpcode);
        var dmgBootRom = BootRomTestFactory.CreateDmg(bytes => bytes[0x0000] = IncBOpcode);
        var cgbBootRom = BootRomTestFactory.CreateCgb(bytes =>
        {
            bytes[0x0000] = LoadAImmediate8Opcode;
            bytes[0x0100] = StopOpcode;
        });

        var gameBoy = new GameBoy(
            cartridge,
            HardwareModel.Cgb,
            new BootRomOptions { DmgBootRom = dmgBootRom, CgbBootRom = cgbBootRom }
        );

        gameBoy.HardwareModel.Should().Be(HardwareModel.Cgb);
        gameBoy.Bus.ReadByte(0x0000).Should().Be(LoadAImmediate8Opcode);
        gameBoy.Bus.ReadByte(0x0100).Should().Be(HaltOpcode);
        gameBoy.Bus.ReadByte(0x0200).Should().Be(StopOpcode);
    }

    [Fact]
    public void Ctor_CgbHardwareAcceptsMappedCgbBootRomWithGap()
    {
        var cartridge = TestRomFactory.LoadCartridge(bytes => bytes[0x0100] = HaltOpcode);
        var cgbBootRom = new byte[BootRomOptions.CgbBootRomMappedSize];
        cgbBootRom[0x0000] = LoadAImmediate8Opcode;
        cgbBootRom[0x0200] = StopOpcode;

        var gameBoy = new GameBoy(
            cartridge,
            HardwareModel.Cgb,
            new BootRomOptions { CgbBootRom = cgbBootRom }
        );

        gameBoy.Bus.ReadByte(0x0000).Should().Be(LoadAImmediate8Opcode);
        gameBoy.Bus.ReadByte(0x0100).Should().Be(HaltOpcode);
        gameBoy.Bus.ReadByte(0x0200).Should().Be(StopOpcode);
    }

    [Fact]
    public void Constructor_SgbHardwareIgnoresDmgBootRomSlot()
    {
        var cartridge = TestRomFactory.LoadCartridge(
            CreateSgbRom(bytes => bytes[0x0000] = HaltOpcode)
        );
        var dmgBootRom = BootRomTestFactory.CreateDmg(bytes => bytes[0x0000] = IncBOpcode);

        var gameBoy = new GameBoy(
            cartridge,
            HardwareModel.Sgb,
            new BootRomOptions { DmgBootRom = dmgBootRom }
        );

        gameBoy.HardwareModel.Should().Be(HardwareModel.Sgb);
        gameBoy.CpuMachineCyclesPerSecond.Should().Be(GameBoyTiming.SgbCpuHz);
        gameBoy.Cpu.Registers.PC.Should().Be(0x0100);
        gameBoy.Bus.ReadByte(0x0000).Should().Be(HaltOpcode);
    }

    [Fact]
    public void Constructor_SgbHardwareMapsAndRunsSgbBootRomSlot()
    {
        var cartridge = TestRomFactory.LoadCartridge(
            CreateSgbRom(bytes => bytes[0x0000] = HaltOpcode)
        );
        var dmgBootRom = BootRomTestFactory.CreateDmg(bytes => bytes[0x0000] = HaltOpcode);
        var sgbBootRom = BootRomTestFactory.CreateSgb(bytes => bytes[0x0000] = IncBOpcode);

        var gameBoy = new GameBoy(
            cartridge,
            HardwareModel.Sgb,
            new BootRomOptions { DmgBootRom = dmgBootRom, SgbBootRom = sgbBootRom }
        );

        gameBoy.HardwareModel.Should().Be(HardwareModel.Sgb);
        gameBoy.Cpu.Registers.PC.Should().Be(0x0000);
        gameBoy.Bus.ReadByte(0x0000).Should().Be(IncBOpcode);

        gameBoy.Step();

        gameBoy.Cpu.Registers.B.Should().Be(0x01);
        gameBoy.Cpu.Registers.PC.Should().Be(0x0001);
    }

    [Fact]
    public void Joypad_SgbMltReqEnablesPlayerIdReadback()
    {
        var cartridge = TestRomFactory.LoadCartridge(CreateSgbRom());
        var gameBoy = new GameBoy(cartridge, HardwareModel.Sgb);

        WriteSgbPacket(gameBoy, 0x11, [0x01]);

        gameBoy.Bus.WriteByte(AddressMap.JoypadRegister, 0x30);

        gameBoy.Bus.ReadByte(AddressMap.JoypadRegister).Should().Be(0xFF);

        gameBoy.Bus.WriteByte(AddressMap.JoypadRegister, 0x10);
        gameBoy.Bus.WriteByte(AddressMap.JoypadRegister, 0x30);

        gameBoy.Bus.ReadByte(AddressMap.JoypadRegister).Should().Be(0xFE);
    }

    [Fact]
    public void TickPpu_SgbAppliesPaletteToCompletedFrame()
    {
        var cartridge = TestRomFactory.LoadCartridge(CreateSgbRom());
        var gameBoy = new GameBoy(cartridge, HardwareModel.Sgb);
        WriteSgbPacket(
            gameBoy,
            0x00,
            [
                0x34,
                0x12,
                0x22,
                0x22,
                0x33,
                0x33,
                0x44,
                0x44,
                0x55,
                0x55,
                0x66,
                0x66,
                0x77,
                0x77,
                0x00,
            ]
        );

        var frame = gameBoy
            .Bus.TickPpu(456 * 144)
            .CompletedFrame.Should()
            .BeOfType<LcdFrame>()
            .Subject;

        frame.Width.Should().Be(160);
        frame.Height.Should().Be(144);
        frame.Pixels.Length.Should().Be(160 * 144 * 2);
        Rgb555Assertions.PixelEquals(frame, GameBoyPixelIndex(x: 0, y: 0), expected: 0x1234);
    }

    [Fact]
    public void TickPpu_SgbCapturesPaletteTransferFromScreen()
    {
        var cartridge = TestRomFactory.LoadCartridge(CreateSgbRom());
        var gameBoy = new GameBoy(cartridge, HardwareModel.Sgb);
        var transferData = new byte[4096];
        WriteSystemPalette(transferData, paletteId: 9, 0x1234, 0x2345, 0x3456, 0x4567);

        WriteSgbTransferFrame(gameBoy, transferData, tileCount: 0x100);
        WriteSgbPacket(gameBoy, command: 0x0B, []);
        TickSgbTransferFrames(gameBoy);
        WriteSgbPacket(gameBoy, command: 0x0A, CreatePalSetPayload(9, 9, 9, 9));

        var frame = gameBoy
            .Bus.TickPpu(456 * 154)
            .CompletedFrame.Should()
            .BeOfType<LcdFrame>()
            .Subject;

        Rgb555Assertions.PixelEquals(frame, GameBoyPixelIndex(x: 0, y: 0), expected: 0x1234);
    }

    [Fact]
    public void TickPpu_SgbCapturesAttributeTransferFromScreen()
    {
        var cartridge = TestRomFactory.LoadCartridge(CreateSgbRom());
        var gameBoy = new GameBoy(cartridge, HardwareModel.Sgb);
        WriteSgbPacket(gameBoy, command: 0x00, Pal01Payload);
        var transferData = new byte[4096];
        WriteAttributeFile(transferData, fileIndex: 2, packedFirstFourTiles: 0x40);

        WriteSgbTransferFrame(gameBoy, transferData, tileCount: 0xFE);
        WriteSgbPacket(gameBoy, command: 0x15, []);
        TickSgbTransferFrames(gameBoy);
        WriteSgbPacket(gameBoy, command: 0x16, [0x02]);
        WriteFirstBackgroundPixelShade2(gameBoy);

        var frame = gameBoy
            .Bus.TickPpu(456 * 154)
            .CompletedFrame.Should()
            .BeOfType<LcdFrame>()
            .Subject;

        Rgb555Assertions.PixelEquals(frame, GameBoyPixelIndex(x: 0, y: 0), expected: 0x6666);
        Rgb555Assertions.PixelEquals(frame, GameBoyPixelIndex(x: 8, y: 0), expected: 0x3333);
    }

    [Fact]
    public void TickPpu_SgbPalSetCanApplyAttributeFile()
    {
        var cartridge = TestRomFactory.LoadCartridge(CreateSgbRom());
        var gameBoy = new GameBoy(cartridge, HardwareModel.Sgb);
        var paletteTransfer = new byte[4096];
        var attributeTransfer = new byte[4096];
        WriteSystemPalette(paletteTransfer, paletteId: 9, 0x1111, 0x2222, 0x3333, 0x4444);
        WriteSystemPalette(paletteTransfer, paletteId: 10, 0x5555, 0x6666, 0x7777, 0x7FFF);
        WriteAttributeFile(attributeTransfer, fileIndex: 3, packedFirstFourTiles: 0x40);

        WriteSgbTransferFrame(gameBoy, paletteTransfer, tileCount: 0x100);
        WriteSgbPacket(gameBoy, command: 0x0B, []);
        TickSgbTransferFrames(gameBoy);
        WriteSgbTransferFrame(gameBoy, attributeTransfer, tileCount: 0xFE);
        WriteSgbPacket(gameBoy, command: 0x15, []);
        TickSgbTransferFrames(gameBoy);
        WriteSgbPacket(gameBoy, command: 0x0A, CreatePalSetPayload(9, 10, 9, 9, flags: 0x83));
        WriteFirstBackgroundPixelShade2(gameBoy);

        var frame = gameBoy
            .Bus.TickPpu(456 * 154)
            .CompletedFrame.Should()
            .BeOfType<LcdFrame>()
            .Subject;

        Rgb555Assertions.PixelEquals(frame, GameBoyPixelIndex(x: 0, y: 0), expected: 0x7777);
        Rgb555Assertions.PixelEquals(frame, GameBoyPixelIndex(x: 8, y: 0), expected: 0x3333);
    }

    [Fact]
    public void TickPpu_SgbCapturesBorderTransferFromScreen()
    {
        var cartridge = TestRomFactory.LoadCartridge(CreateSgbRom());
        var gameBoy = new GameBoy(cartridge, HardwareModel.Sgb);
        var tileTransfer = new byte[4096];
        var mapTransfer = new byte[4096];
        WriteBorderTilePixel(tileTransfer, tileIndex: 1, color: 5);

        WriteSgbTransferFrame(gameBoy, tileTransfer, tileCount: 0x100);
        WriteSgbPacket(gameBoy, command: 0x13, [0x00]);

        gameBoy.VideoRenderingEnabled = false;

        TickSgbTransferFrames(gameBoy);
        WriteBorderMapEntry(mapTransfer, tileX: 0, tileY: 0, tileIndex: 1, palette: 4);
        WriteBorderPaletteColor(mapTransfer, paletteColor: 5, color: 0x1234);
        WriteSgbTransferFrame(gameBoy, mapTransfer, tileCount: 0x88);
        WriteSgbPacket(gameBoy, command: 0x14, []);
        TickSgbTransferFrames(gameBoy);

        gameBoy.VideoRenderingEnabled = true;

        var frame = gameBoy
            .Bus.TickPpu(456 * 154)
            .CompletedFrame.Should()
            .BeOfType<LcdFrame>()
            .Subject;

        frame.Width.Should().Be(256);
        frame.Height.Should().Be(224);
        Rgb555Assertions.PixelEquals(frame, pixelIndex: 0, expected: 0x1234);
    }

    [Fact]
    public void DrainAudioSamples_ReturnsProducedSamples()
    {
        var cartridge = TestRomFactory.LoadCartridge();
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        var destination = new ApuStereoSample[1];

        gameBoy.Bus.Apu.Tick(88);

        gameBoy.DrainAudioSamples(destination).Should().Be(1);
        destination[0].Should().Be(default(ApuStereoSample));
    }

    [Fact]
    public void DrainAudioSamples_PreservesSamplesThatDoNotFit()
    {
        var cartridge = TestRomFactory.LoadCartridge();
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        var firstDrain = new ApuStereoSample[1];
        var secondDrain = new ApuStereoSample[2];

        gameBoy.Bus.Apu.Tick(264);

        gameBoy.DrainAudioSamples(firstDrain).Should().Be(1);
        gameBoy.DrainAudioSamples(secondDrain).Should().Be(2);
    }

    [Fact]
    public void DrainAudioSamples_ReturnsZeroWhenEmpty()
    {
        var cartridge = TestRomFactory.LoadCartridge();
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        Span<ApuStereoSample> destination = stackalloc ApuStereoSample[1];

        gameBoy.DrainAudioSamples(destination).Should().Be(0);
    }

    [Fact]
    public void SetButtonState_UpdatesJoypadInputState()
    {
        var cartridge = TestRomFactory.LoadCartridge();
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        gameBoy.Bus.WriteByte(AddressMap.JoypadRegister, 0x10);

        gameBoy.SetButtonState(JoypadButton.A, pressed: true);

        gameBoy.Bus.ReadByte(AddressMap.JoypadRegister).Should().Be(0xDE);
    }

    [Fact]
    public void Step_RaisesFrameCompletedAfterCpuInstruction()
    {
        var cartridge = TestRomFactory.LoadCartridge(bytes =>
        {
            bytes[0x0100] = JumpImmediate16Opcode;
            bytes[0x0101] = 0x00;
            bytes[0x0102] = 0x01;
        });
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        var completedFrames = new List<LcdFrame>();
        gameBoy.FrameCompleted += completedFrames.Add;

        for (var step = 0; completedFrames.Count == 0 && step < 20_000; step++)
        {
            gameBoy.Step();
        }

        using var completedFrame = completedFrames.Should().ContainSingle().Which;
        completedFrame.Width.Should().Be(160);
        completedFrame.Height.Should().Be(144);
        completedFrame.PixelFormat.Should().Be(LcdPixelFormat.DmgShadeIndex8);
    }

    [Fact]
    public void Step_GivesFrameSubscribersIndependentOwnership()
    {
        var cartridge = TestRomFactory.LoadCartridge(bytes =>
        {
            bytes[0x0100] = JumpImmediate16Opcode;
            bytes[0x0101] = 0x00;
            bytes[0x0102] = 0x01;
        });
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        var secondSubscriberPixelCounts = new List<int>();
        gameBoy.FrameCompleted += frame => frame.Dispose();
        gameBoy.FrameCompleted += frame =>
        {
            using (frame)
            {
                secondSubscriberPixelCounts.Add(frame.Pixels.Length);
            }
        };

        for (var step = 0; secondSubscriberPixelCounts.Count == 0 && step < 20_000; step++)
        {
            gameBoy.Step();
        }

        secondSubscriberPixelCounts.Should().ContainSingle().Which.Should().Be(160 * 144);
    }

    [Fact]
    public void Step_RendersBootRomFrameEvenWhenHostFrameSkippingIsEnabled()
    {
        var cartridge = TestRomFactory.LoadCartridge();
        var bootRom = BootRomTestFactory.CreateDmg(bytes =>
        {
            bytes[0x0000] = LoadAImmediate8Opcode;
            bytes[0x0001] = LcdControlEnabled;
            bytes[0x0002] = LoadHighMemoryAImmediate8Opcode;
            bytes[0x0003] = 0x40;
            bytes[0x0004] = JumpImmediate16Opcode;
            bytes[0x0005] = 0x04;
            bytes[0x0006] = 0x00;
        });
        var gameBoy = new GameBoy(
            cartridge,
            HardwareModel.Dmg,
            new BootRomOptions { DmgBootRom = bootRom }
        )
        {
            VideoRenderingEnabled = false,
        };
        var completedFrames = new List<LcdFrame>();
        gameBoy.FrameCompleted += completedFrames.Add;

        for (var step = 0; completedFrames.Count == 0 && step < 20_000; step++)
        {
            gameBoy.Step();
        }

        using var completedFrame = completedFrames.Should().ContainSingle().Which;
    }

    [Fact]
    public void Step_ClearsBootRomMappedStateWhenFf50IsWritten()
    {
        var cartridge = TestRomFactory.LoadCartridge(bytes => bytes[0x0000] = HaltOpcode);
        var bootRom = BootRomTestFactory.CreateDmg(bytes =>
        {
            bytes[0x0000] = LoadAImmediate8Opcode;
            bytes[0x0001] = 0x01;
            bytes[0x0002] = LoadHighMemoryAImmediate8Opcode;
            bytes[0x0003] = 0x50;
        });
        var gameBoy = new GameBoy(
            cartridge,
            HardwareModel.Dmg,
            new BootRomOptions { DmgBootRom = bootRom }
        );

        gameBoy.IsBootRomMapped.Should().BeTrue();

        gameBoy.Step();
        gameBoy.Step();

        gameBoy.IsBootRomMapped.Should().BeFalse();
    }

    private static byte[] CreateSgbRom(Action<byte[]>? configure = null) =>
        TestRomFactory.Create(bytes =>
        {
            bytes[0x0146] = 0x03;
            bytes[0x014B] = 0x33;
            configure?.Invoke(bytes);
        });

    private static void WriteSgbPacket(GameBoy gameBoy, byte command, ReadOnlySpan<byte> payload)
    {
        var packet = CreatePacket(command, payload);

        WriteSgbStartPulse(gameBoy);
        foreach (var value in packet)
        {
            for (var bit = 0; bit < 8; bit++)
            {
                WriteSgbBit(gameBoy, (value & (1 << bit)) != 0);
            }
        }

        WriteSgbBit(gameBoy, value: false);
    }

    private static void WriteSgbStartPulse(GameBoy gameBoy)
    {
        gameBoy.Bus.WriteByte(AddressMap.JoypadRegister, 0x00);
        gameBoy.Bus.WriteByte(AddressMap.JoypadRegister, 0x30);
    }

    private static void WriteSgbBit(GameBoy gameBoy, bool value)
    {
        gameBoy.Bus.WriteByte(AddressMap.JoypadRegister, 0x30);
        gameBoy.Bus.WriteByte(AddressMap.JoypadRegister, value ? (byte)0x10 : (byte)0x20);
    }

    private static void WriteSgbTransferFrame(
        GameBoy gameBoy,
        ReadOnlySpan<byte> transferData,
        int tileCount
    )
    {
        gameBoy.Bus.Ppu.WriteRegister(AddressMap.BackgroundPaletteRegister, 0xE4);
        for (var tile = 0; tile < tileCount; tile++)
        {
            var tileDataAddress = AddressMap.VideoRamStart + (tile * 16);
            for (var offset = 0; offset < 16; offset++)
            {
                gameBoy.Bus.Ppu.VideoRam.Write(
                    (ushort)(tileDataAddress + offset),
                    transferData[(tile * 16) + offset]
                );
            }

            gameBoy.Bus.Ppu.VideoRam.Write(
                (ushort)(0x9800 + (tile / 20 * 32) + (tile % 20)),
                (byte)tile
            );
        }
    }

    private static void TickSgbTransferFrames(GameBoy gameBoy)
    {
        gameBoy.Bus.TickPpu(456 * 154);
        gameBoy.Bus.TickPpu(456 * 154);
        gameBoy.Bus.TickPpu(456 * 154);
    }

    private static void WriteFirstBackgroundPixelShade2(GameBoy gameBoy)
    {
        gameBoy.Bus.Ppu.WriteRegister(AddressMap.BackgroundPaletteRegister, 0xE4);
        gameBoy.Bus.Ppu.VideoRam.Write(0x8000, 0x00);
        gameBoy.Bus.Ppu.VideoRam.Write(0x8001, 0x80);
        gameBoy.Bus.Ppu.VideoRam.Write(0x9800, 0x00);
        gameBoy.Bus.Ppu.VideoRam.Write(0x9801, 0x00);
    }
}
