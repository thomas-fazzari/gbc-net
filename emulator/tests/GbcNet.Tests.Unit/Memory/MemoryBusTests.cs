// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Apu;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Clock;
using GbcNet.Core.Hardware;
using GbcNet.Core.Hardware.Profiles;
using GbcNet.Core.Joypad;
using GbcNet.Core.Memory;
using GbcNet.Core.Ppu;
using GbcNet.Core.Sm83;

namespace GbcNet.Tests.Unit.Memory;

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

        bus.ReadByte(0x0000).Should().Be(0x11);
        bus.ReadByte(0x4000).Should().Be(0x22);
        bus.ReadByte(0x7FFF).Should().Be(0x33);
    }

    [Fact]
    public void WriteByte_IgnoresRomWindowForRomOnlyCartridge()
    {
        var rom = TestRomFactory.Create();
        rom[0x0000] = 0x11;
        var bus = CreateBus(rom);

        bus.WriteByte(0x0000, 0xAA);

        bus.ReadByte(0x0000).Should().Be(0x11);
    }

    [Fact]
    public void ReadWriteByte_StoresVideoRam()
    {
        var bus = CreateBus();

        bus.WriteByte(0x8000, 0x12);
        bus.WriteByte(0x9FFF, 0x34);

        bus.ReadByte(0x8000).Should().Be(0x12);
        bus.ReadByte(0x9FFF).Should().Be(0x34);
    }

    [Fact]
    public void ReadWriteByte_StoresCgbModeVideoRamBanksSelectedByVbk()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.Cgb));

        bus.WriteByte(AddressMap.VideoRamStart, 0x12);

        bus.ReadByte(AddressMap.VideoRamBankRegister).Should().Be(0xFE);

        bus.WriteByte(AddressMap.VideoRamBankRegister, 0x01);

        bus.ReadByte(AddressMap.VideoRamBankRegister).Should().Be(0xFF);
        bus.ReadByte(AddressMap.VideoRamStart).Should().Be(0x00);

        bus.WriteByte(AddressMap.VideoRamStart, 0x34);
        bus.WriteByte(AddressMap.VideoRamBankRegister, 0xFE);

        bus.ReadByte(AddressMap.VideoRamStart).Should().Be(0x12);

        bus.WriteByte(AddressMap.VideoRamBankRegister, 0x01);

        bus.ReadByte(AddressMap.VideoRamStart).Should().Be(0x34);
    }

    [Fact]
    public void ReadWriteByte_ExposesReadOnlyVbkInCgbDmgCompatibilityMode()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.DmgCompatibility));

        bus.WriteByte(AddressMap.VideoRamStart, 0x12);
        bus.WriteByte(AddressMap.VideoRamBankRegister, 0x01);

        bus.ReadByte(AddressMap.VideoRamBankRegister).Should().Be(0xFE);

        bus.WriteByte(AddressMap.VideoRamStart, 0x34);
        bus.WriteByte(AddressMap.VideoRamBankRegister, 0x00);

        bus.ReadByte(AddressMap.VideoRamStart).Should().Be(0x34);
    }

    [Fact]
    public void ReadWriteByte_RoutesCgbColorPaletteRegisters()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.Cgb));

        bus.WriteByte(AddressMap.BackgroundPaletteIndexRegister, 0x80);
        bus.WriteByte(AddressMap.BackgroundPaletteDataRegister, 0x12);

        bus.ReadByte(AddressMap.BackgroundPaletteIndexRegister).Should().Be(0xC1);

        bus.WriteByte(AddressMap.BackgroundPaletteIndexRegister, 0x80);

        bus.ReadByte(AddressMap.BackgroundPaletteDataRegister).Should().Be(0x12);
    }

    [Fact]
    public void ReadWriteByte_ExposesPaletteIndexButNotDataRegistersInCgbDmgCompatibilityMode()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.DmgCompatibility));

        bus.WriteByte(AddressMap.BackgroundPaletteIndexRegister, 0x80);
        bus.WriteByte(AddressMap.BackgroundPaletteDataRegister, 0x12);
        bus.WriteByte(AddressMap.ObjectPaletteIndexRegister, 0x81);
        bus.WriteByte(AddressMap.ObjectPaletteDataRegister, 0x34);

        bus.ReadByte(AddressMap.BackgroundPaletteIndexRegister).Should().Be(0xC0);
        bus.ReadByte(AddressMap.BackgroundPaletteDataRegister).Should().Be(0xFF);
        bus.ReadByte(AddressMap.ObjectPaletteIndexRegister).Should().Be(0xC1);
        bus.ReadByte(AddressMap.ObjectPaletteDataRegister).Should().Be(0xFF);
    }

    [Fact]
    public void ReadWriteByte_IgnoresColorPaletteRegistersOnDmg()
    {
        var bus = CreateBus();

        bus.WriteByte(AddressMap.BackgroundPaletteIndexRegister, 0x80);
        bus.WriteByte(AddressMap.BackgroundPaletteDataRegister, 0x12);
        bus.WriteByte(AddressMap.ObjectPaletteIndexRegister, 0x81);
        bus.WriteByte(AddressMap.ObjectPaletteDataRegister, 0x34);

        bus.ReadByte(AddressMap.BackgroundPaletteIndexRegister).Should().Be(0xFF);
        bus.ReadByte(AddressMap.BackgroundPaletteDataRegister).Should().Be(0xFF);
        bus.ReadByte(AddressMap.ObjectPaletteIndexRegister).Should().Be(0xFF);
        bus.ReadByte(AddressMap.ObjectPaletteDataRegister).Should().Be(0xFF);
    }

    [Fact]
    public void ReadWriteByte_RoutesCgbObjectPriorityModeRegister()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.Cgb));

        bus.ReadByte(AddressMap.ObjectPriorityModeRegister).Should().Be(0xFE);

        bus.WriteByte(AddressMap.ObjectPriorityModeRegister, 0xFF);

        bus.ReadByte(AddressMap.ObjectPriorityModeRegister).Should().Be(0xFF);

        bus.WriteByte(AddressMap.ObjectPriorityModeRegister, 0xFE);

        bus.ReadByte(AddressMap.ObjectPriorityModeRegister).Should().Be(0xFE);
    }

    [Fact]
    public void ReadWriteByte_IgnoresObjectPriorityModeRegisterInCgbDmgCompatibilityMode()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.DmgCompatibility));

        bus.WriteByte(AddressMap.ObjectPriorityModeRegister, 0x01);

        bus.ReadByte(AddressMap.ObjectPriorityModeRegister).Should().Be(0xFF);
    }

    [Fact]
    public void ReadWriteByte_IgnoresObjectPriorityModeRegisterOnDmg()
    {
        var bus = CreateBus();

        bus.WriteByte(AddressMap.ObjectPriorityModeRegister, 0x01);

        bus.ReadByte(AddressMap.ObjectPriorityModeRegister).Should().Be(0xFF);
    }

    [Fact]
    public void ReadWriteByte_RoutesCgbMiscRegistersInCgbMode()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.Cgb));

        bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf72).Should().Be(0x00);
        bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf73).Should().Be(0x00);
        bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf74).Should().Be(0x00);
        bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf75).Should().Be(0x8F);

        bus.WriteByte(AddressMap.CgbUndocumentedRegisterFf72, 0xFF);
        bus.WriteByte(AddressMap.CgbUndocumentedRegisterFf73, 0xA5);
        bus.WriteByte(AddressMap.CgbUndocumentedRegisterFf74, 0x5A);
        bus.WriteByte(AddressMap.CgbUndocumentedRegisterFf75, 0x00);

        bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf72).Should().Be(0xFF);
        bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf73).Should().Be(0xA5);
        bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf74).Should().Be(0x5A);
        bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf75).Should().Be(0x8F);

        bus.WriteByte(AddressMap.CgbUndocumentedRegisterFf75, 0x70);

        bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf75).Should().Be(0xFF);
    }

    [Fact]
    public void ReadWriteByte_RoutesCgbMiscRegistersInCgbDmgCompatibilityMode()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.DmgCompatibility));

        bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf72).Should().Be(0x00);
        bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf73).Should().Be(0x00);
        bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf74).Should().Be(0xFF);
        bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf75).Should().Be(0x8F);

        bus.WriteByte(AddressMap.CgbUndocumentedRegisterFf72, 0x12);
        bus.WriteByte(AddressMap.CgbUndocumentedRegisterFf73, 0x34);
        bus.WriteByte(AddressMap.CgbUndocumentedRegisterFf74, 0x56);
        bus.WriteByte(AddressMap.CgbUndocumentedRegisterFf75, 0x70);

        bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf72).Should().Be(0x12);
        bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf73).Should().Be(0x34);
        bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf74).Should().Be(0xFF);
        bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf75).Should().Be(0xFF);
    }

    [Fact]
    public void ReadWriteByte_IgnoresCgbMiscRegistersOnDmg()
    {
        var bus = CreateBus();

        bus.WriteByte(AddressMap.CgbUndocumentedRegisterFf72, 0x12);
        bus.WriteByte(AddressMap.CgbUndocumentedRegisterFf73, 0x34);
        bus.WriteByte(AddressMap.CgbUndocumentedRegisterFf74, 0x56);
        bus.WriteByte(AddressMap.CgbUndocumentedRegisterFf75, 0x70);

        bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf72).Should().Be(0xFF);
        bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf73).Should().Be(0xFF);
        bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf74).Should().Be(0xFF);
        bus.ReadByte(AddressMap.CgbUndocumentedRegisterFf75).Should().Be(0xFF);
    }

    [Fact]
    public void ReadWriteByte_RoutesCgbPcmOutputRegistersInDmgCompatibilityMode()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.DmgCompatibility));

        bus.WriteByte(AddressMap.AudioPcm12Register, 0xFF);
        bus.WriteByte(AddressMap.AudioPcm34Register, 0xFF);

        bus.ReadByte(AddressMap.AudioPcm12Register).Should().Be(0x00);
        bus.ReadByte(AddressMap.AudioPcm34Register).Should().Be(0x00);
    }

    [Fact]
    public void ReadWriteByte_IgnoresCgbPcmOutputRegistersOnDmg()
    {
        var bus = CreateBus();

        bus.WriteByte(AddressMap.AudioPcm12Register, 0x00);
        bus.WriteByte(AddressMap.AudioPcm34Register, 0x00);

        bus.ReadByte(AddressMap.AudioPcm12Register).Should().Be(0xFF);
        bus.ReadByte(AddressMap.AudioPcm34Register).Should().Be(0xFF);
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
        var clock = new MachineClock(bus);

        bus.WriteByte(AddressMap.VideoRamDmaSourceHighRegister, 0x12);
        bus.WriteByte(AddressMap.VideoRamDmaSourceLowRegister, 0x3F);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationHighRegister, 0xE1);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationLowRegister, 0x2F);
        bus.WriteByte(AddressMap.VideoRamDmaLengthModeStartRegister, 0x00);
        TickMachineCycles(clock, 9);

        bus.ReadByte(AddressMap.VideoRamDmaSourceHighRegister).Should().Be(0xFF);
        bus.ReadByte(AddressMap.VideoRamDmaSourceLowRegister).Should().Be(0xFF);
        bus.ReadByte(AddressMap.VideoRamDmaDestinationHighRegister).Should().Be(0xFF);
        bus.ReadByte(AddressMap.VideoRamDmaDestinationLowRegister).Should().Be(0xFF);
        bus.ReadByte(AddressMap.VideoRamDmaLengthModeStartRegister).Should().Be(0xFF);
        bus.ReadByte(0x8120).Should().Be(0xA1);
        bus.ReadByte(0x812F).Should().Be(0xAF);
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

        bus.ReadByte(AddressMap.VideoRamDmaLengthModeStartRegister).Should().Be(0x00);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart).Should().Be(0x00);
    }

    [Fact]
    public void ReadWriteByte_CopiesCgbHBlankVramDmaBlockOverEightNormalSpeedCycles()
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

        // Pan Docs `cgb-registers.md`: one 16-byte block takes eight normal-speed M-cycles.
        bus.ReadByte(AddressMap.VideoRamDmaLengthModeStartRegister).Should().Be(0x00);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart).Should().Be(0x00);

        TickMachineCycles(clock, 1);

        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart).Should().Be(0xA1);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart + 0x02).Should().Be(0x00);

        TickMachineCycles(clock, 7);

        bus.ReadByte(AddressMap.VideoRamDmaLengthModeStartRegister).Should().Be(0xFF);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart + 0x0F).Should().Be(0xAF);
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

        TickMachineCycles(clock, 71);

        bus.ReadByte(AddressMap.VideoRamDmaLengthModeStartRegister).Should().Be(0x00);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart).Should().Be(0x40);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart + 0x0F).Should().Be(0x4F);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart + 0x10).Should().Be(0x00);

        TickMachineCycles(clock, 106);

        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart + 0x10).Should().Be(0x00);

        TickMachineCycles(clock, 8);

        bus.ReadByte(AddressMap.VideoRamDmaLengthModeStartRegister).Should().Be(0xFF);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart + 0x10).Should().Be(0x50);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart + 0x1F).Should().Be(0x5F);
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

        bus.ReadByte(AddressMap.VideoRamDmaLengthModeStartRegister).Should().Be(0x00);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart).Should().Be(0x00);
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

        bus.ReadByte(AddressMap.VideoRamDmaLengthModeStartRegister).Should().Be(0x83);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart).Should().Be(0x00);
    }

    [Fact]
    public void ReadWriteByte_CancelsCgbHBlankVramDmaAfterOneBlockWithRemainingCount()
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
        TickMachineCycles(clock, 71);

        bus.WriteByte(AddressMap.VideoRamDmaLengthModeStartRegister, 0x00);
        TickMachineCycles(clock, 114);

        bus.ReadByte(AddressMap.VideoRamDmaLengthModeStartRegister).Should().Be(0x80);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart).Should().Be(0x40);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart + 0x0F).Should().Be(0x4F);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart + 0x10).Should().Be(0x00);
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
        var clock = new MachineClock(bus);

        bus.WriteByte(AddressMap.VideoRamBankRegister, 0x01);
        bus.WriteByte(AddressMap.VideoRamDmaSourceHighRegister, 0x20);
        bus.WriteByte(AddressMap.VideoRamDmaSourceLowRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationHighRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationLowRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaLengthModeStartRegister, 0x01);
        TickMachineCycles(clock, 17);

        bus.ReadByte(AddressMap.VideoRamStart).Should().Be(0x40);
        bus.ReadByte(AddressMap.VideoRamStart + 0x1F).Should().Be(0x5F);

        bus.WriteByte(AddressMap.VideoRamBankRegister, 0x00);

        bus.ReadByte(AddressMap.VideoRamStart).Should().Be(0x00);
        bus.ReadByte(AddressMap.VideoRamStart + 0x1F).Should().Be(0x00);

        bus.WriteByte(AddressMap.VideoRamBankRegister, 0x01);

        bus.ReadByte(AddressMap.VideoRamStart).Should().Be(0x40);
        bus.ReadByte(AddressMap.VideoRamStart + 0x1F).Should().Be(0x5F);
    }

    [Fact]
    public void ReadWriteByte_ContinuesGeneralPurposeCgbVramDmaFromAdvancedAddresses()
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

        bus.WriteByte(AddressMap.VideoRamDmaLengthModeStartRegister, 0x00);
        TickMachineCycles(clock, 9);
        bus.WriteByte(AddressMap.VideoRamDmaLengthModeStartRegister, 0x00);
        TickMachineCycles(clock, 9);

        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart).Should().Be(0x40);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart + 0x0F).Should().Be(0x4F);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart + 0x10).Should().Be(0x50);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart + 0x1F).Should().Be(0x5F);
    }

    [Fact]
    public void ReadWriteByte_CopiesCgbVramDmaFromWorkRam()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.Cgb));
        var clock = new MachineClock(bus);
        bus.WriteByte(AddressMap.WorkRamStart, 0x55);
        bus.WriteByte(AddressMap.WorkRamStart + 0x0F, 0x66);

        bus.WriteByte(AddressMap.VideoRamDmaSourceHighRegister, 0xC0);
        bus.WriteByte(AddressMap.VideoRamDmaSourceLowRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationHighRegister, 0x10);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationLowRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaLengthModeStartRegister, 0x00);
        TickMachineCycles(clock, 9);

        bus.ReadByte(0x9000).Should().Be(0x55);
        bus.ReadByte(0x900F).Should().Be(0x66);
    }

    [Fact]
    public void CpuWrite_BlocksDuringGeneralPurposeCgbVramDmaWhileHardwareClocksContinue()
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
        bus.WriteByte(AddressMap.LcdControlRegister, LcdEnable);
        TickMachineCycles(clock, 10);
        bus.Clock.SetCounter(0x00D0);

        cpu.Step().Should().Be(2);
        var machineCycles = cpu.Step();

        machineCycles.Should().Be(11);
        bus.ReadByte(AddressMap.VideoRamDmaLengthModeStartRegister).Should().Be(0xFF);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart).Should().Be(0xA1);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart + 0x0F).Should().Be(0xAF);
        ((PpuMode)(bus.ReadByte(AddressMap.LcdStatusRegister) & 0x03)).Should().Be(PpuMode.Drawing);
        bus.Clock.ReadDivider().Should().Be(0x01);
        Span<ApuStereoSample> samples = stackalloc ApuStereoSample[1];
        bus.Apu.DrainBufferedSamples(samples).Should().Be(1);
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

        machineCycles.Should().Be(19);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart).Should().Be(0xA1);
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

        machineCycles.Should().Be(9);
        bus.ReadByte(AddressMap.VideoRamDmaLengthModeStartRegister).Should().Be(0xFF);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart).Should().Be(0xA1);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart + 0x0F).Should().Be(0xAF);
    }

    [Fact]
    public void CpuStep_BlocksDuringDoubleSpeedCgbHBlankVramDmaBlock()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x1230] = 0xA1);
        var bus = CreateBus(rom, new CgbHardwareProfile(CgbOperatingMode.Cgb));
        var clock = new MachineClock(bus);
        var cpu = new Cpu(bus, clock.TickMachineCycle);
        bus.Clock.SetKey1State(0x80);
        bus.WriteByte(AddressMap.VideoRamDmaSourceHighRegister, 0x12);
        bus.WriteByte(AddressMap.VideoRamDmaSourceLowRegister, 0x30);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationHighRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationLowRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaLengthModeStartRegister, 0x80);
        bus.WriteByte(AddressMap.LcdControlRegister, LcdEnable);
        TickMachineCycles(clock, 125);

        var machineCycles = cpu.Step();

        machineCycles.Should().Be(17);
        bus.ReadByte(AddressMap.VideoRamDmaLengthModeStartRegister).Should().Be(0xFF);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart).Should().Be(0xA1);
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

        machineCycles.Should().Be(1);
        bus.ReadByte(AddressMap.VideoRamDmaLengthModeStartRegister).Should().Be(0x00);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart).Should().Be(0x00);
    }

    [Fact]
    public void ReadWriteByte_StopsCgbHBlankVramDmaWhenDestinationOverflows()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x2000] = 0xA1;
            bytes[0x200F] = 0xAF;
            bytes[0x2010] = 0xB1;
        });
        var bus = CreateBus(rom, new CgbHardwareProfile(CgbOperatingMode.Cgb));
        var clock = new MachineClock(bus);

        bus.WriteByte(AddressMap.VideoRamDmaSourceHighRegister, 0x20);
        bus.WriteByte(AddressMap.VideoRamDmaSourceLowRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationHighRegister, 0x1F);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationLowRegister, 0xF0);
        bus.WriteByte(AddressMap.VideoRamDmaLengthModeStartRegister, 0x81);
        bus.WriteByte(AddressMap.LcdControlRegister, LcdEnable);
        TickMachineCycles(clock, 71);
        TickMachineCycles(clock, 114);

        bus.Ppu.VideoRam.Read(0x9FF0).Should().Be(0xA1);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamEnd).Should().Be(0xAF);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart).Should().Be(0x00);
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

        bus.ReadByte(AddressMap.VideoRamDmaSourceHighRegister).Should().Be(0xFF);
        bus.ReadByte(AddressMap.VideoRamDmaLengthModeStartRegister).Should().Be(0xFF);
        bus.ReadByte(AddressMap.VideoRamStart).Should().Be(0x00);
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

        bus.ReadByte(AddressMap.VideoRamDmaSourceHighRegister).Should().Be(0xFF);
        bus.ReadByte(AddressMap.VideoRamDmaLengthModeStartRegister).Should().Be(0xFF);
        bus.ReadByte(AddressMap.VideoRamStart).Should().Be(0x00);
    }

    [Fact]
    public void ReadWriteByte_StoresDmgPaletteRegisters()
    {
        var bus = CreateBus();

        bus.WriteByte(AddressMap.BackgroundPaletteRegister, 0xFC);
        bus.WriteByte(AddressMap.ObjectPalette0Register, 0xA5);
        bus.WriteByte(AddressMap.ObjectPalette1Register, 0x5A);

        bus.ReadByte(AddressMap.BackgroundPaletteRegister).Should().Be(0xFC);
        bus.ReadByte(AddressMap.ObjectPalette0Register).Should().Be(0xA5);
        bus.ReadByte(AddressMap.ObjectPalette1Register).Should().Be(0x5A);
    }

    [Fact]
    public void ReadWriteByte_StoresWorkRam()
    {
        var bus = CreateBus();

        bus.WriteByte(0xC000, 0x56);
        bus.WriteByte(0xDFFF, 0x78);

        bus.ReadByte(0xC000).Should().Be(0x56);
        bus.ReadByte(0xDFFF).Should().Be(0x78);
    }

    [Fact]
    public void ReadWriteByte_MirrorsEchoRamToWorkRam()
    {
        var bus = CreateBus();

        bus.WriteByte(0xC000, 0x9A);
        bus.WriteByte(0xFDFF, 0xBC);

        bus.ReadByte(0xE000).Should().Be(0x9A);
        bus.ReadByte(0xDDFF).Should().Be(0xBC);
    }

    [Fact]
    public void ReadWriteByte_StoresCgbModeWorkRamBanksSelectedBySvbk()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.Cgb));

        bus.WriteByte(AddressMap.WorkRamSwitchableBankStart, 0x11);
        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x02);

        bus.ReadByte(AddressMap.WorkRamBankRegister).Should().Be(0xFA);
        bus.ReadByte(AddressMap.WorkRamSwitchableBankStart).Should().Be(0x00);

        bus.WriteByte(AddressMap.WorkRamSwitchableBankStart, 0x22);
        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x01);

        bus.ReadByte(AddressMap.WorkRamSwitchableBankStart).Should().Be(0x11);

        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x02);

        bus.ReadByte(AddressMap.WorkRamSwitchableBankStart).Should().Be(0x22);
    }

    [Fact]
    public void ReadWriteByte_KeepsFixedWorkRamBankAcrossSvbk()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.Cgb));

        bus.WriteByte(AddressMap.WorkRamStart, 0x44);
        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x07);

        bus.ReadByte(AddressMap.WorkRamStart).Should().Be(0x44);

        bus.WriteByte(AddressMap.WorkRamStart, 0x55);
        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x01);

        bus.ReadByte(AddressMap.WorkRamStart).Should().Be(0x55);
    }

    [Fact]
    public void ReadWriteByte_SvbkZeroReadsF8AndMapsBankOne()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.Cgb));

        bus.WriteByte(AddressMap.WorkRamSwitchableBankStart, 0x11);
        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x02);
        bus.WriteByte(AddressMap.WorkRamSwitchableBankStart, 0x22);
        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x00);

        bus.ReadByte(AddressMap.WorkRamBankRegister).Should().Be(0xF8);
        bus.ReadByte(AddressMap.WorkRamSwitchableBankStart).Should().Be(0x11);
    }

    [Fact]
    public void ReadWriteByte_SvbkSevenReadsFfAndMapsBankSeven()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.Cgb));

        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x07);
        bus.WriteByte(AddressMap.WorkRamSwitchableBankStart, 0x77);
        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x01);

        bus.ReadByte(AddressMap.WorkRamBankRegister).Should().Be(0xF9);
        bus.ReadByte(AddressMap.WorkRamSwitchableBankStart).Should().Be(0x00);

        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x07);

        bus.ReadByte(AddressMap.WorkRamBankRegister).Should().Be(0xFF);
        bus.ReadByte(AddressMap.WorkRamSwitchableBankStart).Should().Be(0x77);
    }

    [Fact]
    public void ReadWriteByte_MirrorsEchoRamThroughSelectedCgbWorkRamBank()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.Cgb));

        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x03);
        bus.WriteByte(0xF000, 0x33);
        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x01);

        bus.ReadByte(AddressMap.WorkRamSwitchableBankStart).Should().Be(0x00);

        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x03);

        bus.ReadByte(AddressMap.WorkRamSwitchableBankStart).Should().Be(0x33);
        bus.ReadByte(0xF000).Should().Be(0x33);
    }

    [Fact]
    public void ReadWriteByte_IgnoresSvbkInCgbDmgCompatibilityMode()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.DmgCompatibility));

        bus.WriteByte(AddressMap.WorkRamSwitchableBankStart, 0x12);
        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x07);

        bus.ReadByte(AddressMap.WorkRamBankRegister).Should().Be(0xFF);

        bus.WriteByte(AddressMap.WorkRamSwitchableBankStart, 0x34);
        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x01);

        bus.ReadByte(AddressMap.WorkRamSwitchableBankStart).Should().Be(0x34);
    }

    [Fact]
    public void ReadWriteByte_IgnoresSvbkOnDmg()
    {
        var bus = CreateBus();

        bus.WriteByte(AddressMap.WorkRamSwitchableBankStart, 0x12);
        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x07);

        bus.ReadByte(AddressMap.WorkRamBankRegister).Should().Be(0xFF);

        bus.WriteByte(AddressMap.WorkRamSwitchableBankStart, 0x34);
        bus.WriteByte(AddressMap.WorkRamBankRegister, 0x01);

        bus.ReadByte(AddressMap.WorkRamSwitchableBankStart).Should().Be(0x34);
    }

    [Fact]
    public void ReadWriteByte_StoresObjectAttributeMemory()
    {
        var bus = CreateBus();

        bus.WriteByte(0xFE00, 0xDE);
        bus.WriteByte(0xFE9F, 0xF0);

        bus.ReadByte(0xFE00).Should().Be(0xDE);
        bus.ReadByte(0xFE9F).Should().Be(0xF0);
    }

    [Fact]
    public void ReadWriteByte_BlocksVideoRamDuringPpuDrawingMode()
    {
        var bus = CreateBus();
        bus.WriteByte(AddressMap.VideoRamStart, 0x12);
        bus.WriteByte(AddressMap.LcdControlRegister, LcdEnable);
        bus.Ppu.Tick(80);

        bus.ReadByte(AddressMap.VideoRamStart).Should().Be(0xFF);

        bus.WriteByte(AddressMap.VideoRamStart, 0x34);
        bus.Ppu.Tick(172);

        bus.ReadByte(AddressMap.VideoRamStart).Should().Be(0x12);
    }

    [Fact]
    public void ReadWriteByte_BlocksObjectAttributeMemoryDuringPpuOamScanAndDrawingModes()
    {
        var bus = CreateBus();
        bus.WriteByte(AddressMap.ObjectAttributeMemoryStart, 0x12);
        bus.WriteByte(AddressMap.LcdControlRegister, LcdEnable);

        bus.ReadByte(AddressMap.ObjectAttributeMemoryStart).Should().Be(0x12);
        bus.WriteByte(AddressMap.ObjectAttributeMemoryStart, 0x34);

        bus.Ppu.Tick(80);
        bus.ReadByte(AddressMap.ObjectAttributeMemoryStart).Should().Be(0xFF);
        bus.WriteByte(AddressMap.ObjectAttributeMemoryStart, 0x56);

        bus.Ppu.Tick(172);

        bus.ReadByte(AddressMap.ObjectAttributeMemoryStart).Should().Be(0x34);

        bus.Ppu.Tick(204);

        bus.ReadByte(AddressMap.ObjectAttributeMemoryStart).Should().Be(0xFF);
        bus.WriteByte(AddressMap.ObjectAttributeMemoryStart, 0x78);

        bus.Ppu.Tick(252);

        bus.ReadByte(AddressMap.ObjectAttributeMemoryStart).Should().Be(0x34);
    }

    [Fact]
    public void ReadWriteByte_IgnoresNotUsableRange()
    {
        var bus = CreateBus();

        bus.WriteByte(0xFEA0, 0x12);
        bus.WriteByte(0xFEFF, 0x34);

        bus.ReadByte(0xFEA0).Should().Be(0x00);
        bus.ReadByte(0xFEFF).Should().Be(0x00);
    }

    [Fact]
    public void ReadWriteByte_KeepsNotUsableRangeBehaviorDuringPpuOamBlock()
    {
        var bus = CreateBus();
        bus.WriteByte(AddressMap.LcdControlRegister, LcdEnable);

        bus.WriteByte(AddressMap.NotUsableStart, 0x42);

        bus.ReadByte(AddressMap.NotUsableStart).Should().Be(0x00);
    }

    [Fact]
    public void ReadWriteByte_ReadsUnmappedIoRegistersHigh()
    {
        var bus = CreateBus();

        bus.WriteByte(0xFF03, 0x12);
        bus.WriteByte(0xFF7F, 0x34);

        bus.ReadByte(0xFF03).Should().Be(0xFF);
        bus.ReadByte(0xFF7F).Should().Be(0xFF);
    }

    [Fact]
    public void ReadWriteByte_RoutesJoypadRegister()
    {
        var bus = CreateBus();
        bus.WriteByte(AddressMap.JoypadRegister, 0x20);

        bus.Joypad.SetButtonState(JoypadButton.Right, pressed: true);

        bus.ReadByte(AddressMap.JoypadRegister).Should().Be(0xEE);
        bus.Interrupts.InterruptFlag.Should().Be(0b0001_0000);
    }

    [Fact]
    public void ReadWriteByte_RoutesSerialRegisters()
    {
        var bus = CreateBus();

        bus.WriteByte(AddressMap.SerialTransferDataRegister, 0x12);
        bus.WriteByte(AddressMap.SerialTransferControlRegister, 0x81);

        bus.ReadByte(AddressMap.SerialTransferDataRegister).Should().Be(0x12);
        bus.ReadByte(AddressMap.SerialTransferControlRegister).Should().Be(0xFF);

        TickMachineCycles(bus, 128 * 8);

        bus.ReadByte(AddressMap.SerialTransferDataRegister).Should().Be(0xFF);
        bus.ReadByte(AddressMap.SerialTransferControlRegister).Should().Be(0x7F);
        bus.Interrupts.InterruptFlag.Should().Be(0b0000_1000);
    }

    [Fact]
    public void ReadWriteByte_StoresHighRam()
    {
        var bus = CreateBus();

        bus.WriteByte(0xFF80, 0x56);
        bus.WriteByte(0xFFFE, 0x78);

        bus.ReadByte(0xFF80).Should().Be(0x56);
        bus.ReadByte(0xFFFE).Should().Be(0x78);
    }

    [Fact]
    public void ReadWriteByte_StoresInterruptEnableRegister()
    {
        var bus = CreateBus();

        bus.WriteByte(0xFFFF, 0xF1);

        bus.ReadByte(0xFFFF).Should().Be(0xF1);
        bus.Interrupts.InterruptEnable.Should().Be(0xF1);
    }

    [Fact]
    public void ReadWriteByte_RoutesInterruptFlagRegister()
    {
        var bus = CreateBus();

        bus.WriteByte(0xFF0F, 0xFF);

        bus.ReadByte(0xFF0F).Should().Be(0xFF);
        bus.Interrupts.InterruptFlag.Should().Be(0x1F);
    }

    [Fact]
    public void ReadWriteByte_RoutesTimerRegisters()
    {
        var bus = CreateBus();
        TickMachineCycles(bus, 64);

        bus.ReadByte(AddressMap.DividerRegister).Should().Be(0x01);

        bus.WriteByte(AddressMap.DividerRegister, 0xFF);
        bus.WriteByte(AddressMap.TimerCounterRegister, 0x12);
        bus.WriteByte(AddressMap.TimerModuloRegister, 0x34);
        bus.WriteByte(AddressMap.TimerControlRegister, 0b0000_0101);

        bus.ReadByte(AddressMap.DividerRegister).Should().Be(0x00);
        bus.ReadByte(AddressMap.TimerCounterRegister).Should().Be(0x12);
        bus.ReadByte(AddressMap.TimerModuloRegister).Should().Be(0x34);
        bus.ReadByte(AddressMap.TimerControlRegister).Should().Be(0b1111_1101);
    }

    [Fact]
    public void ReadWriteByte_RoutesKey1RegisterForCgbMode()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.Cgb));

        bus.ReadByte(AddressMap.Key1Register).Should().Be(0x7E);

        bus.WriteByte(AddressMap.Key1Register, 0xFF);

        bus.ReadByte(AddressMap.Key1Register).Should().Be(0x7F);

        bus.WriteByte(AddressMap.Key1Register, 0xFE);

        bus.ReadByte(AddressMap.Key1Register).Should().Be(0x7E);
    }

    [Fact]
    public void ReadWriteByte_IgnoresKey1RegisterOnDmg()
    {
        var bus = CreateBus();

        bus.WriteByte(AddressMap.Key1Register, 0x01);

        bus.ReadByte(AddressMap.Key1Register).Should().Be(0xFF);
        bus.Clock.CgbDoubleSpeed.Should().BeFalse();
    }

    [Fact]
    public void ReadWriteByte_IgnoresKey1RegisterInCgbDmgCompatibilityMode()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.DmgCompatibility));

        bus.WriteByte(AddressMap.Key1Register, 0x01);

        bus.ReadByte(AddressMap.Key1Register).Should().Be(0xFF);
        bus.Clock.CgbDoubleSpeed.Should().BeFalse();
    }

    [Fact]
    public void TickMachineCycle_TicksPpuAtTwoTCyclesInDoubleSpeed()
    {
        var bus = CreateBus(new CgbHardwareProfile(CgbOperatingMode.Cgb));
        var clock = new MachineClock(bus);
        bus.WriteByte(AddressMap.LcdControlRegister, 0x80);
        bus.WriteByte(AddressMap.Key1Register, 0x01);

        bus.Clock.TryStartSpeedSwitch().Should().BeTrue();

        TickMachineCycles(clock, 114);

        bus.ReadByte(AddressMap.LcdYCoordinateRegister).Should().Be(0x00);

        TickMachineCycles(clock, 114);

        bus.ReadByte(AddressMap.LcdYCoordinateRegister).Should().Be(0x01);
    }

    [Fact]
    public void WriteByte_DividerRegisterTicksApuDivApuOnFallingEdge()
    {
        var bus = CreateBus();
        bus.Clock.SetCounter(1 << 12);

        bus.WriteByte(AddressMap.DividerRegister, 0x00);

        bus.Apu.DivApuStep.Should().Be(1);
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

        bus.ReadByte(AddressMap.LcdControlRegister).Should().Be(0x91);
        bus.ReadByte(AddressMap.LcdStatusRegister).Should().Be(0xF8);
        bus.ReadByte(AddressMap.LcdYCoordinateRegister).Should().Be(0x42);
        bus.ReadByte(AddressMap.BackgroundPaletteRegister).Should().Be(0xFC);
        bus.ReadByte(AddressMap.ObjectPalette0Register).Should().Be(0xA5);
        bus.ReadByte(AddressMap.ObjectPalette1Register).Should().Be(0x5A);
    }

    [Fact]
    public void ReadWriteByte_RoutesDmaRegisterAndDefersOamCopy()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x1200] = 0x42);
        var bus = CreateBus(rom);

        bus.WriteByte(AddressMap.DmaRegister, 0x12);
        bus.TickDma(2);
        bus.TickDma(160);

        bus.ReadByte(AddressMap.ObjectAttributeMemoryStart).Should().Be(0x42);
    }

    [Fact]
    public void TickDma_CopiesFromRomWindow()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x1200] = 0x66);
        var bus = CreateBus(rom);

        bus.WriteByte(AddressMap.DmaRegister, 0x12);
        bus.TickDma(2);
        bus.TickDma(160);

        bus.ReadByte(AddressMap.ObjectAttributeMemoryStart).Should().Be(0x66);
    }

    [Fact]
    public void TickDma_CopiesFromVideoRam()
    {
        var bus = CreateBus();
        bus.WriteByte(AddressMap.VideoRamStart, 0x99);

        bus.WriteByte(AddressMap.DmaRegister, 0x80);
        bus.TickDma(2);
        bus.TickDma(160);

        bus.ReadByte(AddressMap.ObjectAttributeMemoryStart).Should().Be(0x99);
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

        bus.ReadByte(AddressMap.ObjectAttributeMemoryStart).Should().Be(0x42);
    }

    [Fact]
    public void TickDma_CopiesFromWorkRam()
    {
        var bus = CreateBus();
        bus.WriteByte(AddressMap.WorkRamStart, 0x42);

        bus.WriteByte(AddressMap.DmaRegister, 0xC0);
        bus.TickDma(2);
        bus.TickDma(160);

        bus.ReadByte(AddressMap.ObjectAttributeMemoryStart).Should().Be(0x42);
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

        bus.ReadByte(AddressMap.ObjectAttributeMemoryStart).Should().Be(0x42);
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

        bus.ReadByte(AddressMap.ObjectAttributeMemoryStart).Should().Be(0xFF);

        bus.Ppu.Tick(172);

        bus.ReadByte(AddressMap.ObjectAttributeMemoryStart).Should().Be(0x42);
    }

    [Fact]
    public void ReadByte_AllowsObjectAttributeMemoryDuringDmaStartupDelay()
    {
        var bus = CreateBus();
        bus.WriteByte(AddressMap.ObjectAttributeMemoryStart, 0x44);

        bus.WriteByte(AddressMap.DmaRegister, 0x80);

        bus.ReadByte(AddressMap.ObjectAttributeMemoryStart).Should().Be(0x44);

        bus.TickDma(1);
        bus.ReadByte(AddressMap.ObjectAttributeMemoryStart).Should().Be(0x44);

        bus.TickDma(1);
        bus.ReadByte(AddressMap.ObjectAttributeMemoryStart).Should().Be(0xFF);
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

        bus.ReadByte(AddressMap.RomStart).Should().Be(0x11);
        bus.ReadByte(AddressMap.VideoRamStart + 1).Should().Be(0x22);
        bus.ReadByte(AddressMap.WorkRamStart).Should().Be(0x33);
        bus.ReadByte(AddressMap.ObjectAttributeMemoryStart).Should().Be(0xFF);
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

        bus.ReadByte(AddressMap.RomStart).Should().Be(0x11);
        bus.ReadByte(AddressMap.ExternalRamStart).Should().Be(0x55);
        bus.ReadByte(AddressMap.WorkRamStart + 1).Should().Be(0x22);
        bus.ReadByte(AddressMap.ObjectAttributeMemoryStart).Should().Be(0xFF);
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

        bus.ReadByte(AddressMap.RomStart).Should().Be(0x66);
        bus.ReadByte(AddressMap.ExternalRamStart).Should().Be(0x66);
        bus.ReadByte(AddressMap.WorkRamStart).Should().Be(0x33);
        bus.ReadByte(AddressMap.ObjectAttributeMemoryStart).Should().Be(0xFF);
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

        bus.ReadByte(AddressMap.VideoRamStart).Should().Be(0xAA);
        bus.ReadByte(AddressMap.WorkRamStart + 1).Should().Be(0x33);
        bus.ReadByte(AddressMap.WorkRamStart + 2).Should().Be(0x44);
        bus.ReadByte(AddressMap.ObjectAttributeMemoryStart).Should().Be(0x42);
    }

    [Fact]
    public void ReadWriteByte_KeepsNotUsableRangeBehaviorDuringDma()
    {
        var bus = CreateBus();

        bus.WriteByte(AddressMap.DmaRegister, 0xC0);
        bus.WriteByte(AddressMap.NotUsableStart, 0x42);

        bus.ReadByte(AddressMap.NotUsableStart).Should().Be(0x00);
    }

    [Fact]
    public void ReadWriteByte_AllowsIoHighRamAndInterruptEnableDuringDma()
    {
        var bus = CreateBus();

        bus.WriteByte(AddressMap.DmaRegister, 0xC0);
        bus.WriteByte(0xFF03, 0x12);
        bus.WriteByte(AddressMap.HighRamStart, 0x34);
        bus.WriteByte(AddressMap.InterruptEnableRegister, 0x56);

        bus.ReadByte(AddressMap.DmaRegister).Should().Be(0xC0);
        bus.ReadByte(0xFF03).Should().Be(0xFF);
        bus.ReadByte(AddressMap.HighRamStart).Should().Be(0x34);
        bus.ReadByte(AddressMap.InterruptEnableRegister).Should().Be(0x56);
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

        bus.ReadByte(AddressMap.DmaRegister).Should().Be(0x90);
        bus.ReadByte(AddressMap.ObjectAttributeMemoryStart).Should().Be(0xD0);
    }

    [Fact]
    public void SetHardwareRegisterState_DmaRegisterDoesNotStartTransfer()
    {
        var bus = CreateBus();
        bus.WriteByte(AddressMap.VideoRamStart, 0x42);

        bus.SetHardwareRegisterState(AddressMap.DmaRegister, 0x80);
        bus.TickDma(160);

        bus.ReadByte(AddressMap.DmaRegister).Should().Be(0x80);
        bus.ReadByte(AddressMap.ObjectAttributeMemoryStart).Should().Be(0x00);
    }

    [Fact]
    public void SetHardwareRegisterState_SerialControlDoesNotStartTransfer()
    {
        var bus = CreateBus();

        bus.SetHardwareRegisterState(AddressMap.SerialTransferDataRegister, 0x00);
        bus.SetHardwareRegisterState(AddressMap.SerialTransferControlRegister, 0x81);
        TickMachineCycles(bus, 128 * 8);

        bus.ReadByte(AddressMap.SerialTransferDataRegister).Should().Be(0x00);
        bus.ReadByte(AddressMap.SerialTransferControlRegister).Should().Be(0xFF);
        bus.Interrupts.InterruptFlag.Should().Be(0x00);
    }

    [Fact]
    public void ReadWriteByte_ExternalRamIsUnmappedForRomOnlyCartridge()
    {
        var bus = CreateBus();

        bus.WriteByte(0xA000, 0x42);

        bus.ReadByte(0xA000).Should().Be(0xFF);
        bus.ReadByte(0xBFFF).Should().Be(0xFF);
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

        bus.ReadByte(AddressMap.ExternalRamStart).Should().Be(0x42);
    }

    [Fact]
    public void RestoreState_RejectsInvalidCgbMiscStateBeforeMutatingBusMemory()
    {
        var bus = CreateBus();
        bus.WriteByte(AddressMap.HighRamStart, 0x11);
        var state = bus.CaptureState();
        bus.WriteByte(AddressMap.HighRamStart, 0x22);

        FluentActions
            .Invoking(() =>
                bus.RestoreState(
                    state with
                    {
                        CgbMiscRegisters = new CgbMiscRegistersState(0, 0, 0, 0x01),
                    }
                )
            )
            .Should()
            .ThrowExactly<ArgumentException>();

        bus.ReadByte(AddressMap.HighRamStart).Should().Be(0x22);
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
