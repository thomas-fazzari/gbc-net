// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cartridges;
using GbcNet.Core.Cartridges.Memory;
using GbcNet.Core.Memory;

namespace GbcNet.Tests.Unit.Cartridges;

public sealed class Mbc3CartridgeTests
{
    private const int RomBankSize = Cartridge.FixedRomBankSize;

    [Theory]
    [InlineData(CartridgeType.Mbc3)]
    [InlineData(CartridgeType.Mbc3Ram)]
    [InlineData(CartridgeType.Mbc3RamBattery)]
    public void Load_AcceptsMbc3Cartridge(CartridgeType cartridgeType)
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0147] = (byte)cartridgeType);

        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.Header.CartridgeType.Should().Be(cartridgeType);
    }

    [Theory]
    [InlineData(CartridgeType.Mbc3TimerBattery)]
    [InlineData(CartridgeType.Mbc3TimerRamBattery)]
    public void Load_AcceptsMbc3TimerCartridges(CartridgeType cartridgeType)
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)cartridgeType;
            bytes[0x0149] =
                cartridgeType is CartridgeType.Mbc3TimerRamBattery ? (byte)0x03 : (byte)0;
        });

        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.Header.CartridgeType.Should().Be(cartridgeType);
    }

    [Fact]
    public void WriteRom_SwitchesMbc3RomBank()
    {
        var rom = TestRomFactory.Create(
            romSizeCode: 0x01,
            bytes =>
            {
                bytes[0x0147] = (byte)CartridgeType.Mbc3;
                bytes[1 * RomBankSize] = 0x11;
                bytes[2 * RomBankSize] = 0x22;
            }
        );
        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.WriteRom(0x2000, 0x02);

        cartridge.ReadRom(0x4000).Should().Be(0x22);
    }

    [Fact]
    public void WriteRom_TreatsMbc3RomBankZeroAsOne()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc3;
            bytes[0 * RomBankSize] = 0x00;
            bytes[1 * RomBankSize] = 0x11;
        });
        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.WriteRom(0x2000, 0x00);

        cartridge.ReadRom(0x4000).Should().Be(0x11);
    }

    [Fact]
    public void WriteRom_AllowsMbc3Banks20_40_60()
    {
        var rom = TestRomFactory.Create(
            romSizeCode: 0x06,
            bytes =>
            {
                bytes[0x0147] = (byte)CartridgeType.Mbc3;
                bytes[0x20 * RomBankSize] = 0x20;
                bytes[0x40 * RomBankSize] = 0x40;
                bytes[0x60 * RomBankSize] = 0x60;
            }
        );
        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.WriteRom(0x2000, 0x20);
        cartridge.ReadRom(0x4000).Should().Be(0x20);

        cartridge.WriteRom(0x2000, 0x40);
        cartridge.ReadRom(0x4000).Should().Be(0x40);

        cartridge.WriteRom(0x2000, 0x60);
        cartridge.ReadRom(0x4000).Should().Be(0x60);
    }

    [Fact]
    public void ReadWriteRam_RequiresMbc3RamEnable()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc3Ram;
            bytes[0x0149] = 0x02;
        });
        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);

        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(0xFF);

        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);

        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(0x42);
    }

    [Fact]
    public void ReadWriteRam_UsesMbc3RamBank()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc3Ram;
            bytes[0x0149] = 0x03;
        });
        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x11);
        cartridge.WriteRom(0x4000, 0x01);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x22);
        cartridge.WriteRom(0x4000, 0x00);

        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(0x11);

        cartridge.WriteRom(0x4000, 0x01);

        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(0x22);
    }

    [Fact]
    public void ReadRam_RtcRegisterSelectionReturnsFF()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc3Ram;
            bytes[0x0149] = 0x02;
        });
        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRom(0x4000, 0x08);

        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(0xFF);
    }

    [Fact]
    public void WriteRam_RtcRegisterSelectionDoesNotDirtyBatterySave()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc3RamBattery;
            bytes[0x0149] = 0x03;
        });
        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRom(0x4000, 0x08);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);

        cartridge.IsBatterySaveDirty.Should().BeFalse();
    }

    [Fact]
    public void ReadWriteRam_RequiresMbc3RtcEnable()
    {
        FakeClock clock = new();
        var cartridge = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, clock);

        SelectRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x2A);
        LatchRtc(cartridge);

        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(0xFF);

        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(0);

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x2A);
        LatchRtc(cartridge);

        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(42);
    }

    [Fact]
    public void ReadWriteRam_MapsMbc3RtcRegisters08Through0C()
    {
        FakeClock clock = new();
        var cartridge = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, clock);

        cartridge.WriteRom(0x0000, 0x0A);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister, 1);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.MinutesRegister, 2);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.HoursRegister, 3);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.DayLowRegister, 4);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.DayHighRegister, 0x41);
        LatchRtc(cartridge);

        ReadRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister).Should().Be(1);
        ReadRtcRegister(cartridge, Mbc3RealTimeClock.MinutesRegister).Should().Be(2);
        ReadRtcRegister(cartridge, Mbc3RealTimeClock.HoursRegister).Should().Be(3);
        ReadRtcRegister(cartridge, Mbc3RealTimeClock.DayLowRegister).Should().Be(4);
        ReadRtcRegister(cartridge, Mbc3RealTimeClock.DayHighRegister).Should().Be(0x41);
    }

    [Fact]
    public void WriteRam_Mbc3RtcStoresMaskedRegisterBits()
    {
        FakeClock clock = new();
        var cartridge = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, clock);

        cartridge.WriteRom(0x0000, 0x0A);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister, 0xFF);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.MinutesRegister, 0xFF);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.HoursRegister, 0xFF);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.DayHighRegister, 0xFF);
        LatchRtc(cartridge);

        ReadRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister).Should().Be(0x3F);
        ReadRtcRegister(cartridge, Mbc3RealTimeClock.MinutesRegister).Should().Be(0x3F);
        ReadRtcRegister(cartridge, Mbc3RealTimeClock.HoursRegister).Should().Be(0x1F);
        ReadRtcRegister(cartridge, Mbc3RealTimeClock.DayHighRegister).Should().Be(0xC1);
    }

    [Fact]
    public void WriteRom_LatchesMbc3RtcOnlyOnZeroToOneTransition()
    {
        FakeClock clock = new();
        var cartridge = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, clock);

        cartridge.WriteRom(0x0000, 0x0A);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister, 7);
        cartridge.WriteRom(0x6000, 0x01);

        ReadRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister).Should().Be(0);

        LatchRtc(cartridge);

        ReadRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister).Should().Be(7);
    }

    [Fact]
    public void ReadRam_Mbc3RtcLatchedValueStaysStableWhileClockAdvances()
    {
        FakeClock clock = new();
        var cartridge = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, clock);

        cartridge.WriteRom(0x0000, 0x0A);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister, 10);
        LatchRtc(cartridge);
        clock.UnixTimeSeconds += 5;

        ReadRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister).Should().Be(10);

        LatchRtc(cartridge);

        ReadRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister).Should().Be(15);
    }

    [Fact]
    public void ReadRam_Mbc3RtcTicksSecondsMinutesHoursDaysAndCarry()
    {
        FakeClock clock = new();
        var cartridge = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, clock);

        cartridge.WriteRom(0x0000, 0x0A);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister, 58);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.MinutesRegister, 59);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.HoursRegister, 23);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.DayLowRegister, 0xFF);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.DayHighRegister, 0x01);
        clock.UnixTimeSeconds += 2;
        LatchRtc(cartridge);

        ReadRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister).Should().Be(0);
        ReadRtcRegister(cartridge, Mbc3RealTimeClock.MinutesRegister).Should().Be(0);
        ReadRtcRegister(cartridge, Mbc3RealTimeClock.HoursRegister).Should().Be(0);
        ReadRtcRegister(cartridge, Mbc3RealTimeClock.DayLowRegister).Should().Be(0);
        ReadRtcRegister(cartridge, Mbc3RealTimeClock.DayHighRegister).Should().Be(0x80);
    }

    [Fact]
    public void ReadRam_Mbc3RtcHaltBitStopsClock()
    {
        FakeClock clock = new();
        var cartridge = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, clock);

        cartridge.WriteRom(0x0000, 0x0A);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister, 10);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.DayHighRegister, 0x40);
        clock.UnixTimeSeconds += 5;
        LatchRtc(cartridge);

        ReadRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister).Should().Be(10);
        ReadRtcRegister(cartridge, Mbc3RealTimeClock.DayHighRegister).Should().Be(0x40);
    }

    [Fact]
    public void BatterySave_ExportsAndImportsMbc3RamBanks()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc3RamBattery;
            bytes[0x0149] = 0x03;
        });
        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x11);
        cartridge.WriteRom(0x4000, 0x01);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x22);

        var save = cartridge.ExportBatterySave();

        cartridge.HasBatteryBackedSave.Should().BeTrue();
        cartridge.BatterySaveSize.Should().Be(32 * 1024);
        cartridge.IsBatterySaveDirty.Should().BeTrue();
        save[0].Should().Be(0x11);
        save[AddressMap.ExternalRamWindowSize].Should().Be(0x22);

        var reloaded = TestRomFactory.LoadCartridge(rom);
        var import = reloaded.TryImportBatterySave(save, out var errorMessage);

        import.Should().BeTrue(errorMessage);
        reloaded.IsBatterySaveDirty.Should().BeFalse();

        reloaded.WriteRom(0x0000, 0x0A);
        reloaded.ReadRam(AddressMap.ExternalRamStart).Should().Be(0x11);

        reloaded.WriteRom(0x4000, 0x01);
        reloaded.ReadRam(AddressMap.ExternalRamStart).Should().Be(0x22);
    }

    [Fact]
    public void BatterySave_ExportsAndImportsMbc3RamAndRtcState()
    {
        FakeClock clock = new();
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc3TimerRamBattery;
            bytes[0x0149] = 0x03;
        });
        var cartridge = TestRomFactory.LoadCartridge(rom, () => clock.UnixTimeSeconds);

        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x11);
        cartridge.WriteRom(0x4000, 0x01);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x22);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister, 12);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.MinutesRegister, 34);
        LatchRtc(cartridge);

        var save = cartridge.ExportBatterySave();

        cartridge.HasBatteryBackedSave.Should().BeTrue();
        cartridge.BatterySaveSize.Should().Be((32 * 1024) + Mbc3RealTimeClock.SaveStateSize);
        cartridge.IsBatterySaveDirty.Should().BeTrue();

        var reloaded = TestRomFactory.LoadCartridge(rom, () => clock.UnixTimeSeconds);
        var import = reloaded.TryImportBatterySave(save, out var errorMessage);

        import.Should().BeTrue(errorMessage);
        reloaded.IsBatterySaveDirty.Should().BeFalse();

        reloaded.WriteRom(0x0000, 0x0A);
        reloaded.ReadRam(AddressMap.ExternalRamStart).Should().Be(0x11);

        reloaded.WriteRom(0x4000, 0x01);
        reloaded.ReadRam(AddressMap.ExternalRamStart).Should().Be(0x22);
        ReadRtcRegister(reloaded, Mbc3RealTimeClock.SecondsRegister).Should().Be(12);
        ReadRtcRegister(reloaded, Mbc3RealTimeClock.MinutesRegister).Should().Be(34);
    }

    [Fact]
    public void BatterySave_Mbc3RtcExportUsesStandardRtcTailOffsets()
    {
        FakeClock clock = new() { UnixTimeSeconds = 123456 };
        var cartridge = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, clock);

        cartridge.WriteRom(0x0000, 0x0A);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister, 1);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.MinutesRegister, 2);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.HoursRegister, 3);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.DayLowRegister, 4);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.DayHighRegister, 0x41);
        LatchRtc(cartridge);

        var save = cartridge.ExportBatterySave();
        byte[] expectedTimestamp = [0x40, 0xE2, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00];

        save[0].Should().Be(1);
        save[4].Should().Be(2);
        save[8].Should().Be(3);
        save[12].Should().Be(4);
        save[16].Should().Be(0x41);
        save[20].Should().Be(1);
        save[24].Should().Be(2);
        save[28].Should().Be(3);
        save[32].Should().Be(4);
        save[36].Should().Be(0x41);
        save[40..48].Should().Equal(expectedTimestamp);
        save[1].Should().Be(0);
        save[19].Should().Be(0);
        save[21].Should().Be(0);
        save[39].Should().Be(0);
    }

    [Fact]
    public void BatterySave_Mbc3RtcImportIgnoresPaddingAndMasksRegisterBits()
    {
        FakeClock clock = new();
        var cartridge = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, clock);
        var save = cartridge.ExportBatterySave();
        const int latchedRtcOffset = 20;
        save[latchedRtcOffset] = 0xFF;
        save[latchedRtcOffset + 1] = 0xEE;
        save[latchedRtcOffset + 4] = 0xFF;
        save[latchedRtcOffset + 5] = 0xDD;
        save[latchedRtcOffset + 8] = 0xFF;
        save[latchedRtcOffset + 9] = 0xCC;
        save[latchedRtcOffset + 16] = 0xFF;
        save[latchedRtcOffset + 17] = 0xBB;
        var reloaded = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, clock);

        var import = reloaded.TryImportBatterySave(save, out var errorMessage);

        import.Should().BeTrue(errorMessage);
        reloaded.WriteRom(0x0000, 0x0A);
        ReadRtcRegister(reloaded, Mbc3RealTimeClock.SecondsRegister).Should().Be(0x3F);
        ReadRtcRegister(reloaded, Mbc3RealTimeClock.MinutesRegister).Should().Be(0x3F);
        ReadRtcRegister(reloaded, Mbc3RealTimeClock.HoursRegister).Should().Be(0x1F);
        ReadRtcRegister(reloaded, Mbc3RealTimeClock.DayHighRegister).Should().Be(0xC1);
    }

    [Fact]
    public void BatterySave_Mbc3TimerBatteryHasSaveWithoutExternalRam()
    {
        var cartridge = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, new FakeClock());

        cartridge.HasBatteryBackedSave.Should().BeTrue();
        cartridge.BatterySaveSize.Should().Be(Mbc3RealTimeClock.SaveStateSize);
        cartridge.ExportBatterySave().Should().NotBeEmpty();
    }

    [Fact]
    public void BatterySave_RejectsInvalidMbc3RtcSaveSize()
    {
        var cartridge = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, new FakeClock());

        var result = cartridge.TryImportBatterySave(new byte[1], out _);

        result.Should().BeFalse();
    }

    [Fact]
    public void BatterySave_RejectsInvalidMbc3SaveSize()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc3RamBattery;
            bytes[0x0149] = 0x02;
        });
        var cartridge = TestRomFactory.LoadCartridge(rom);

        var result = cartridge.TryImportBatterySave(new byte[1], out _);

        result.Should().BeFalse();
    }

    [Fact]
    public void CaptureRestore_PreservesVolatileRamBanksRomSelectionAndEnableState()
    {
        var rom = TestRomFactory.Create(
            romSizeCode: 0x01,
            bytes =>
            {
                bytes[0x0147] = (byte)CartridgeType.Mbc3Ram;
                bytes[0x0149] = 0x03;
                bytes[2 * RomBankSize] = 0x22;
            }
        );
        var source = TestRomFactory.LoadCartridge(rom);

        source.WriteRom(0x0000, 0x0A);
        source.WriteRam(AddressMap.ExternalRamStart, 0x11);
        source.WriteRom(0x4000, 0x01);
        source.WriteRam(AddressMap.ExternalRamStart, 0x33);
        source.WriteRom(0x2000, 0x02);
        source.WriteRom(0x0000, 0x00);

        var restored = TestRomFactory.LoadCartridge(rom);
        restored.RestoreState(source.CaptureState());

        restored.ReadRom(0x4000).Should().Be(0x22);
        restored.ReadRam(AddressMap.ExternalRamStart).Should().Be(0xFF);

        restored.WriteRom(0x0000, 0x0A);
        restored.ReadRam(AddressMap.ExternalRamStart).Should().Be(0x33);
        restored.WriteRom(0x4000, 0x00);
        restored.ReadRam(AddressMap.ExternalRamStart).Should().Be(0x11);
    }

    [Fact]
    public void CaptureRestore_PreservesUnsupportedMbc3RamSelector()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc3Ram;
            bytes[0x0149] = 0x02;
        });
        var source = TestRomFactory.LoadCartridge(rom);

        source.WriteRom(0x0000, 0x0A);
        source.WriteRam(AddressMap.ExternalRamStart, 0x5A);
        source.WriteRom(0x4000, 0x0D);

        var restored = TestRomFactory.LoadCartridge(rom);
        restored.RestoreState(source.CaptureState());

        restored.ReadRam(AddressMap.ExternalRamStart).Should().Be(0xFF);
        restored.WriteRam(AddressMap.ExternalRamStart, 0x99);
        restored.WriteRom(0x4000, 0x00);
        restored.ReadRam(AddressMap.ExternalRamStart).Should().Be(0x5A);
    }

    [Fact]
    public void CaptureRestore_RtcPreservesDistinctLiveAndLatchedRegisters()
    {
        FakeClock sourceClock = new();
        var source = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, sourceClock);
        source.WriteRom(0x0000, 0x0A);
        WriteRtcRegister(source, Mbc3RealTimeClock.SecondsRegister, 10);
        LatchRtc(source);
        sourceClock.UnixTimeSeconds += 5;
        source.IsBatterySaveDirty.Should().BeTrue();

        FakeClock destinationClock = new() { UnixTimeSeconds = 1000 };
        var restored = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, destinationClock);
        restored.RestoreState(source.CaptureState());

        ReadRtcRegister(restored, Mbc3RealTimeClock.SecondsRegister).Should().Be(10);
        LatchRtc(restored);
        ReadRtcRegister(restored, Mbc3RealTimeClock.SecondsRegister).Should().Be(15);
    }

    [Fact]
    public void CaptureRestore_RtcPreservesArmedLatchTransition()
    {
        FakeClock clock = new();
        var source = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, clock);
        source.WriteRom(0x0000, 0x0A);
        WriteRtcRegister(source, Mbc3RealTimeClock.SecondsRegister, 7);
        LatchRtc(source);
        WriteRtcRegister(source, Mbc3RealTimeClock.SecondsRegister, 9);
        source.WriteRom(0x6000, 0x00);

        var restored = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, clock);
        restored.RestoreState(source.CaptureState());
        restored.WriteRom(0x6000, 0x01);

        ReadRtcRegister(restored, Mbc3RealTimeClock.SecondsRegister).Should().Be(9);
    }

    [Fact]
    public void CaptureRestore_RtcPreservesUnarmedLatchTransition()
    {
        FakeClock clock = new();
        var source = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, clock);
        source.WriteRom(0x0000, 0x0A);
        WriteRtcRegister(source, Mbc3RealTimeClock.SecondsRegister, 7);
        source.WriteRom(0x6000, 0x01);

        var restored = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, clock);
        restored.RestoreState(source.CaptureState());
        WriteRtcRegister(restored, Mbc3RealTimeClock.SecondsRegister, 9);
        restored.WriteRom(0x6000, 0x01);

        ReadRtcRegister(restored, Mbc3RealTimeClock.SecondsRegister).Should().Be(0);
    }

    [Fact]
    public void RestoreState_RejectsRtcCapabilityMismatchWithoutMutation()
    {
        FakeClock clock = new();
        var timerCartridge = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, clock);
        var incompatibleState = timerCartridge.CaptureState();
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc3Ram;
            bytes[0x0149] = 0x02;
        });
        var cartridge = TestRomFactory.LoadCartridge(rom);
        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x5A);

        FluentActions
            .Invoking(() => cartridge.RestoreState(incompatibleState))
            .Should()
            .ThrowExactly<ArgumentException>();
        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(0x5A);
    }

    [Fact]
    public void RestoreState_RejectsContradictoryEnableStateWithoutMutation()
    {
        var rom = TestRomFactory.Create(
            romSizeCode: 0x01,
            bytes =>
            {
                bytes[0x0147] = (byte)CartridgeType.Mbc3Ram;
                bytes[0x0149] = 0x02;
                bytes[2 * RomBankSize] = 0x22;
            }
        );
        var source = TestRomFactory.LoadCartridge(rom);
        source.WriteRom(0x0000, 0x0A);
        var state = source.CaptureState();
        var mbc3State = (Mbc3MemoryControllerState)state.Controller;
        var contradictoryState = new CartridgeState(mbc3State with { RamAndTimerEnabled = false });
        var cartridge = TestRomFactory.LoadCartridge(rom);
        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x5A);
        cartridge.WriteRom(0x2000, 0x02);

        FluentActions
            .Invoking(() => cartridge.RestoreState(contradictoryState))
            .Should()
            .ThrowExactly<ArgumentException>();
        cartridge.ReadRom(0x4000).Should().Be(0x22);
        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(0x5A);
    }

    [Fact]
    public void RestoreState_RejectsOutOfRangeRtcWithoutMutation()
    {
        FakeClock clock = new();
        var source = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, clock);
        var state = source.CaptureState();
        var mbc3State = (Mbc3MemoryControllerState)state.Controller;
        var realTimeClock =
            mbc3State.RealTimeClock
            ?? throw new InvalidOperationException("RTC state was not captured.");
        var invalidState = new CartridgeState(
            mbc3State with
            {
                RealTimeClock = realTimeClock with
                {
                    Live = realTimeClock.Live with { Seconds = 0x40 },
                },
            }
        );
        var cartridge = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, clock);
        cartridge.WriteRom(0x0000, 0x0A);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister, 7);
        LatchRtc(cartridge);

        FluentActions
            .Invoking(() => cartridge.RestoreState(invalidState))
            .Should()
            .ThrowExactly<ArgumentException>();
        ReadRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister).Should().Be(7);
    }

    private static Cartridge LoadMbc3TimerCartridge(CartridgeType cartridgeType, FakeClock clock)
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0147] = (byte)cartridgeType);
        return TestRomFactory.LoadCartridge(rom, () => clock.UnixTimeSeconds);
    }

    private static void WriteRtcRegister(Cartridge cartridge, byte register, byte value)
    {
        SelectRtcRegister(cartridge, register);
        cartridge.WriteRam(AddressMap.ExternalRamStart, value);
    }

    private static byte ReadRtcRegister(Cartridge cartridge, byte register)
    {
        SelectRtcRegister(cartridge, register);
        return cartridge.ReadRam(AddressMap.ExternalRamStart);
    }

    private static void SelectRtcRegister(Cartridge cartridge, byte register)
    {
        cartridge.WriteRom(0x4000, register);
    }

    private static void LatchRtc(Cartridge cartridge)
    {
        cartridge.WriteRom(0x6000, 0x00);
        cartridge.WriteRom(0x6000, 0x01);
    }

    private sealed class FakeClock
    {
        public long UnixTimeSeconds { get; set; }
    }
}
