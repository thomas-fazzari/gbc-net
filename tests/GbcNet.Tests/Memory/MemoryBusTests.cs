// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cartridges;
using GbcNet.Core.Clock;
using GbcNet.Core.Hardware;
using GbcNet.Core.Hardware.Profiles;
using GbcNet.Core.Joypad;
using GbcNet.Core.Memory;
using GbcNet.Core.Ppu;
using GbcNet.Core.Sm83;
using GbcNet.Tests.Cartridges;

namespace GbcNet.Tests.Memory;

public sealed class MemoryBusTests
{
    private const byte LcdEnable = 0x80;

    [Fact]
    public void ReadByte_RoutesRomWindowToCartridge()
    {
        var rom = TestRomFactory.Create();
        rom[0x0000] = 0x11;
        rom[0x4000] = 0x22;
        rom[0x7FFF] = 0x33;
        var bus = CreateBus(rom);

        Assert.Equal(0x11, bus.ReadByte(0x0000));
        Assert.Equal(0x22, bus.ReadByte(0x4000));
        Assert.Equal(0x33, bus.ReadByte(0x7FFF));
    }

    [Fact]
    public void WriteByte_IgnoresRomWindowForRomOnlyCartridge()
    {
        var rom = TestRomFactory.Create();
        rom[0x0000] = 0x11;
        var bus = CreateBus(rom);

        bus.WriteByte(0x0000, 0xAA);

        Assert.Equal(0x11, bus.ReadByte(0x0000));
    }

    [Fact]
    public void ReadWriteByte_StoresVideoRam()
    {
        var bus = CreateBus();

        bus.WriteByte(0x8000, 0x12);
        bus.WriteByte(0x9FFF, 0x34);

        Assert.Equal(0x12, bus.ReadByte(0x8000));
        Assert.Equal(0x34, bus.ReadByte(0x9FFF));
    }

