// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core;
using GbcNet.Core.Apu;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Hardware;
using GbcNet.Core.Joypad;
using GbcNet.Core.Memory;
using GbcNet.Core.Ppu;
using GbcNet.Tests.Cartridges;

namespace GbcNet.Tests;

public sealed class GameBoyTests
{
    private const byte HaltOpcode = 0x76;
    private const byte StopOpcode = 0x10;
    private const byte IncBOpcode = 0x04;
    private const byte JumpImmediate16Opcode = 0xC3;
    private const byte LoadAImmediate8Opcode = 0x3E;
    private const byte LoadHighMemoryAImmediate8Opcode = 0xE0;

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

        Assert.Equal(1, machineCycles);
        Assert.Equal(0x01, gameBoy.Bus.ReadByte(AddressMap.TimerCounterRegister));
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

        Assert.Equal(2, gameBoy.Step());
        Assert.Equal(0, gameBoy.Step());
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

        Assert.Equal(GameBoyTiming.NormalCpuHz, gameBoy.CpuMachineCyclesPerSecond);

        gameBoy.Bus.WriteByte(AddressMap.Key1Register, 0x01);
        gameBoy.Step();

        Assert.Equal(GameBoyTiming.DoubleCpuHz, gameBoy.CpuMachineCyclesPerSecond);
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

        Assert.Equal(2, gameBoy.Step());
        var dividerAfterStop = gameBoy.Bus.ReadByte(AddressMap.DividerRegister);

        Assert.True(gameBoy.Bus.Clock.CgbDoubleSpeed);
        Assert.Equal(0xFE, gameBoy.Bus.ReadByte(AddressMap.Key1Register));
        Assert.Equal(2050, gameBoy.Bus.Clock.SpeedSwitchPauseCycles);

        var pauseMachineCycles = 0;
        for (var cycle = 0; cycle < 2050; cycle++)
        {
            pauseMachineCycles += gameBoy.Step();
        }

        Assert.Equal(2050, pauseMachineCycles);
        Assert.Equal(0, gameBoy.Bus.Clock.SpeedSwitchPauseCycles);
        Assert.Equal(dividerAfterStop, gameBoy.Bus.ReadByte(AddressMap.DividerRegister));
        Assert.Equal(0, gameBoy.Cpu.Registers.B);
        Assert.Equal(0x0102, gameBoy.Cpu.Registers.PC);

        Assert.Equal(1, gameBoy.Step());

