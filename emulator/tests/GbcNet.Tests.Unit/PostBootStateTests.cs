// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cartridges;
using GbcNet.Core.Hardware;
using GbcNet.Core.Hardware.Profiles;
using GbcNet.Core.Memory;
using GbcNet.Core.Ppu;
using GbcNet.Core.Sm83;

namespace GbcNet.Tests.Unit;

public sealed class PostBootStateTests
{
    private const ushort AudioMasterControlRegister = 0xFF26;

    [Fact]
    public void Apply_SetsDmgCpuRegistersAfterBootHandoff()
    {
        var cartridge = LoadCartridge(TestRomFactory.Create());
        var (cpu, bus) = CreateHardware(cartridge);

        DmgHardwareProfile.Instance.ApplyPostBootState(cartridge, cpu, bus);

        cpu.Registers.A.Should().Be(0x01);
        cpu.Registers.F.Should().Be(0xB0);
        cpu.Registers.BC.Should().Be(0x0013);
        cpu.Registers.DE.Should().Be(0x00D8);
        cpu.Registers.HL.Should().Be(0x014D);
        cpu.Registers.PC.Should().Be(0x0100);
        cpu.Registers.SP.Should().Be(0xFFFE);
    }

    [Fact]
    public void Apply_ClearsDmgHalfCarryAndCarryWhenHeaderChecksumIsZero()
    {
        var cartridge = LoadCartridge(CreateRomWithZeroHeaderChecksum());
        var (cpu, bus) = CreateHardware(cartridge);

        DmgHardwareProfile.Instance.ApplyPostBootState(cartridge, cpu, bus);

        cpu.Registers.F.Should().Be(0x80);
    }

    [Fact]
    public void Apply_SetsSgbCpuRegistersAfterBootHandoff()
    {
        var cartridge = LoadCartridge(CreateSgbRom());
        var profile = SgbHardwareProfile.Instance;
        var bus = new MemoryBus(cartridge, profile);
        var cpu = new Cpu(bus);

        profile.ApplyPostBootState(cartridge, cpu, bus);

        cpu.Registers.A.Should().Be(0x01);
        cpu.Registers.BC.Should().Be(0x0014);
        cpu.Registers.F.Should().Be(0x00);
        cpu.Registers.DE.Should().Be(0x0000);
        cpu.Registers.HL.Should().Be(0xC060);
        cpu.Registers.PC.Should().Be(0x0100);
        cpu.Registers.SP.Should().Be(0xFFFE);
    }

    [Fact]
    public void Apply_SetsSgbIoRegistersAfterBootHandoff()
    {
        var cartridge = LoadCartridge(CreateSgbRom());
        var profile = SgbHardwareProfile.Instance;
        var bus = new MemoryBus(cartridge, profile);
        var cpu = new Cpu(bus);

        profile.ApplyPostBootState(cartridge, cpu, bus);

        bus.ReadByte(AddressMap.JoypadRegister).Should().Be(0xFF);
        bus.ReadByte(AudioMasterControlRegister).Should().Be(0xF0);
    }