    [Fact]
    public void ReadWriteByte_StoresCgbModeVideoRamBanksSelectedByVbk()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.Cgb));

        bus.WriteByte(AddressMap.VideoRamStart, 0x12);

        Assert.Equal(0xFE, bus.ReadByte(AddressMap.VideoRamBankRegister));

        bus.WriteByte(AddressMap.VideoRamBankRegister, 0x01);

        Assert.Equal(0xFF, bus.ReadByte(AddressMap.VideoRamBankRegister));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.VideoRamStart));

        bus.WriteByte(AddressMap.VideoRamStart, 0x34);
        bus.WriteByte(AddressMap.VideoRamBankRegister, 0xFE);

        Assert.Equal(0x12, bus.ReadByte(AddressMap.VideoRamStart));

        bus.WriteByte(AddressMap.VideoRamBankRegister, 0x01);

        Assert.Equal(0x34, bus.ReadByte(AddressMap.VideoRamStart));
    }

    [Fact]
    public void ReadWriteByte_ExposesReadOnlyVbkInCgbDmgCompatibilityMode()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.DmgCompatibility));

        bus.WriteByte(AddressMap.VideoRamStart, 0x12);
        bus.WriteByte(AddressMap.VideoRamBankRegister, 0x01);

        Assert.Equal(0xFE, bus.ReadByte(AddressMap.VideoRamBankRegister));

        bus.WriteByte(AddressMap.VideoRamStart, 0x34);
        bus.WriteByte(AddressMap.VideoRamBankRegister, 0x00);

        Assert.Equal(0x34, bus.ReadByte(AddressMap.VideoRamStart));
    }

    [Fact]
    public void ReadWriteByte_RoutesCgbColorPaletteRegisters()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.Cgb));

        bus.WriteByte(AddressMap.BackgroundPaletteIndexRegister, 0x80);
        bus.WriteByte(AddressMap.BackgroundPaletteDataRegister, 0x12);

        Assert.Equal(0xC1, bus.ReadByte(AddressMap.BackgroundPaletteIndexRegister));

        bus.WriteByte(AddressMap.BackgroundPaletteIndexRegister, 0x80);

        Assert.Equal(0x12, bus.ReadByte(AddressMap.BackgroundPaletteDataRegister));
    }

    [Fact]
    public void ReadWriteByte_ExposesPaletteIndexButNotDataRegistersInCgbDmgCompatibilityMode()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.DmgCompatibility));

        bus.WriteByte(AddressMap.BackgroundPaletteIndexRegister, 0x80);
        bus.WriteByte(AddressMap.BackgroundPaletteDataRegister, 0x12);
        bus.WriteByte(AddressMap.ObjectPaletteIndexRegister, 0x81);
        bus.WriteByte(AddressMap.ObjectPaletteDataRegister, 0x34);

        Assert.Equal(0xC0, bus.ReadByte(AddressMap.BackgroundPaletteIndexRegister));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.BackgroundPaletteDataRegister));
        Assert.Equal(0xC1, bus.ReadByte(AddressMap.ObjectPaletteIndexRegister));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.ObjectPaletteDataRegister));
    }

    [Fact]
    public void ReadWriteByte_IgnoresColorPaletteRegistersOnDmg()
    {
        var bus = CreateBus();

        bus.WriteByte(AddressMap.BackgroundPaletteIndexRegister, 0x80);
        bus.WriteByte(AddressMap.BackgroundPaletteDataRegister, 0x12);
        bus.WriteByte(AddressMap.ObjectPaletteIndexRegister, 0x81);
        bus.WriteByte(AddressMap.ObjectPaletteDataRegister, 0x34);

        Assert.Equal(0xFF, bus.ReadByte(AddressMap.BackgroundPaletteIndexRegister));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.BackgroundPaletteDataRegister));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.ObjectPaletteIndexRegister));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.ObjectPaletteDataRegister));
    }

    [Fact]
    public void ReadWriteByte_RoutesCgbObjectPriorityModeRegister()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.Cgb));

        Assert.Equal(0xFE, bus.ReadByte(AddressMap.ObjectPriorityModeRegister));

        bus.WriteByte(AddressMap.ObjectPriorityModeRegister, 0xFF);

        Assert.Equal(0xFF, bus.ReadByte(AddressMap.ObjectPriorityModeRegister));

        bus.WriteByte(AddressMap.ObjectPriorityModeRegister, 0xFE);

        Assert.Equal(0xFE, bus.ReadByte(AddressMap.ObjectPriorityModeRegister));
    }

    [Fact]
    public void ReadWriteByte_IgnoresObjectPriorityModeRegisterInCgbDmgCompatibilityMode()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.DmgCompatibility));

        bus.WriteByte(AddressMap.ObjectPriorityModeRegister, 0x01);

        Assert.Equal(0xFF, bus.ReadByte(AddressMap.ObjectPriorityModeRegister));
    }

    [Fact]
    public void ReadWriteByte_IgnoresObjectPriorityModeRegisterOnDmg()
    {
        var bus = CreateBus();

        bus.WriteByte(AddressMap.ObjectPriorityModeRegister, 0x01);

        Assert.Equal(0xFF, bus.ReadByte(AddressMap.ObjectPriorityModeRegister));
    }

    [Fact]
    public void ReadWriteByte_RoutesCgbMiscRegistersInCgbMode()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.Cgb));

        Assert.Equal(0x00, bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf72));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf73));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf74));
        Assert.Equal(0x8F, bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf75));

        bus.WriteByte(AddressMap.CgbUndocumentedRegisterFf72, 0xFF);
        bus.WriteByte(AddressMap.CgbUndocumentedRegisterFf73, 0xA5);
        bus.WriteByte(AddressMap.CgbUndocumentedRegisterFf74, 0x5A);
        bus.WriteByte(AddressMap.CgbUndocumentedRegisterFf75, 0x00);

        Assert.Equal(0xFF, bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf72));
        Assert.Equal(0xA5, bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf73));
        Assert.Equal(0x5A, bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf74));
        Assert.Equal(0x8F, bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf75));

        bus.WriteByte(AddressMap.CgbUndocumentedRegisterFf75, 0x70);

        Assert.Equal(0xFF, bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf75));
    }

    [Fact]
    public void ReadWriteByte_RoutesCgbMiscRegistersInCgbDmgCompatibilityMode()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.DmgCompatibility));

        Assert.Equal(0x00, bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf72));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf73));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf74));
        Assert.Equal(0x8F, bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf75));

        bus.WriteByte(AddressMap.CgbUndocumentedRegisterFf72, 0x12);
        bus.WriteByte(AddressMap.CgbUndocumentedRegisterFf73, 0x34);
        bus.WriteByte(AddressMap.CgbUndocumentedRegisterFf74, 0x56);
        bus.WriteByte(AddressMap.CgbUndocumentedRegisterFf75, 0x70);

        Assert.Equal(0x12, bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf72));
        Assert.Equal(0x34, bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf73));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf74));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf75));
    }

    [Fact]
    public void ReadWriteByte_IgnoresCgbMiscRegistersOnDmg()
    {
        var bus = CreateBus();

        bus.WriteByte(AddressMap.CgbUndocumentedRegisterFf72, 0x12);
        bus.WriteByte(AddressMap.CgbUndocumentedRegisterFf73, 0x34);
        bus.WriteByte(AddressMap.CgbUndocumentedRegisterFf74, 0x56);
        bus.WriteByte(AddressMap.CgbUndocumentedRegisterFf75, 0x70);

        Assert.Equal(0xFF, bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf72));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf73));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf74));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf75));
    }

    [Fact]
    public void ReadWriteByte_RoutesCgbPcmOutputRegistersInDmgCompatibilityMode()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.DmgCompatibility));

        bus.WriteByte(AddressMap.AudioPcm12Register, 0xFF);
        bus.WriteByte(AddressMap.AudioPcm34Register, 0xFF);

        Assert.Equal(0x00, bus.ReadByte(AddressMap.AudioPcm12Register));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.AudioPcm34Register));
    }

    [Fact]
    public void ReadWriteByte_IgnoresCgbPcmOutputRegistersOnDmg()
    {
        var bus = CreateBus();

        bus.WriteByte(AddressMap.AudioPcm12Register, 0x00);
        bus.WriteByte(AddressMap.AudioPcm34Register, 0x00);

        Assert.Equal(0xFF, bus.ReadByte(AddressMap.AudioPcm12Register));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.AudioPcm34Register));
    }

    [Fact]
    public void ReadWriteByte_RoutesCgbVramDmaRegistersWithAddressMasks()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x1230] = 0xA1;
            bytes[0x123F] = 0xAF;
        });
        var bus = CreateBus(rom, new CgbHardwareProfile(CgbOperatingMode.Cgb));

        bus.WriteByte(AddressMap.VideoRamDmaSourceHighRegister, 0x12);
        bus.WriteByte(AddressMap.VideoRamDmaSourceLowRegister, 0x3F);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationHighRegister, 0xE1);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationLowRegister, 0x2F);
        bus.WriteByte(AddressMap.VideoRamDmaLengthModeStartRegister, 0x00);

        Assert.Equal(0xFF, bus.ReadByte(AddressMap.VideoRamDmaSourceHighRegister));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.VideoRamDmaSourceLowRegister));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.VideoRamDmaDestinationHighRegister));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.VideoRamDmaDestinationLowRegister));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.VideoRamDmaLengthModeStartRegister));
        Assert.Equal(0xA1, bus.ReadByte(0x8120));
        Assert.Equal(0xAF, bus.ReadByte(0x812F));
    }

    [Fact]
    public void ReadWriteByte_DoesNotCopyCgbHBlankVramDmaBeforeVisibleHBlank()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x1230] = 0xA1);
        var bus = CreateBus(rom, new CgbHardwareProfile(CgbOperatingMode.Cgb));
        var clock = new MachineClock(bus);

        bus.WriteByte(AddressMap.VideoRamDmaSourceHighRegister, 0x12);
        bus.WriteByte(AddressMap.VideoRamDmaSourceLowRegister, 0x30);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationHighRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationLowRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaLengthModeStartRegister, 0x80);
        bus.WriteByte(AddressMap.LcdControlRegister, LcdEnable);

        TickMachineCycles(clock, 62);

        Assert.Equal(0x00, bus.ReadByte(AddressMap.VideoRamDmaLengthModeStartRegister));
        Assert.Equal(0x00, bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart));
    }

    [Fact]
    public void ReadWriteByte_CopiesCgbHBlankVramDmaBlockOnFirstVisibleHBlank()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x1230] = 0xA1;
            bytes[0x123F] = 0xAF;
        });
        var bus = CreateBus(rom, new CgbHardwareProfile(CgbOperatingMode.Cgb));
        var clock = new MachineClock(bus);

        bus.WriteByte(AddressMap.VideoRamDmaSourceHighRegister, 0x12);
        bus.WriteByte(AddressMap.VideoRamDmaSourceLowRegister, 0x30);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationHighRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationLowRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaLengthModeStartRegister, 0x80);
        bus.WriteByte(AddressMap.LcdControlRegister, LcdEnable);

        TickMachineCycles(clock, 63);

        Assert.Equal(0xFF, bus.ReadByte(AddressMap.VideoRamDmaLengthModeStartRegister));
        Assert.Equal(0xA1, bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart));
        Assert.Equal(0xAF, bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart + 0x0F));
    }

    [Fact]
    public void ReadWriteByte_CopiesCgbHBlankVramDmaBlocksAcrossVisibleHBlanks()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            for (var offset = 0; offset < 0x20; offset++)
            {
                bytes[0x2000 + offset] = (byte)(0x40 + offset);
            }
        });
        var bus = CreateBus(rom, new CgbHardwareProfile(CgbOperatingMode.Cgb));
        var clock = new MachineClock(bus);

        bus.WriteByte(AddressMap.VideoRamDmaSourceHighRegister, 0x20);
        bus.WriteByte(AddressMap.VideoRamDmaSourceLowRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationHighRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationLowRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaLengthModeStartRegister, 0x81);
        bus.WriteByte(AddressMap.LcdControlRegister, LcdEnable);

        TickMachineCycles(clock, 63);

        Assert.Equal(0x00, bus.ReadByte(AddressMap.VideoRamDmaLengthModeStartRegister));
        Assert.Equal(0x40, bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart));
        Assert.Equal(0x4F, bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart + 0x0F));
        Assert.Equal(0x00, bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart + 0x10));

        TickMachineCycles(clock, 114);

        Assert.Equal(0xFF, bus.ReadByte(AddressMap.VideoRamDmaLengthModeStartRegister));
        Assert.Equal(0x50, bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart + 0x10));
        Assert.Equal(0x5F, bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart + 0x1F));
    }

    [Fact]
    public void ReadWriteByte_DoesNotCopyCgbHBlankVramDmaDuringVBlank()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x1230] = 0xA1);
        var bus = CreateBus(rom, new CgbHardwareProfile(CgbOperatingMode.Cgb));
        var clock = new MachineClock(bus);

        bus.WriteByte(AddressMap.LcdControlRegister, LcdEnable);
        bus.SetHardwareRegisterState(
            AddressMap.LcdYCoordinateRegister,
            PpuGeometry.VBlankStartLine
        );
        bus.SetHardwareRegisterState(AddressMap.LcdStatusRegister, (byte)PpuMode.VBlank);
        bus.WriteByte(AddressMap.VideoRamDmaSourceHighRegister, 0x12);
        bus.WriteByte(AddressMap.VideoRamDmaSourceLowRegister, 0x30);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationHighRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationLowRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaLengthModeStartRegister, 0x80);

        TickMachineCycles(
            clock,
            PpuGeometry.ScanlineDots * 10 / HardwareTiming.MachineCycleTCycles
        );

        Assert.Equal(0x00, bus.ReadByte(AddressMap.VideoRamDmaLengthModeStartRegister));
        Assert.Equal(0x00, bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart));
    }

    [Fact]
    public void ReadWriteByte_CancelsActiveCgbHBlankVramDmaWithRemainingCount()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x1230] = 0xA1);
        var bus = CreateBus(rom, new CgbHardwareProfile(CgbOperatingMode.Cgb));

        bus.WriteByte(AddressMap.VideoRamDmaSourceHighRegister, 0x12);
        bus.WriteByte(AddressMap.VideoRamDmaSourceLowRegister, 0x30);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationHighRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationLowRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaLengthModeStartRegister, 0x83);

        bus.WriteByte(AddressMap.VideoRamDmaLengthModeStartRegister, 0x00);

        Assert.Equal(0x83, bus.ReadByte(AddressMap.VideoRamDmaLengthModeStartRegister));
        Assert.Equal(0x00, bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart));
    }

    [Fact]
    public void ReadWriteByte_CopiesCgbVramDmaMultipleBlocksIntoSelectedVramBank()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            for (var offset = 0; offset < 0x20; offset++)
            {
                bytes[0x2000 + offset] = (byte)(0x40 + offset);
            }
        });
        var bus = CreateBus(rom, new CgbHardwareProfile(CgbOperatingMode.Cgb));

        bus.WriteByte(AddressMap.VideoRamBankRegister, 0x01);
        bus.WriteByte(AddressMap.VideoRamDmaSourceHighRegister, 0x20);
        bus.WriteByte(AddressMap.VideoRamDmaSourceLowRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationHighRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationLowRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaLengthModeStartRegister, 0x01);

        Assert.Equal(0x40, bus.ReadByte(AddressMap.VideoRamStart));
        Assert.Equal(0x5F, bus.ReadByte(AddressMap.VideoRamStart + 0x1F));

        bus.WriteByte(AddressMap.VideoRamBankRegister, 0x00);

        Assert.Equal(0x00, bus.ReadByte(AddressMap.VideoRamStart));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.VideoRamStart + 0x1F));

        bus.WriteByte(AddressMap.VideoRamBankRegister, 0x01);

        Assert.Equal(0x40, bus.ReadByte(AddressMap.VideoRamStart));
        Assert.Equal(0x5F, bus.ReadByte(AddressMap.VideoRamStart + 0x1F));
    }

    [Fact]
    public void ReadWriteByte_CopiesCgbVramDmaFromWorkRam()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.Cgb));
        bus.WriteByte(AddressMap.WorkRamStart, 0x55);
        bus.WriteByte(AddressMap.WorkRamStart + 0x0F, 0x66);

        bus.WriteByte(AddressMap.VideoRamDmaSourceHighRegister, 0xC0);
        bus.WriteByte(AddressMap.VideoRamDmaSourceLowRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationHighRegister, 0x10);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationLowRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaLengthModeStartRegister, 0x00);

        Assert.Equal(0x55, bus.ReadByte(0x9000));
        Assert.Equal(0x66, bus.ReadByte(0x900F));
    }

    [Fact]
    public void CpuWrite_BlocksDuringGeneralPurposeCgbVramDma()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0100] = 0x3E;
            bytes[0x0101] = 0x00;
            bytes[0x0102] = 0xE0;
            bytes[0x0103] = 0x55;
            bytes[0x1230] = 0xA1;
            bytes[0x123F] = 0xAF;
        });
        var bus = CreateBus(rom, new CgbHardwareProfile(CgbOperatingMode.Cgb));
        var clock = new MachineClock(bus);
        var cpu = new Cpu(bus, clock.TickMachineCycle);
        bus.WriteByte(AddressMap.VideoRamDmaSourceHighRegister, 0x12);
        bus.WriteByte(AddressMap.VideoRamDmaSourceLowRegister, 0x30);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationHighRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationLowRegister, 0x00);

        Assert.Equal(2, cpu.Step());
        var machineCycles = cpu.Step();

        Assert.Equal(11, machineCycles);
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.VideoRamDmaLengthModeStartRegister));
        Assert.Equal(0xA1, bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart));
        Assert.Equal(0xAF, bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart + 0x0F));
    }

    [Fact]
    public void CpuWrite_BlocksDuringGeneralPurposeCgbVramDmaWithDoubleSpeedCycles()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0100] = 0x3E;
            bytes[0x0101] = 0x00;
            bytes[0x0102] = 0xE0;
            bytes[0x0103] = 0x55;
            bytes[0x1230] = 0xA1;
        });
        var bus = CreateBus(rom, new CgbHardwareProfile(CgbOperatingMode.Cgb));
        var clock = new MachineClock(bus);
        var cpu = new Cpu(bus, clock.TickMachineCycle);
        bus.Clock.SetKey1State(0x80);
        bus.WriteByte(AddressMap.VideoRamDmaSourceHighRegister, 0x12);
        bus.WriteByte(AddressMap.VideoRamDmaSourceLowRegister, 0x30);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationHighRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationLowRegister, 0x00);

        cpu.Step();
        var machineCycles = cpu.Step();

        Assert.Equal(19, machineCycles);
        Assert.Equal(0xA1, bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart));
    }

    [Fact]
    public void CpuStep_BlocksDuringCgbHBlankVramDmaBlock()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x1230] = 0xA1;
            bytes[0x123F] = 0xAF;
        });
        var bus = CreateBus(rom, new CgbHardwareProfile(CgbOperatingMode.Cgb));
        var clock = new MachineClock(bus);
        var cpu = new Cpu(bus, clock.TickMachineCycle);
        bus.WriteByte(AddressMap.VideoRamDmaSourceHighRegister, 0x12);
        bus.WriteByte(AddressMap.VideoRamDmaSourceLowRegister, 0x30);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationHighRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationLowRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaLengthModeStartRegister, 0x80);
        bus.WriteByte(AddressMap.LcdControlRegister, LcdEnable);
        TickMachineCycles(clock, 62);

        var machineCycles = cpu.Step();

        Assert.Equal(9, machineCycles);
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.VideoRamDmaLengthModeStartRegister));
        Assert.Equal(0xA1, bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart));
        Assert.Equal(0xAF, bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart + 0x0F));
    }

    [Fact]
    public void CpuHalt_SuspendsCgbHBlankVramDmaBlocks()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x1230] = 0xA1);
        var bus = CreateBus(rom, new CgbHardwareProfile(CgbOperatingMode.Cgb));
        var clock = new MachineClock(bus);
        var cpu = new Cpu(bus, clock.TickMachineCycle);
        bus.WriteByte(AddressMap.VideoRamDmaSourceHighRegister, 0x12);
        bus.WriteByte(AddressMap.VideoRamDmaSourceLowRegister, 0x30);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationHighRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationLowRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaLengthModeStartRegister, 0x80);
        bus.WriteByte(AddressMap.LcdControlRegister, LcdEnable);
        TickMachineCycles(clock, 62);
        cpu.Halt();

        var machineCycles = cpu.Step();

        Assert.Equal(1, machineCycles);
        Assert.Equal(0x00, bus.ReadByte(AddressMap.VideoRamDmaLengthModeStartRegister));
        Assert.Equal(0x00, bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart));
    }

    [Fact]
    public void ReadWriteByte_IgnoresCgbVramDmaRegistersOnDmg()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x1200] = 0x77);
        var bus = CreateBus(rom);

        bus.WriteByte(AddressMap.VideoRamDmaSourceHighRegister, 0x12);
        bus.WriteByte(AddressMap.VideoRamDmaSourceLowRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationHighRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationLowRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaLengthModeStartRegister, 0x00);

        Assert.Equal(0xFF, bus.ReadByte(AddressMap.VideoRamDmaSourceHighRegister));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.VideoRamDmaLengthModeStartRegister));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.VideoRamStart));
    }

    [Fact]
    public void ReadWriteByte_IgnoresCgbVramDmaRegistersInDmgCompatibilityMode()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x1200] = 0x77);
        var bus = CreateBus(rom, new CgbHardwareProfile(CgbOperatingMode.DmgCompatibility));

        bus.WriteByte(AddressMap.VideoRamDmaSourceHighRegister, 0x12);
        bus.WriteByte(AddressMap.VideoRamDmaSourceLowRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationHighRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationLowRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaLengthModeStartRegister, 0x00);

        Assert.Equal(0xFF, bus.ReadByte(AddressMap.VideoRamDmaSourceHighRegister));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.VideoRamDmaLengthModeStartRegister));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.VideoRamStart));
    }

    [Fact]
    public void ReadWriteByte_StoresDmgPaletteRegisters()
    {
        var bus = CreateBus();

        bus.WriteByte(AddressMap.BackgroundPaletteRegister, 0xFC);
        bus.WriteByte(AddressMap.ObjectPalette0Register, 0xA5);
        bus.WriteByte(AddressMap.ObjectPalette1Register, 0x5A);

        Assert.Equal(0xFC, bus.ReadByte(AddressMap.BackgroundPaletteRegister));
        Assert.Equal(0xA5, bus.ReadByte(AddressMap.ObjectPalette0Register));
        Assert.Equal(0x5A, bus.ReadByte(AddressMap.ObjectPalette1Register));
    }

    [Fact]
    public void ReadWriteByte_StoresWorkRam()
    {
        var bus = CreateBus();

        bus.WriteByte(0xC000, 0x56);
        bus.WriteByte(0xDFFF, 0x78);

        Assert.Equal(0x56, bus.ReadByte(0xC000));
        Assert.Equal(0x78, bus.ReadByte(0xDFFF));
    }

    [Fact]
    public void ReadWriteByte_MirrorsEchoRamToWorkRam()
    {
        var bus = CreateBus();

        bus.WriteByte(0xC000, 0x9A);
        bus.WriteByte(0xFDFF, 0xBC);

        Assert.Equal(0x9A, bus.ReadByte(0xE000));
        Assert.Equal(0xBC, bus.ReadByte(0xDDFF));
    }

    [Fact]
    public void ReadWriteByte_StoresCgbModeWorkRamBanksSelectedBySvbk()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.Cgb));

        bus.WriteByte(AddressMap.WorkRamSwitchableBankStart, 0x11);
        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x02);

        Assert.Equal(0xFA, bus.ReadByte(AddressMap.WorkRamBankRegister));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.WorkRamSwitchableBankStart));

        bus.WriteByte(AddressMap.WorkRamSwitchableBankStart, 0x22);
        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x01);

        Assert.Equal(0x11, bus.ReadByte(AddressMap.WorkRamSwitchableBankStart));

        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x02);

        Assert.Equal(0x22, bus.ReadByte(AddressMap.WorkRamSwitchableBankStart));
    }

    [Fact]
    public void ReadWriteByte_KeepsFixedWorkRamBankAcrossSvbk()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.Cgb));

        bus.WriteByte(AddressMap.WorkRamStart, 0x44);
        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x07);

        Assert.Equal(0x44, bus.ReadByte(AddressMap.WorkRamStart));

        bus.WriteByte(AddressMap.WorkRamStart, 0x55);
        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x01);

        Assert.Equal(0x55, bus.ReadByte(AddressMap.WorkRamStart));
    }

    [Fact]
    public void ReadWriteByte_SvbkZeroReadsF8AndMapsBankOne()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.Cgb));

        bus.WriteByte(AddressMap.WorkRamSwitchableBankStart, 0x11);
        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x02);
        bus.WriteByte(AddressMap.WorkRamSwitchableBankStart, 0x22);
        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x00);

        Assert.Equal(0xF8, bus.ReadByte(AddressMap.WorkRamBankRegister));
        Assert.Equal(0x11, bus.ReadByte(AddressMap.WorkRamSwitchableBankStart));
    }

    [Fact]
    public void ReadWriteByte_SvbkSevenReadsFfAndMapsBankSeven()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.Cgb));

        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x07);
        bus.WriteByte(AddressMap.WorkRamSwitchableBankStart, 0x77);
        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x01);

        Assert.Equal(0xF9, bus.ReadByte(AddressMap.WorkRamBankRegister));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.WorkRamSwitchableBankStart));

        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x07);

        Assert.Equal(0xFF, bus.ReadByte(AddressMap.WorkRamBankRegister));
        Assert.Equal(0x77, bus.ReadByte(AddressMap.WorkRamSwitchableBankStart));
    }

    [Fact]
    public void ReadWriteByte_MirrorsEchoRamThroughSelectedCgbWorkRamBank()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.Cgb));

        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x03);
        bus.WriteByte(0xF000, 0x33);
        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x01);

        Assert.Equal(0x00, bus.ReadByte(AddressMap.WorkRamSwitchableBankStart));

        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x03);

        Assert.Equal(0x33, bus.ReadByte(AddressMap.WorkRamSwitchableBankStart));
        Assert.Equal(0x33, bus.ReadByte(0xF000));
    }

    [Fact]
    public void ReadWriteByte_IgnoresSvbkInCgbDmgCompatibilityMode()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.DmgCompatibility));

        bus.WriteByte(AddressMap.WorkRamSwitchableBankStart, 0x12);
        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x07);

        Assert.Equal(0xFF, bus.ReadByte(AddressMap.WorkRamBankRegister));

        bus.WriteByte(AddressMap.WorkRamSwitchableBankStart, 0x34);
        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x01);

        Assert.Equal(0x34, bus.ReadByte(AddressMap.WorkRamSwitchableBankStart));
    }

    [Fact]
    public void ReadWriteByte_IgnoresSvbkOnDmg()
    {
        var bus = CreateBus();

        bus.WriteByte(AddressMap.WorkRamSwitchableBankStart, 0x12);
        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x07);

        Assert.Equal(0xFF, bus.ReadByte(AddressMap.WorkRamBankRegister));

        bus.WriteByte(AddressMap.WorkRamSwitchableBankStart, 0x34);
        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x01);

        Assert.Equal(0x34, bus.ReadByte(AddressMap.WorkRamSwitchableBankStart));
    }

    [Fact]
    public void ReadWriteByte_StoresObjectAttributeMemory()
    {
        var bus = CreateBus();

        bus.WriteByte(0xFE00, 0xDE);
        bus.WriteByte(0xFE9F, 0xF0);

        Assert.Equal(0xDE, bus.ReadByte(0xFE00));
        Assert.Equal(0xF0, bus.ReadByte(0xFE9F));
    }

    [Fact]
    public void ReadWriteByte_BlocksVideoRamDuringPpuDrawingMode()
    {
        var bus = CreateBus();
        bus.WriteByte(AddressMap.VideoRamStart, 0x12);
        bus.WriteByte(AddressMap.LcdControlRegister, LcdEnable);
        bus.Ppu.Tick(80);

        Assert.Equal(0xFF, bus.ReadByte(AddressMap.VideoRamStart));

        bus.WriteByte(AddressMap.VideoRamStart, 0x34);
        bus.Ppu.Tick(172);

        Assert.Equal(0x12, bus.ReadByte(AddressMap.VideoRamStart));
    }

    [Fact]
    public void ReadWriteByte_BlocksObjectAttributeMemoryDuringPpuOamScanAndDrawingModes()
    {
        var bus = CreateBus();
        bus.WriteByte(AddressMap.ObjectAttributeMemoryStart, 0x12);
        bus.WriteByte(AddressMap.LcdControlRegister, LcdEnable);

        Assert.Equal(0x12, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
        bus.WriteByte(AddressMap.ObjectAttributeMemoryStart, 0x34);

        bus.Ppu.Tick(80);
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
        bus.WriteByte(AddressMap.ObjectAttributeMemoryStart, 0x56);

        bus.Ppu.Tick(172);

        Assert.Equal(0x34, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));

        bus.Ppu.Tick(204);

        Assert.Equal(0xFF, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
        bus.WriteByte(AddressMap.ObjectAttributeMemoryStart, 0x78);

        bus.Ppu.Tick(252);

        Assert.Equal(0x34, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void ReadWriteByte_IgnoresNotUsableRange()
    {
        var bus = CreateBus();

        bus.WriteByte(0xFEA0, 0x12);
        bus.WriteByte(0xFEFF, 0x34);

        Assert.Equal(0x00, bus.ReadByte(0xFEA0));
        Assert.Equal(0x00, bus.ReadByte(0xFEFF));
    }

    [Fact]
    public void ReadWriteByte_KeepsNotUsableRangeBehaviorDuringPpuOamBlock()
    {
        var bus = CreateBus();
        bus.WriteByte(AddressMap.LcdControlRegister, LcdEnable);

        bus.WriteByte(AddressMap.NotUsableStart, 0x42);

        Assert.Equal(0x00, bus.ReadByte(AddressMap.NotUsableStart));
    }

    [Fact]
    public void ReadWriteByte_ReadsUnmappedIoRegistersHigh()
    {
        var bus = CreateBus();

        bus.WriteByte(0xFF03, 0x12);
        bus.WriteByte(0xFF7F, 0x34);

        Assert.Equal(0xFF, bus.ReadByte(0xFF03));
        Assert.Equal(0xFF, bus.ReadByte(0xFF7F));
    }

    [Fact]
    public void ReadWriteByte_RoutesJoypadRegister()
    {
        var bus = CreateBus();
        bus.WriteByte(AddressMap.JoypadRegister, 0x20);

        bus.Joypad.SetButtonState(JoypadButton.Right, pressed: true);

        Assert.Equal(0xEE, bus.ReadByte(AddressMap.JoypadRegister));
        Assert.Equal(0b0001_0000, bus.Interrupts.InterruptFlag);
    }

    [Fact]
    public void ReadWriteByte_RoutesSerialRegisters()
    {
        var bus = CreateBus();

        bus.WriteByte(AddressMap.SerialTransferDataRegister, 0x12);
        bus.WriteByte(AddressMap.SerialTransferControlRegister, 0x81);

        Assert.Equal(0x12, bus.ReadByte(AddressMap.SerialTransferDataRegister));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.SerialTransferControlRegister));

        TickMachineCycles(bus, 128 * 8);

        Assert.Equal(0xFF, bus.ReadByte(AddressMap.SerialTransferDataRegister));
        Assert.Equal(0x7F, bus.ReadByte(AddressMap.SerialTransferControlRegister));
        Assert.Equal(0b0000_1000, bus.Interrupts.InterruptFlag);
    }

    [Fact]
    public void ReadWriteByte_StoresHighRam()
    {
        var bus = CreateBus();

        bus.WriteByte(0xFF80, 0x56);
        bus.WriteByte(0xFFFE, 0x78);

        Assert.Equal(0x56, bus.ReadByte(0xFF80));
        Assert.Equal(0x78, bus.ReadByte(0xFFFE));
    }

    [Fact]
    public void ReadWriteByte_StoresInterruptEnableRegister()
    {
        var bus = CreateBus();

        bus.WriteByte(0xFFFF, 0xF1);

        Assert.Equal(0xF1, bus.ReadByte(0xFFFF));
        Assert.Equal(0xF1, bus.Interrupts.InterruptEnable);
    }

    [Fact]
    public void ReadWriteByte_RoutesInterruptFlagRegister()
    {
        var bus = CreateBus();

        bus.WriteByte(0xFF0F, 0xFF);

        Assert.Equal(0xFF, bus.ReadByte(0xFF0F));
        Assert.Equal(0x1F, bus.Interrupts.InterruptFlag);
    }

    [Fact]
    public void ReadWriteByte_RoutesTimerRegisters()
    {
        var bus = CreateBus();
        TickMachineCycles(bus, 64);

        Assert.Equal(0x01, bus.ReadByte(AddressMap.DividerRegister));

        bus.WriteByte(AddressMap.DividerRegister, 0xFF);
        bus.WriteByte(AddressMap.TimerCounterRegister, 0x12);
        bus.WriteByte(AddressMap.TimerModuloRegister, 0x34);
        bus.WriteByte(AddressMap.TimerControlRegister, 0b0000_0101);

        Assert.Equal(0x00, bus.ReadByte(AddressMap.DividerRegister));
        Assert.Equal(0x12, bus.ReadByte(AddressMap.TimerCounterRegister));
        Assert.Equal(0x34, bus.ReadByte(AddressMap.TimerModuloRegister));
        Assert.Equal(0b1111_1101, bus.ReadByte(AddressMap.TimerControlRegister));
    }

    [Fact]
    public void ReadWriteByte_RoutesKey1RegisterForCgbMode()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.Cgb));

        Assert.Equal(0x7E, bus.ReadByte(AddressMap.Key1Register));

        bus.WriteByte(AddressMap.Key1Register, 0xFF);

        Assert.Equal(0x7F, bus.ReadByte(AddressMap.Key1Register));

        bus.WriteByte(AddressMap.Key1Register, 0xFE);

        Assert.Equal(0x7E, bus.ReadByte(AddressMap.Key1Register));
    }

    [Fact]
    public void ReadWriteByte_IgnoresKey1RegisterOnDmg()
    {
        var bus = CreateBus();

        bus.WriteByte(AddressMap.Key1Register, 0x01);

        Assert.Equal(0xFF, bus.ReadByte(AddressMap.Key1Register));
        Assert.False(bus.Clock.CgbDoubleSpeed);
    }

    [Fact]
    public void ReadWriteByte_IgnoresKey1RegisterInCgbDmgCompatibilityMode()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.DmgCompatibility));

        bus.WriteByte(AddressMap.Key1Register, 0x01);

        Assert.Equal(0xFF, bus.ReadByte(AddressMap.Key1Register));
        Assert.False(bus.Clock.CgbDoubleSpeed);
    }

    [Fact]
    public void TickMachineCycle_TicksPpuAtTwoTCyclesInDoubleSpeed()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.Cgb));
        var clock = new MachineClock(bus);
        bus.WriteByte(AddressMap.LcdControlRegister, 0x80);
        bus.WriteByte(AddressMap.Key1Register, 0x01);

        Assert.True(bus.Clock.TryStartSpeedSwitch());

        TickMachineCycles(clock, 114);

        Assert.Equal(0x00, bus.ReadByte(AddressMap.LcdYCoordinateRegister));

        TickMachineCycles(clock, 114);

        Assert.Equal(0x01, bus.ReadByte(AddressMap.LcdYCoordinateRegister));
    }

    [Fact]
    public void WriteByte_DividerRegisterTicksApuDivApuOnFallingEdge()
    {
        var bus = CreateBus();
        bus.Clock.SetCounter(1 << 12);

        bus.WriteByte(AddressMap.DividerRegister, 0x00);

        Assert.Equal(1, bus.Apu.DivApuStep);
    }

    [Fact]
    public void ReadWriteByte_RoutesPpuRegisters()
    {
        var bus = CreateBus();
        bus.SetHardwareRegisterState(AddressMap.LcdStatusRegister, 0x85);
        bus.SetHardwareRegisterState(AddressMap.LcdYCoordinateRegister, 0x42);

        bus.WriteByte(AddressMap.LcdControlRegister, 0x91);
        bus.WriteByte(AddressMap.LcdStatusRegister, 0x78);
        bus.WriteByte(AddressMap.LcdYCoordinateRegister, 0x99);
        bus.WriteByte(AddressMap.BackgroundPaletteRegister, 0xFC);
        bus.WriteByte(AddressMap.ObjectPalette0Register, 0xA5);
        bus.WriteByte(AddressMap.ObjectPalette1Register, 0x5A);

        Assert.Equal(0x91, bus.ReadByte(AddressMap.LcdControlRegister));
        Assert.Equal(0xF8, bus.ReadByte(AddressMap.LcdStatusRegister));
        Assert.Equal(0x42, bus.ReadByte(AddressMap.LcdYCoordinateRegister));
        Assert.Equal(0xFC, bus.ReadByte(AddressMap.BackgroundPaletteRegister));
        Assert.Equal(0xA5, bus.ReadByte(AddressMap.ObjectPalette0Register));
        Assert.Equal(0x5A, bus.ReadByte(AddressMap.ObjectPalette1Register));
    }

    [Fact]
    public void ReadWriteByte_RoutesDmaRegisterAndDefersOamCopy()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x1200] = 0x42);
        var bus = CreateBus(rom);

        bus.WriteByte(AddressMap.DmaRegister, 0x12);
        bus.TickDma(2);
        bus.TickDma(160);

        Assert.Equal(0x42, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void TickDma_CopiesFromRomWindow()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x1200] = 0x66);
        var bus = CreateBus(rom);

        bus.WriteByte(AddressMap.DmaRegister, 0x12);
        bus.TickDma(2);
        bus.TickDma(160);

        Assert.Equal(0x66, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void TickDma_CopiesFromVideoRam()
    {
        var bus = CreateBus();
        bus.WriteByte(AddressMap.VideoRamStart, 0x99);

        bus.WriteByte(AddressMap.DmaRegister, 0x80);
        bus.TickDma(2);
        bus.TickDma(160);

        Assert.Equal(0x99, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void TickDma_CopiesFromExternalRam()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc1Ram;
            bytes[0x0149] = 0x02;
        });
        var bus = CreateBus(rom);
        bus.WriteByte(0x0000, 0x0A);
        bus.WriteByte(AddressMap.ExternalRamStart, 0x42);

        bus.WriteByte(AddressMap.DmaRegister, 0xA0);
        bus.TickDma(2);
        bus.TickDma(160);

        Assert.Equal(0x42, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void TickDma_CopiesFromWorkRam()
    {
        var bus = CreateBus();
        bus.WriteByte(AddressMap.WorkRamStart, 0x42);

        bus.WriteByte(AddressMap.DmaRegister, 0xC0);
        bus.TickDma(2);
        bus.TickDma(160);

        Assert.Equal(0x42, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void TickDma_MirrorsHighSourcePagesToWorkRam()
    {
        var bus = CreateBus();
        bus.WriteByte(0xDF00, 0x42);
        bus.WriteByte(AddressMap.JoypadRegister, 0x20);
        bus.Joypad.SetButtonState(JoypadButton.Right, pressed: true);

        bus.WriteByte(AddressMap.DmaRegister, 0xFF);
        bus.TickDma(2);
        bus.TickDma(160);

        Assert.Equal(0x42, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void TickDma_WritesObjectAttributeMemoryWhileCpuAccessIsPpuBlocked()
    {
        var bus = CreateBus();
        bus.WriteByte(AddressMap.VideoRamStart, 0x42);
        bus.WriteByte(AddressMap.LcdControlRegister, LcdEnable);
        bus.Ppu.Tick(80);

        bus.WriteByte(AddressMap.DmaRegister, 0x80);
        bus.TickDma(2);
        bus.TickDma(160);

        Assert.Equal(0xFF, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));

        bus.Ppu.Tick(172);

        Assert.Equal(0x42, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void ReadByte_AllowsObjectAttributeMemoryDuringDmaStartupDelay()
    {
        var bus = CreateBus();
        bus.WriteByte(AddressMap.ObjectAttributeMemoryStart, 0x44);

        bus.WriteByte(AddressMap.DmaRegister, 0x80);

        Assert.Equal(0x44, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));

        bus.TickDma(1);
        Assert.Equal(0x44, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));

        bus.TickDma(1);
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void ReadByte_AppliesDmgDmaBusConflicts()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0000] = 0x11);
        var bus = CreateBus(rom);
        bus.WriteByte(AddressMap.VideoRamStart, 0x22);
        bus.WriteByte(AddressMap.VideoRamStart + 1, 0x77);
        bus.WriteByte(AddressMap.WorkRamStart, 0x33);
        bus.WriteByte(AddressMap.ObjectAttributeMemoryStart, 0x44);

        bus.WriteByte(AddressMap.DmaRegister, 0x80);
        bus.TickDma(2);
        bus.TickDma(1);

        Assert.Equal(0x11, bus.ReadByte(AddressMap.RomStart));
        Assert.Equal(0x22, bus.ReadByte(AddressMap.VideoRamStart + 1));
        Assert.Equal(0x33, bus.ReadByte(AddressMap.WorkRamStart));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void ReadByte_AppliesCgbDmaBusConflictsFromWorkRam()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0000] = 0x11;
            bytes[0x0147] = (byte)CartridgeType.Mbc1Ram;
            bytes[0x0149] = 0x02;
        });
        var bus = CreateBus(rom, new CgbHardwareProfile(CgbOperatingMode.Cgb));
        bus.WriteByte(0x0000, 0x0A);
        bus.WriteByte(AddressMap.ExternalRamStart, 0x55);
        bus.WriteByte(AddressMap.WorkRamStart, 0x22);
        bus.WriteByte(AddressMap.WorkRamStart + 1, 0x33);
        bus.WriteByte(AddressMap.ObjectAttributeMemoryStart, 0x44);

        bus.WriteByte(AddressMap.DmaRegister, 0xC0);
        bus.TickDma(2);
        bus.TickDma(1);

        Assert.Equal(0x11, bus.ReadByte(AddressMap.RomStart));
        Assert.Equal(0x55, bus.ReadByte(AddressMap.ExternalRamStart));
        Assert.Equal(0x22, bus.ReadByte(AddressMap.WorkRamStart + 1));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void ReadByte_AppliesCgbDmaBusConflictsFromCartridge()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x1200] = 0x66;
            bytes[0x0147] = (byte)CartridgeType.Mbc1Ram;
            bytes[0x0149] = 0x02;
        });
        var bus = CreateBus(rom, new CgbHardwareProfile(CgbOperatingMode.Cgb));
        bus.WriteByte(0x0000, 0x0A);
        bus.WriteByte(AddressMap.ExternalRamStart, 0x55);
        bus.WriteByte(AddressMap.WorkRamStart, 0x33);
        bus.WriteByte(AddressMap.ObjectAttributeMemoryStart, 0x44);

        bus.WriteByte(AddressMap.DmaRegister, 0x12);
        bus.TickDma(2);
        bus.TickDma(1);

        Assert.Equal(0x66, bus.ReadByte(AddressMap.RomStart));
        Assert.Equal(0x66, bus.ReadByte(AddressMap.ExternalRamStart));
        Assert.Equal(0x33, bus.ReadByte(AddressMap.WorkRamStart));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void WriteByte_BlocksCpuMemoryDuringDma()
    {
        var bus = CreateBus();
        bus.WriteByte(AddressMap.VideoRamStart, 0x22);
        bus.WriteByte(AddressMap.WorkRamStart, 0x42);
        bus.WriteByte(AddressMap.WorkRamStart + 1, 0x33);
        bus.WriteByte(AddressMap.WorkRamStart + 2, 0x44);
        bus.WriteByte(AddressMap.ObjectAttributeMemoryStart, 0x55);

        bus.WriteByte(AddressMap.DmaRegister, 0xC0);
        bus.TickDma(2);
        bus.TickDma(1);
        bus.WriteByte(AddressMap.VideoRamStart, 0xAA);
        bus.WriteByte(AddressMap.WorkRamStart + 1, 0xBB);
        bus.WriteByte(AddressMap.EchoRamStart + 2, 0xCC);
        bus.WriteByte(AddressMap.ObjectAttributeMemoryStart, 0xDD);
        bus.TickDma(160);

        Assert.Equal(0xAA, bus.ReadByte(AddressMap.VideoRamStart));
        Assert.Equal(0x33, bus.ReadByte(AddressMap.WorkRamStart + 1));
        Assert.Equal(0x44, bus.ReadByte(AddressMap.WorkRamStart + 2));
        Assert.Equal(0x42, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void ReadWriteByte_KeepsNotUsableRangeBehaviorDuringDma()
    {
        var bus = CreateBus();

        bus.WriteByte(AddressMap.DmaRegister, 0xC0);
        bus.WriteByte(AddressMap.NotUsableStart, 0x42);

        Assert.Equal(0x00, bus.ReadByte(AddressMap.NotUsableStart));
    }

    [Fact]
    public void ReadWriteByte_AllowsIoHighRamAndInterruptEnableDuringDma()
    {
        var bus = CreateBus();

        bus.WriteByte(AddressMap.DmaRegister, 0xC0);
        bus.WriteByte(0xFF03, 0x12);
        bus.WriteByte(AddressMap.HighRamStart, 0x34);
        bus.WriteByte(AddressMap.InterruptEnableRegister, 0x56);

        Assert.Equal(0xC0, bus.ReadByte(AddressMap.DmaRegister));
        Assert.Equal(0xFF, bus.ReadByte(0xFF03));
        Assert.Equal(0x34, bus.ReadByte(AddressMap.HighRamStart));
        Assert.Equal(0x56, bus.ReadByte(AddressMap.InterruptEnableRegister));
    }

    [Fact]
    public void WriteByte_AllowsDmaRestartDuringDma()
    {
        var bus = CreateBus();
        bus.WriteByte(AddressMap.VideoRamStart, 0xC0);
        bus.WriteByte(0x9000, 0xD0);

        bus.WriteByte(AddressMap.DmaRegister, 0x80);
        bus.TickDma(2);
        bus.TickDma(1);
        bus.WriteByte(AddressMap.DmaRegister, 0x90);
        bus.TickDma(2);
        bus.TickDma(160);

        Assert.Equal(0x90, bus.ReadByte(AddressMap.DmaRegister));
        Assert.Equal(0xD0, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void SetHardwareRegisterState_DmaRegisterDoesNotStartTransfer()
    {
        var bus = CreateBus();
        bus.WriteByte(AddressMap.VideoRamStart, 0x42);

        bus.SetHardwareRegisterState(AddressMap.DmaRegister, 0x80);
        bus.TickDma(160);

        Assert.Equal(0x80, bus.ReadByte(AddressMap.DmaRegister));
        Assert.Equal(0x00, bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void SetHardwareRegisterState_SerialControlDoesNotStartTransfer()
    {
        var bus = CreateBus();

        bus.SetHardwareRegisterState(AddressMap.SerialTransferDataRegister, 0x00);
        bus.SetHardwareRegisterState(AddressMap.SerialTransferControlRegister, 0x81);
        TickMachineCycles(bus, 128 * 8);

        Assert.Equal(0x00, bus.ReadByte(AddressMap.SerialTransferDataRegister));
        Assert.Equal(0xFF, bus.ReadByte(AddressMap.SerialTransferControlRegister));
        Assert.Equal(0x00, bus.Interrupts.InterruptFlag);
    }

    [Fact]
    public void ReadWriteByte_ExternalRamIsUnmappedForRomOnlyCartridge()
    {
        var bus = CreateBus();

        bus.WriteByte(0xA000, 0x42);

        Assert.Equal(0xFF, bus.ReadByte(0xA000));
        Assert.Equal(0xFF, bus.ReadByte(0xBFFF));
    }

    [Fact]
    public void ReadWriteByte_RoutesExternalRamToMbcCartridge()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc1Ram;
            bytes[0x0149] = 0x02;
        });
        var bus = CreateBus(rom);

        bus.WriteByte(0x0000, 0x0A);
        bus.WriteByte(AddressMap.ExternalRamStart, 0x42);

        Assert.Equal(0x42, bus.ReadByte(AddressMap.ExternalRamStart));
    }

    private static MemoryBus CreateBus() => CreateBus(TestRomFactory.Create());

    private static MemoryBus CreateBus(IHardwareProfile profile) =>
        CreateBus(TestRomFactory.Create(), profile);

    private static MemoryBus CreateBus(byte[] rom) => CreateBus(rom, DmgHardwareProfile.Instance);

    private static MemoryBus CreateBus(byte[] rom, IHardwareProfile profile)
    {
        var cartridge = TestRomFactory.LoadCartridge(rom);
        return new MemoryBus(cartridge, profile);
    }

    private static void TickMachineCycles(MemoryBus bus, int machineCycles)
    {
        for (var cycle = 0; cycle < machineCycles; cycle++)
        {
            bus.Clock.TickMachineCycle();
            bus.Apu.Tick(HardwareTiming.MachineCycleTCycles);
        }
    }

    private static void TickMachineCycles(MachineClock clock, int machineCycles)
    {
        for (var cycle = 0; cycle < machineCycles; cycle++)
        {
            clock.TickMachineCycle();
        }
    }
}