        Assert.Equal(1, gameBoy.Cpu.Registers.B);
        Assert.Equal(0x0103, gameBoy.Cpu.Registers.PC);
    }

    [Fact]
    public void Step_TicksSerial()
    {
        var cartridge = TestRomFactory.LoadCartridge(bytes => bytes[0x0100] = HaltOpcode);
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        byte? transferredByte = null;
        gameBoy.SerialByteTransferred += (_, e) => transferredByte = e.TransferredByte;
        gameBoy.Bus.WriteByte(AddressMap.SerialTransferDataRegister, 0x41);
        gameBoy.Bus.WriteByte(AddressMap.SerialTransferControlRegister, 0x81);

        for (var step = 0; step < 1024; step++)
        {
            gameBoy.Step();
        }

        Assert.Equal(0xFF, gameBoy.Bus.ReadByte(AddressMap.SerialTransferDataRegister));
        Assert.Equal(0x7F, gameBoy.Bus.ReadByte(AddressMap.SerialTransferControlRegister));
        Assert.Equal((byte)0x41, transferredByte);
    }

    [Fact]
    public void Constructor_AppliesDmgPostBootState()
    {
        var cartridge = TestRomFactory.LoadCartridge();

        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);

        Assert.Equal(HardwareModel.Dmg, gameBoy.HardwareModel);
        Assert.Equal(0xAB, gameBoy.Bus.ReadByte(AddressMap.DividerRegister));
        Assert.Equal(0xE1, gameBoy.Bus.ReadByte(AddressMap.InterruptFlagRegister));
    }

    [Fact]
    public void Constructor_WithEmptyBootRomSlotAppliesPostBootState()
    {
        var cartridge = TestRomFactory.LoadCartridge();

        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg, new BootRomOptions());

        Assert.Equal(0x0100, gameBoy.Cpu.Registers.PC);
        Assert.Equal(0xAB, gameBoy.Bus.ReadByte(AddressMap.DividerRegister));
    }

    [Fact]
    public void Constructor_WithDmgBootRomStartsAtResetVector()
    {
        var cartridge = TestRomFactory.LoadCartridge();
        var bootRom = CreateDmgBootRom(bytes => bytes[0x0000] = IncBOpcode);

        var gameBoy = new GameBoy(
            cartridge,
            HardwareModel.Dmg,
            new BootRomOptions { DmgBootRom = bootRom }
        );

        Assert.Equal(0x0000, gameBoy.Cpu.Registers.PC);
        Assert.Equal(IncBOpcode, gameBoy.Bus.ReadByte(0x0000));

        gameBoy.Step();

        Assert.Equal(0x01, gameBoy.Cpu.Registers.B);
        Assert.Equal(0x0001, gameBoy.Cpu.Registers.PC);
    }

    [Fact]
    public void Constructor_RejectsInvalidSelectedBootRomSize()
    {
        var cartridge = TestRomFactory.LoadCartridge();

        var exception = Assert.Throws<ArgumentException>(() =>
            new GameBoy(
                cartridge,
                HardwareModel.Dmg,
                new BootRomOptions { DmgBootRom = new byte[255] }
            )
        );

        Assert.Contains(
            "Dmg boot ROM must be 256 bytes",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Step_UnmapsBootRomWhenFf50IsWritten()
    {
        var cartridge = TestRomFactory.LoadCartridge(bytes => bytes[0x0000] = HaltOpcode);
        var bootRom = CreateDmgBootRom(bytes =>
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

        Assert.Equal(LoadAImmediate8Opcode, gameBoy.Bus.ReadByte(0x0000));

        gameBoy.Step();
        gameBoy.Step();

        Assert.Equal(HaltOpcode, gameBoy.Bus.ReadByte(0x0000));
    }

    [Fact]
    public void Constructor_CgbHardwareWithDmgCartridgeMapsCgbBootRom()
    {
        var cartridge = TestRomFactory.LoadCartridge(bytes => bytes[0x0100] = HaltOpcode);
        var dmgBootRom = CreateDmgBootRom(bytes => bytes[0x0000] = IncBOpcode);
        var cgbBootRom = CreateCgbBootRom(bytes =>
        {
            bytes[0x0000] = LoadAImmediate8Opcode;
            bytes[0x0100] = StopOpcode;
        });

        var gameBoy = new GameBoy(
            cartridge,
            HardwareModel.Cgb,
            new BootRomOptions { DmgBootRom = dmgBootRom, CgbBootRom = cgbBootRom }
        );

        Assert.Equal(HardwareModel.Cgb, gameBoy.HardwareModel);
        Assert.Equal(LoadAImmediate8Opcode, gameBoy.Bus.ReadByte(0x0000));
        Assert.Equal(HaltOpcode, gameBoy.Bus.ReadByte(0x0100));
        Assert.Equal(StopOpcode, gameBoy.Bus.ReadByte(0x0200));
    }

    [Fact]
    public void Constructor_SgbHardwareIgnoresDmgBootRomSlot()
    {
        var cartridge = TestRomFactory.LoadCartridge(
            CreateSgbRom(bytes => bytes[0x0000] = HaltOpcode)
        );
        var dmgBootRom = CreateDmgBootRom(bytes => bytes[0x0000] = IncBOpcode);

        var gameBoy = new GameBoy(
            cartridge,
            HardwareModel.Sgb,
            new BootRomOptions { DmgBootRom = dmgBootRom }
        );

        Assert.Equal(HardwareModel.Sgb, gameBoy.HardwareModel);
        Assert.Equal(GameBoyTiming.SgbCpuHz, gameBoy.CpuMachineCyclesPerSecond);
        Assert.Equal(0x0100, gameBoy.Cpu.Registers.PC);
        Assert.Equal(HaltOpcode, gameBoy.Bus.ReadByte(0x0000));
    }

    [Fact]
    public void Constructor_SgbHardwareMapsSgbBootRomSlot()
    {
        var cartridge = TestRomFactory.LoadCartridge(
            CreateSgbRom(bytes => bytes[0x0000] = HaltOpcode)
        );
        var dmgBootRom = CreateDmgBootRom(bytes => bytes[0x0000] = HaltOpcode);
        var sgbBootRom = CreateSgbBootRom(bytes => bytes[0x0000] = IncBOpcode);

        var gameBoy = new GameBoy(
            cartridge,
            HardwareModel.Sgb,
            new BootRomOptions { DmgBootRom = dmgBootRom, SgbBootRom = sgbBootRom }
        );

        Assert.Equal(HardwareModel.Sgb, gameBoy.HardwareModel);
        Assert.Equal(IncBOpcode, gameBoy.Bus.ReadByte(0x0000));
    }

    [Fact]
    public void Joypad_SgbMltReqEnablesPlayerIdReadback()
    {
        var cartridge = TestRomFactory.LoadCartridge(CreateSgbRom());
        var gameBoy = new GameBoy(cartridge, HardwareModel.Sgb);

        WriteSgbPacket(gameBoy, 0x11, [0x01]);

        gameBoy.Bus.WriteByte(AddressMap.JoypadRegister, 0x30);

        Assert.Equal(0xFF, gameBoy.Bus.ReadByte(AddressMap.JoypadRegister));

        gameBoy.Bus.WriteByte(AddressMap.JoypadRegister, 0x10);
        gameBoy.Bus.WriteByte(AddressMap.JoypadRegister, 0x30);

        Assert.Equal(0xFE, gameBoy.Bus.ReadByte(AddressMap.JoypadRegister));
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

        var frame = Assert.IsType<LcdFrame>(gameBoy.Bus.TickPpu(456 * 144).CompletedFrame);

        Assert.Equal(LcdPixelFormat.Rgb555Le, frame.PixelFormat);
        Assert.Equal(160, frame.Width);
        Assert.Equal(144, frame.Height);
        Assert.Equal(160 * 144 * 2, frame.Pixels.Length);
        AssertRgb555Pixel(frame, GameBoyPixelIndex(x: 0, y: 0), expected: 0x1234);
    }

    [Fact]
    public void TickPpu_SgbCapturesPaletteTransferFromScreen()
    {
        var cartridge = TestRomFactory.LoadCartridge(CreateSgbRom());
        var gameBoy = new GameBoy(cartridge, HardwareModel.Sgb);
        var transferData = new byte[4096];
        WriteSgbPaletteTransfer(transferData, paletteId: 9, 0x1234, 0x2345, 0x3456, 0x4567);

        WriteSgbTransferFrame(gameBoy, transferData, tileCount: 0x100);
        WriteSgbPacket(gameBoy, command: 0x0B, []);
        TickSgbTransferFrames(gameBoy);
        WriteSgbPacket(gameBoy, command: 0x0A, CreateSgbPalSetPayload(9, 9, 9, 9));

        var frame = Assert.IsType<LcdFrame>(gameBoy.Bus.TickPpu(456 * 154).CompletedFrame);

        Assert.Equal(LcdPixelFormat.Rgb555Le, frame.PixelFormat);
        AssertRgb555Pixel(frame, GameBoyPixelIndex(x: 0, y: 0), expected: 0x1234);
    }

    [Fact]
    public void TickPpu_SgbCapturesAttributeTransferFromScreen()
    {
        var cartridge = TestRomFactory.LoadCartridge(CreateSgbRom());
        var gameBoy = new GameBoy(cartridge, HardwareModel.Sgb);
        WriteSgbPacket(
            gameBoy,
            command: 0x00,
            [
                0x11,
                0x11,
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
        var transferData = new byte[4096];
        WriteSgbAttributeTransfer(transferData, fileIndex: 2, packedFirstFourTiles: 0x40);

        WriteSgbTransferFrame(gameBoy, transferData, tileCount: 0xFE);
        WriteSgbPacket(gameBoy, command: 0x15, []);
        TickSgbTransferFrames(gameBoy);
        WriteSgbPacket(gameBoy, command: 0x16, [0x02]);
        WriteFirstBackgroundPixelShade2(gameBoy);

        var frame = Assert.IsType<LcdFrame>(gameBoy.Bus.TickPpu(456 * 154).CompletedFrame);

        Assert.Equal(LcdPixelFormat.Rgb555Le, frame.PixelFormat);
        AssertRgb555Pixel(frame, GameBoyPixelIndex(x: 0, y: 0), expected: 0x6666);
        AssertRgb555Pixel(frame, GameBoyPixelIndex(x: 8, y: 0), expected: 0x3333);
    }

    [Fact]
    public void TickPpu_SgbPalSetCanApplyAttributeFile()
    {
        var cartridge = TestRomFactory.LoadCartridge(CreateSgbRom());
        var gameBoy = new GameBoy(cartridge, HardwareModel.Sgb);
        var paletteTransfer = new byte[4096];
        var attributeTransfer = new byte[4096];
        WriteSgbPaletteTransfer(paletteTransfer, paletteId: 9, 0x1111, 0x2222, 0x3333, 0x4444);
        WriteSgbPaletteTransfer(paletteTransfer, paletteId: 10, 0x5555, 0x6666, 0x7777, 0x7FFF);
        WriteSgbAttributeTransfer(attributeTransfer, fileIndex: 3, packedFirstFourTiles: 0x40);

        WriteSgbTransferFrame(gameBoy, paletteTransfer, tileCount: 0x100);
        WriteSgbPacket(gameBoy, command: 0x0B, []);
        TickSgbTransferFrames(gameBoy);
        WriteSgbTransferFrame(gameBoy, attributeTransfer, tileCount: 0xFE);
        WriteSgbPacket(gameBoy, command: 0x15, []);
        TickSgbTransferFrames(gameBoy);
        WriteSgbPacket(gameBoy, command: 0x0A, CreateSgbPalSetPayload(9, 10, 9, 9, flags: 0x83));
        WriteFirstBackgroundPixelShade2(gameBoy);

        var frame = Assert.IsType<LcdFrame>(gameBoy.Bus.TickPpu(456 * 154).CompletedFrame);

        AssertRgb555Pixel(frame, GameBoyPixelIndex(x: 0, y: 0), expected: 0x7777);
        AssertRgb555Pixel(frame, GameBoyPixelIndex(x: 8, y: 0), expected: 0x3333);
    }

    [Fact]
    public void TickPpu_SgbCapturesBorderTransferFromScreen()
    {
        var cartridge = TestRomFactory.LoadCartridge(CreateSgbRom());
        var gameBoy = new GameBoy(cartridge, HardwareModel.Sgb);
        var tileTransfer = new byte[4096];
        var mapTransfer = new byte[4096];
        WriteSgbBorderTilePixel(tileTransfer, tileIndex: 1, color: 5);

        WriteSgbTransferFrame(gameBoy, tileTransfer, tileCount: 0x100);
        WriteSgbPacket(gameBoy, command: 0x13, [0x00]);

        gameBoy.VideoRenderingEnabled = false;

        TickSgbTransferFrames(gameBoy);
        WriteSgbBorderMapEntry(mapTransfer, tileX: 0, tileY: 0, tileIndex: 1, palette: 4);
        WriteSgbBorderPaletteColor(mapTransfer, paletteColor: 5, color: 0x1234);
        WriteSgbTransferFrame(gameBoy, mapTransfer, tileCount: 0x88);
        WriteSgbPacket(gameBoy, command: 0x14, []);
        TickSgbTransferFrames(gameBoy);

        gameBoy.VideoRenderingEnabled = true;

        var frame = Assert.IsType<LcdFrame>(gameBoy.Bus.TickPpu(456 * 154).CompletedFrame);

        Assert.Equal(256, frame.Width);
        Assert.Equal(224, frame.Height);
        AssertRgb555Pixel(frame, pixelIndex: 0, expected: 0x1234);
    }

    [Fact]
    public void DrainAudioSamples_ReturnsProducedSamples()
    {
        var cartridge = TestRomFactory.LoadCartridge();
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        var destination = new ApuStereoSample[1];

        gameBoy.Bus.Apu.Tick(88);

        Assert.Equal(1, gameBoy.DrainAudioSamples(destination));
        Assert.Equal(default, destination[0]);
    }

    [Fact]
    public void DrainAudioSamples_PreservesSamplesThatDoNotFit()
    {
        var cartridge = TestRomFactory.LoadCartridge();
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        var firstDrain = new ApuStereoSample[1];
        var secondDrain = new ApuStereoSample[2];

        gameBoy.Bus.Apu.Tick(264);

        Assert.Equal(1, gameBoy.DrainAudioSamples(firstDrain));
        Assert.Equal(2, gameBoy.DrainAudioSamples(secondDrain));
    }

    [Fact]
    public void DrainAudioSamples_ReturnsZeroWhenEmpty()
    {
        var cartridge = TestRomFactory.LoadCartridge();
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        Span<ApuStereoSample> destination = stackalloc ApuStereoSample[1];

        Assert.Equal(0, gameBoy.DrainAudioSamples(destination));
    }

    [Fact]
    public void SetButtonState_UpdatesJoypadInputState()
    {
        var cartridge = TestRomFactory.LoadCartridge();
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        gameBoy.Bus.WriteByte(AddressMap.JoypadRegister, 0x10);

        gameBoy.SetButtonState(JoypadButton.A, pressed: true);

        Assert.Equal(0xDE, gameBoy.Bus.ReadByte(AddressMap.JoypadRegister));
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
        gameBoy.FrameCompleted += (_, e) => completedFrames.Add(e.Frame);

        for (var step = 0; completedFrames.Count == 0 && step < 20_000; step++)
        {
            gameBoy.Step();
        }

        var completedFrame = Assert.Single(completedFrames);
        Assert.Equal(160, completedFrame.Width);
        Assert.Equal(144, completedFrame.Height);
        Assert.Equal(LcdPixelFormat.DmgShadeIndex8, completedFrame.PixelFormat);
    }

    private static byte[] CreateDmgBootRom(Action<byte[]> configure)
    {
        var bootRom = new byte[BootRomOptions.DmgBootRomSize];
        configure(bootRom);
        return bootRom;
    }

    private static byte[] CreateCgbBootRom(Action<byte[]> configure)
    {
        var bootRom = new byte[BootRomOptions.CgbBootRomSize];
        configure(bootRom);
        return bootRom;
    }

    private static byte[] CreateSgbBootRom(Action<byte[]> configure)
    {
        var bootRom = new byte[BootRomOptions.SgbBootRomSize];
        configure(bootRom);
        return bootRom;
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
        Span<byte> packet = stackalloc byte[16];
        packet[0] = (byte)((command << 3) | 0x01);
        payload.CopyTo(packet[1..]);

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

    private static byte[] CreateSgbPalSetPayload(
        ushort palette0,
        ushort palette1,
        ushort palette2,
        ushort palette3,
        byte flags = 0
    )
    {
        var payload = new byte[15];
        WriteUInt16(payload, offset: 0, palette0);
        WriteUInt16(payload, offset: 2, palette1);
        WriteUInt16(payload, offset: 4, palette2);
        WriteUInt16(payload, offset: 6, palette3);
        payload[8] = flags;
        return payload;
    }

    private static void WriteSgbPaletteTransfer(
        byte[] transferData,
        int paletteId,
        ushort color0,
        ushort color1,
        ushort color2,
        ushort color3
    )
    {
        var offset = paletteId * 8;
        WriteUInt16(transferData, offset, color0);
        WriteUInt16(transferData, offset + 2, color1);
        WriteUInt16(transferData, offset + 4, color2);
        WriteUInt16(transferData, offset + 6, color3);
    }

    private static void WriteSgbAttributeTransfer(
        byte[] transferData,
        int fileIndex,
        byte packedFirstFourTiles
    )
    {
        transferData[fileIndex * 90] = packedFirstFourTiles;
    }

    private static void WriteSgbBorderTilePixel(byte[] transferData, int tileIndex, byte color)
    {
        var offset = tileIndex * 32;
        if ((color & 0x01) != 0)
        {
            transferData[offset] = 0x80;
        }

        if ((color & 0x02) != 0)
        {
            transferData[offset + 1] = 0x80;
        }

        if ((color & 0x04) != 0)
        {
            transferData[offset + 16] = 0x80;
        }

        if ((color & 0x08) != 0)
        {
            transferData[offset + 17] = 0x80;
        }
    }

    private static void WriteSgbBorderMapEntry(
        byte[] transferData,
        int tileX,
        int tileY,
        int tileIndex,
        int palette
    )
    {
        WriteUInt16(
            transferData,
            ((tileY * 32) + tileX) * 2,
            (ushort)((palette << 10) | tileIndex)
        );
    }

    private static void WriteSgbBorderPaletteColor(
        byte[] transferData,
        int paletteColor,
        ushort color
    )
    {
        WriteUInt16(transferData, 0x800 + (paletteColor * 2), color);
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

    private static void WriteUInt16(byte[] bytes, int offset, ushort value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
    }

    private static void AssertRgb555Pixel(LcdFrame frame, int pixelIndex, ushort expected)
    {
        var pixels = frame.Pixels.Span;
        var offset = pixelIndex * 2;
        var actual = (ushort)(pixels[offset] | (pixels[offset + 1] << 8));
        Assert.Equal(expected, actual);
    }

    private static int GameBoyPixelIndex(int x, int y) => (y * 160) + x;
}