    [Fact]
    public void Apply_SetsDmgIoRegistersAfterBootHandoff()
    {
        var cartridge = LoadCartridge(TestRomFactory.Create());
        var (cpu, bus) = CreateHardware(cartridge);

        DmgHardwareProfile.Instance.ApplyPostBootState(cartridge, cpu, bus);

        bus.ReadByte(AddressMap.JoypadRegister).Should().Be(0xCF);
        bus.ReadByte(AddressMap.SerialTransferDataRegister).Should().Be(0x00);
        bus.ReadByte(AddressMap.SerialTransferControlRegister).Should().Be(0x7E);
        bus.ReadByte(AddressMap.DividerRegister).Should().Be(0xAB);
        bus.ReadByte(AddressMap.TimerCounterRegister).Should().Be(0x00);
        bus.ReadByte(AddressMap.TimerModuloRegister).Should().Be(0x00);
        bus.ReadByte(AddressMap.TimerControlRegister).Should().Be(0xF8);
        bus.ReadByte(AddressMap.InterruptFlagRegister).Should().Be(0xE1);
        bus.ReadByte(AddressMap.LcdControlRegister).Should().Be(0x91);
        bus.ReadByte(AddressMap.LcdStatusRegister).Should().Be(0x85);
        bus.ReadByte(AddressMap.ScrollYRegister).Should().Be(0x00);
        bus.ReadByte(AddressMap.ScrollXRegister).Should().Be(0x00);
        bus.ReadByte(AddressMap.LcdYCoordinateRegister).Should().Be(0x00);
        bus.ReadByte(AddressMap.LcdYCompareRegister).Should().Be(0x00);
        bus.ReadByte(AddressMap.DmaRegister).Should().Be(0xFF);
        bus.ReadByte(AddressMap.BackgroundPaletteRegister).Should().Be(0xFC);
        bus.ReadByte(AddressMap.WindowYRegister).Should().Be(0x00);
        bus.ReadByte(AddressMap.WindowXRegister).Should().Be(0x00);
        bus.ReadByte(AddressMap.InterruptEnableRegister).Should().Be(0x00);
    }

    [Fact]
    public void Apply_SetsCgbModeCpuRegistersAfterBootHandoff()
    {
        var cartridge = LoadCartridge(
            TestRomFactory.Create(rom => rom[0x0143] = (byte)CgbSupport.Required)
        );
        var profile = new CgbHardwareProfile(CgbOperatingMode.Cgb);
        var bus = new MemoryBus(cartridge, profile);
        var cpu = new Cpu(bus);

        profile.ApplyPostBootState(cartridge, cpu, bus);

        cpu.Registers.A.Should().Be(0x11);
        cpu.Registers.F.Should().Be(0x80);
        cpu.Registers.BC.Should().Be(0x0000);
        cpu.Registers.DE.Should().Be(0xFF56);
        cpu.Registers.HL.Should().Be(0x000D);
        cpu.Registers.PC.Should().Be(0x0100);
        cpu.Registers.SP.Should().Be(0xFFFE);
    }

    [Fact]
    public void Apply_SetsCgbDmgCompatibilityCpuRegistersAfterBootHandoff()
    {
        var cartridge = LoadCartridge(TestRomFactory.Create());
        var profile = new CgbHardwareProfile(CgbOperatingMode.DmgCompatibility);
        var bus = new MemoryBus(cartridge, profile);
        var cpu = new Cpu(bus);

        profile.ApplyPostBootState(cartridge, cpu, bus);

        cpu.Registers.A.Should().Be(0x11);
        cpu.Registers.F.Should().Be(0x80);
        cpu.Registers.BC.Should().Be(0x0000);
        cpu.Registers.DE.Should().Be(0x0008);
        cpu.Registers.HL.Should().Be(0x007C);
        cpu.Registers.PC.Should().Be(0x0100);
        cpu.Registers.SP.Should().Be(0xFFFE);
    }

    [Fact]
    public void Apply_SeedsCgbDmgCompatibilityLogoTilemapForMatchingPaletteTitle()
    {
        var cartridge = LoadCartridge(
            TestRomFactory.Create(bytes =>
            {
                bytes.AsSpan(0x0134, 16).Clear();
                bytes[0x0134] = (byte)'X';
                bytes[0x014B] = 0x01;
            })
        );
        var profile = new CgbHardwareProfile(CgbOperatingMode.DmgCompatibility);
        var bus = new MemoryBus(cartridge, profile);
        var cpu = new Cpu(bus);

        profile.ApplyPostBootState(cartridge, cpu, bus);

        cpu.Registers.BC.Should().Be(0x5800);
        cpu.Registers.HL.Should().Be(0x991A);
        bus.Ppu.VideoRam.ReadBank(0, 0x9904).Should().Be(0x01);
        bus.Ppu.VideoRam.ReadBank(0, 0x990F).Should().Be(0x0C);
        bus.Ppu.VideoRam.ReadBank(0, 0x9910).Should().Be(0x19);
        bus.Ppu.VideoRam.ReadBank(0, 0x9924).Should().Be(0x0D);
        bus.Ppu.VideoRam.ReadBank(0, 0x992F).Should().Be(0x18);
    }

    [Fact]
    public void Apply_DoesNotSeedCgbDmgCompatibilityLogoTilemapForRegularPaletteTitle()
    {
        var cartridge = LoadCartridge(
            TestRomFactory.Create(bytes =>
            {
                bytes.AsSpan(0x0134, 16).Clear();
                "POKEMON RED"u8.CopyTo(bytes.AsSpan(0x0134));
                bytes[0x014B] = 0x01;
            })
        );
        var profile = new CgbHardwareProfile(CgbOperatingMode.DmgCompatibility);
        var bus = new MemoryBus(cartridge, profile);
        var cpu = new Cpu(bus);

        profile.ApplyPostBootState(cartridge, cpu, bus);

        cpu.Registers.HL.Should().Be(0x007C);
        bus.Ppu.VideoRam.ReadBank(0, 0x9904).Should().Be(0x00);
        bus.Ppu.VideoRam.ReadBank(0, 0x9910).Should().Be(0x00);
        bus.Ppu.VideoRam.ReadBank(0, 0x992F).Should().Be(0x00);
    }

    [Fact]
    public void Apply_SetsCgbModeIoRegistersAfterBootHandoff()
    {
        var cartridge = LoadCartridge(
            TestRomFactory.Create(rom => rom[0x0143] = (byte)CgbSupport.Required)
        );
        var profile = new CgbHardwareProfile(CgbOperatingMode.Cgb);
        var bus = new MemoryBus(cartridge, profile);
        var cpu = new Cpu(bus);

        profile.ApplyPostBootState(cartridge, cpu, bus);

        bus.ReadByte(AddressMap.SerialTransferDataRegister).Should().Be(0x00);
        bus.ReadByte(AddressMap.SerialTransferControlRegister).Should().Be(0x7E);
        bus.ReadByte(AddressMap.TimerCounterRegister).Should().Be(0x00);
        bus.ReadByte(AddressMap.TimerModuloRegister).Should().Be(0x00);
        bus.ReadByte(AddressMap.TimerControlRegister).Should().Be(0xF8);
        bus.ReadByte(AddressMap.InterruptFlagRegister).Should().Be(0xE1);
        bus.ReadByte(AddressMap.LcdControlRegister).Should().Be(0x91);
        bus.ReadByte(AddressMap.ScrollYRegister).Should().Be(0x00);
        bus.ReadByte(AddressMap.ScrollXRegister).Should().Be(0x00);
        bus.ReadByte(AddressMap.LcdYCompareRegister).Should().Be(0x00);
        bus.ReadByte(AddressMap.DmaRegister).Should().Be(0x00);
        bus.ReadByte(AddressMap.BackgroundPaletteRegister).Should().Be(0xFC);
        bus.ReadByte(AddressMap.WindowYRegister).Should().Be(0x00);
        bus.ReadByte(AddressMap.WindowXRegister).Should().Be(0x00);
        bus.ReadByte(AddressMap.Key1Register).Should().Be(0x7E);
        bus.ReadByte(AddressMap.VideoRamBankRegister).Should().Be(0xFE);
        bus.ReadByte(AddressMap.WorkRamBankRegister).Should().Be(0xF8);
        bus.ReadByte(AddressMap.InterruptEnableRegister).Should().Be(0x00);
    }

    [Fact]
    public void Apply_SeedsCgbBackgroundPaletteRamToWhiteWithoutChangingIndex()
    {
        var cartridge = LoadCartridge(
            TestRomFactory.Create(rom => rom[0x0143] = (byte)CgbSupport.Required)
        );
        var profile = new CgbHardwareProfile(CgbOperatingMode.Cgb);
        var bus = new MemoryBus(cartridge, profile);
        var cpu = new Cpu(bus);
        bus.WriteByte(AddressMap.BackgroundPaletteIndexRegister, 0x85);
        bus.WriteByte(AddressMap.BackgroundPaletteDataRegister, 0x12);
        bus.WriteByte(AddressMap.BackgroundPaletteIndexRegister, 0x85);

        profile.ApplyPostBootState(cartridge, cpu, bus);

        bus.ReadByte(AddressMap.BackgroundPaletteIndexRegister).Should().Be(0xC5);
        bus.ReadByte(AddressMap.BackgroundPaletteDataRegister).Should().Be(0x7F);
        bus.WriteByte(AddressMap.BackgroundPaletteIndexRegister, 0x04);
        bus.ReadByte(AddressMap.BackgroundPaletteDataRegister).Should().Be(0xFF);
    }

    [Fact]
    public void Apply_SeedsCgbDmgCompatibilityPaletteRamForRgbRendering()
    {
        var cartridge = LoadCartridge(TestRomFactory.Create());
        var profile = new CgbHardwareProfile(CgbOperatingMode.DmgCompatibility);
        var bus = new MemoryBus(cartridge, profile);
        var cpu = new Cpu(bus);

        profile.ApplyPostBootState(cartridge, cpu, bus);

        bus.WriteByte(AddressMap.BackgroundPaletteRegister, 0x08);
        bus.Ppu.VideoRam.Write(0x8000, 0x80);

        bus.Ppu.Tick(456 * 154);
        var frame = bus.Ppu.Tick(456 * 144).CompletedFrame.Should().BeOfType<LcdFrame>().Subject;

        frame.PixelFormat.Should().Be(LcdPixelFormat.Rgb555Le);
        frame.Pixels.Span[0].Should().Be(0x80);
        frame.Pixels.Span[1].Should().Be(0x61);
        bus.ReadByte(AddressMap.BackgroundPaletteIndexRegister).Should().Be(0xC8);
    }

    [Fact]
    public void Apply_SetsCgbDmgCompatibilityHardwareRegistersObservedByBootHwioC()
    {
        var cartridge = LoadCartridge(TestRomFactory.Create());
        var profile = new CgbHardwareProfile(CgbOperatingMode.DmgCompatibility);
        var bus = new MemoryBus(cartridge, profile);
        var cpu = new Cpu(bus);

        profile.ApplyPostBootState(cartridge, cpu, bus);

        bus.ReadByte(AddressMap.SerialTransferControlRegister).Should().Be(0x7E);
        bus.ReadByte(AddressMap.VideoRamBankRegister).Should().Be(0xFE);
        bus.ReadByte(AddressMap.BackgroundPaletteIndexRegister).Should().Be(0xC8);
        bus.ReadByte(AddressMap.BackgroundPaletteDataRegister).Should().Be(0xFF);
        bus.ReadByte(AddressMap.ObjectPaletteIndexRegister).Should().Be(0xD0);
        bus.ReadByte(AddressMap.ObjectPaletteDataRegister).Should().Be(0xFF);
        bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf75).Should().Be(0x8F);
        bus.ReadByte(AddressMap.AudioPcm12Register).Should().Be(0x00);
        bus.ReadByte(AddressMap.AudioPcm34Register).Should().Be(0x00);
    }

    private static (Cpu Cpu, MemoryBus Bus) CreateHardware(Cartridge cartridge)
    {
        var bus = new MemoryBus(cartridge, DmgHardwareProfile.Instance);
        return (new Cpu(bus), bus);
    }

    private static Cartridge LoadCartridge(byte[] rom) => TestRomFactory.LoadCartridge(rom);

    private static byte[] CreateSgbRom() =>
        TestRomFactory.Create(bytes =>
        {
            bytes[0x0146] = 0x03;
            bytes[0x014B] = 0x33;
        });

    private static byte[] CreateRomWithZeroHeaderChecksum()
    {
        return TestRomFactory.Create(bytes =>
        {
            var checksum = CartridgeHeader.CalculateHeaderChecksum(bytes);
            bytes[0x0134] = unchecked((byte)(bytes[0x0134] + checksum));
        });
    }
}
