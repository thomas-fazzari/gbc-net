using FluentResults;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Cartridges.Memory;
using GbcNet.Core.Memory;

namespace GbcNet.Tests.Cartridges;

public sealed class Mbc3CartridgeTests
{
    private const int RomBankSize = Cartridge.FixedRomBankSize;

    [Theory]
    [InlineData(CartridgeType.Mbc3)]
    [InlineData(CartridgeType.Mbc3Ram)]
    [InlineData(CartridgeType.Mbc3RamBattery)]
    public void Load_AcceptsMbc3Cartridge(CartridgeType cartridgeType)
    {
        byte[] rom = TestRomFactory.Create(bytes => bytes[0x0147] = (byte)cartridgeType);

        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        Assert.Equal(cartridgeType, cartridge.Header.CartridgeType);
    }

    [Theory]
    [InlineData(CartridgeType.Mbc3TimerBattery)]
    [InlineData(CartridgeType.Mbc3TimerRamBattery)]
    public void Load_AcceptsMbc3TimerCartridges(CartridgeType cartridgeType)
    {
        byte[] rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)cartridgeType;
            bytes[0x0149] =
                cartridgeType is CartridgeType.Mbc3TimerRamBattery ? (byte)0x03 : (byte)0;
        });

        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        Assert.Equal(cartridgeType, cartridge.Header.CartridgeType);
    }

    [Fact]
    public void WriteRom_SwitchesMbc3RomBank()
    {
        byte[] rom = TestRomFactory.Create(
            romSizeCode: 0x01,
            bytes =>
            {
                bytes[0x0147] = (byte)CartridgeType.Mbc3;
                bytes[1 * RomBankSize] = 0x11;
                bytes[2 * RomBankSize] = 0x22;
            }
        );
        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x2000, 0x02);

        Assert.Equal(0x22, cartridge.ReadRom(0x4000));
    }

    [Fact]
    public void WriteRom_TreatsMbc3RomBankZeroAsOne()
    {
        byte[] rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc3;
            bytes[0 * RomBankSize] = 0x00;
            bytes[1 * RomBankSize] = 0x11;
        });
        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x2000, 0x00);

        Assert.Equal(0x11, cartridge.ReadRom(0x4000));
    }

    [Fact]
    public void WriteRom_AllowsMbc3Banks20_40_60()
    {
        byte[] rom = TestRomFactory.Create(
            romSizeCode: 0x06,
            bytes =>
            {
                bytes[0x0147] = (byte)CartridgeType.Mbc3;
                bytes[0x20 * RomBankSize] = 0x20;
                bytes[0x40 * RomBankSize] = 0x40;
                bytes[0x60 * RomBankSize] = 0x60;
            }
        );
        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x2000, 0x20);
        Assert.Equal(0x20, cartridge.ReadRom(0x4000));

        cartridge.WriteRom(0x2000, 0x40);
        Assert.Equal(0x40, cartridge.ReadRom(0x4000));

        cartridge.WriteRom(0x2000, 0x60);
        Assert.Equal(0x60, cartridge.ReadRom(0x4000));
    }

    [Fact]
    public void ReadWriteRam_RequiresMbc3RamEnable()
    {
        byte[] rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc3Ram;
            bytes[0x0149] = 0x02;
        });
        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);

        Assert.Equal(0xFF, cartridge.ReadRam(AddressMap.ExternalRamStart));

        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);

        Assert.Equal(0x42, cartridge.ReadRam(AddressMap.ExternalRamStart));
    }

    [Fact]
    public void ReadWriteRam_UsesMbc3RamBank()
    {
        byte[] rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc3Ram;
            bytes[0x0149] = 0x03;
        });
        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x11);
        cartridge.WriteRom(0x4000, 0x01);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x22);
        cartridge.WriteRom(0x4000, 0x00);

        Assert.Equal(0x11, cartridge.ReadRam(AddressMap.ExternalRamStart));

        cartridge.WriteRom(0x4000, 0x01);

        Assert.Equal(0x22, cartridge.ReadRam(AddressMap.ExternalRamStart));
    }

    [Fact]
    public void ReadRam_RtcRegisterSelectionReturnsFF()
    {
        byte[] rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc3Ram;
            bytes[0x0149] = 0x02;
        });
        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRom(0x4000, 0x08);

        Assert.Equal(0xFF, cartridge.ReadRam(AddressMap.ExternalRamStart));
    }

    [Fact]
    public void WriteRam_RtcRegisterSelectionDoesNotDirtyBatterySave()
    {
        byte[] rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc3RamBattery;
            bytes[0x0149] = 0x03;
        });
        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRom(0x4000, 0x08);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);

        Assert.False(cartridge.IsBatterySaveDirty);
    }

    [Fact]
    public void ReadWriteRam_RequiresMbc3RtcEnable()
    {
        FakeClock clock = new();
        Cartridge cartridge = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, clock);

        SelectRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x2A);
        LatchRtc(cartridge);

        Assert.Equal(0xFF, cartridge.ReadRam(AddressMap.ExternalRamStart));

        cartridge.WriteRom(0x0000, 0x0A);
        Assert.Equal(0, cartridge.ReadRam(AddressMap.ExternalRamStart));

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x2A);
        LatchRtc(cartridge);

        Assert.Equal(42, cartridge.ReadRam(AddressMap.ExternalRamStart));
    }

    [Fact]
    public void ReadWriteRam_MapsMbc3RtcRegisters08Through0C()
    {
        FakeClock clock = new();
        Cartridge cartridge = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, clock);

        cartridge.WriteRom(0x0000, 0x0A);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister, 1);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.MinutesRegister, 2);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.HoursRegister, 3);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.DayLowRegister, 4);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.DayHighRegister, 0x41);
        LatchRtc(cartridge);

        Assert.Equal(1, ReadRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister));
        Assert.Equal(2, ReadRtcRegister(cartridge, Mbc3RealTimeClock.MinutesRegister));
        Assert.Equal(3, ReadRtcRegister(cartridge, Mbc3RealTimeClock.HoursRegister));
        Assert.Equal(4, ReadRtcRegister(cartridge, Mbc3RealTimeClock.DayLowRegister));
        Assert.Equal(0x41, ReadRtcRegister(cartridge, Mbc3RealTimeClock.DayHighRegister));
    }

    [Fact]
    public void WriteRam_Mbc3RtcStoresMaskedRegisterBits()
    {
        FakeClock clock = new();
        Cartridge cartridge = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, clock);

        cartridge.WriteRom(0x0000, 0x0A);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister, 0xFF);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.MinutesRegister, 0xFF);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.HoursRegister, 0xFF);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.DayHighRegister, 0xFF);
        LatchRtc(cartridge);

        Assert.Equal(0x3F, ReadRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister));
        Assert.Equal(0x3F, ReadRtcRegister(cartridge, Mbc3RealTimeClock.MinutesRegister));
        Assert.Equal(0x1F, ReadRtcRegister(cartridge, Mbc3RealTimeClock.HoursRegister));
        Assert.Equal(0xC1, ReadRtcRegister(cartridge, Mbc3RealTimeClock.DayHighRegister));
    }

    [Fact]
    public void WriteRom_LatchesMbc3RtcOnlyOnZeroToOneTransition()
    {
        FakeClock clock = new();
        Cartridge cartridge = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, clock);

        cartridge.WriteRom(0x0000, 0x0A);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister, 7);
        cartridge.WriteRom(0x6000, 0x01);

        Assert.Equal(0, ReadRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister));

        LatchRtc(cartridge);

        Assert.Equal(7, ReadRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister));
    }

    [Fact]
    public void ReadRam_Mbc3RtcLatchedValueStaysStableWhileClockAdvances()
    {
        FakeClock clock = new();
        Cartridge cartridge = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, clock);

        cartridge.WriteRom(0x0000, 0x0A);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister, 10);
        LatchRtc(cartridge);
        clock.UnixTimeSeconds += 5;

        Assert.Equal(10, ReadRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister));

        LatchRtc(cartridge);

        Assert.Equal(15, ReadRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister));
    }

    [Fact]
    public void ReadRam_Mbc3RtcTicksSecondsMinutesHoursDaysAndCarry()
    {
        FakeClock clock = new();
        Cartridge cartridge = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, clock);

        cartridge.WriteRom(0x0000, 0x0A);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister, 58);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.MinutesRegister, 59);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.HoursRegister, 23);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.DayLowRegister, 0xFF);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.DayHighRegister, 0x01);
        clock.UnixTimeSeconds += 2;
        LatchRtc(cartridge);

        Assert.Equal(0, ReadRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister));
        Assert.Equal(0, ReadRtcRegister(cartridge, Mbc3RealTimeClock.MinutesRegister));
        Assert.Equal(0, ReadRtcRegister(cartridge, Mbc3RealTimeClock.HoursRegister));
        Assert.Equal(0, ReadRtcRegister(cartridge, Mbc3RealTimeClock.DayLowRegister));
        Assert.Equal(0x80, ReadRtcRegister(cartridge, Mbc3RealTimeClock.DayHighRegister));
    }

    [Fact]
    public void ReadRam_Mbc3RtcHaltBitStopsClock()
    {
        FakeClock clock = new();
        Cartridge cartridge = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, clock);

        cartridge.WriteRom(0x0000, 0x0A);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister, 10);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.DayHighRegister, 0x40);
        clock.UnixTimeSeconds += 5;
        LatchRtc(cartridge);

        Assert.Equal(10, ReadRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister));
        Assert.Equal(0x40, ReadRtcRegister(cartridge, Mbc3RealTimeClock.DayHighRegister));
    }

    [Fact]
    public void BatterySave_ExportsAndImportsMbc3RamBanks()
    {
        byte[] rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc3RamBattery;
            bytes[0x0149] = 0x03;
        });
        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x11);
        cartridge.WriteRom(0x4000, 0x01);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x22);

        byte[] save = cartridge.ExportBatterySave();

        Assert.True(cartridge.HasBatteryBackedSave);
        Assert.Equal(32 * 1024, cartridge.BatterySaveSize);
        Assert.True(cartridge.IsBatterySaveDirty);
        Assert.Equal(0x11, save[0]);
        Assert.Equal(0x22, save[AddressMap.ExternalRamWindowSize]);

        Cartridge reloaded = ResultAssertions.AssertSuccess(Cartridge.Load(rom));
        Result import = reloaded.ImportBatterySave(save);

        Assert.True(
            import.IsSuccess,
            string.Join(Environment.NewLine, import.Errors.Select(error => error.Message))
        );
        Assert.False(reloaded.IsBatterySaveDirty);

        reloaded.WriteRom(0x0000, 0x0A);
        Assert.Equal(0x11, reloaded.ReadRam(AddressMap.ExternalRamStart));

        reloaded.WriteRom(0x4000, 0x01);
        Assert.Equal(0x22, reloaded.ReadRam(AddressMap.ExternalRamStart));
    }

    [Fact]
    public void BatterySave_ExportsAndImportsMbc3RamAndRtcState()
    {
        FakeClock clock = new();
        byte[] rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc3TimerRamBattery;
            bytes[0x0149] = 0x03;
        });
        Cartridge cartridge = ResultAssertions.AssertSuccess(
            Cartridge.Load(rom, () => clock.UnixTimeSeconds)
        );

        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x11);
        cartridge.WriteRom(0x4000, 0x01);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x22);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister, 12);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.MinutesRegister, 34);
        LatchRtc(cartridge);

        byte[] save = cartridge.ExportBatterySave();

        Assert.True(cartridge.HasBatteryBackedSave);
        Assert.Equal((32 * 1024) + Mbc3RealTimeClock.SaveStateSize, cartridge.BatterySaveSize);
        Assert.True(cartridge.IsBatterySaveDirty);

        Cartridge reloaded = ResultAssertions.AssertSuccess(
            Cartridge.Load(rom, () => clock.UnixTimeSeconds)
        );
        Result import = reloaded.ImportBatterySave(save);

        Assert.True(
            import.IsSuccess,
            string.Join(Environment.NewLine, import.Errors.Select(error => error.Message))
        );
        Assert.False(reloaded.IsBatterySaveDirty);

        reloaded.WriteRom(0x0000, 0x0A);
        Assert.Equal(0x11, reloaded.ReadRam(AddressMap.ExternalRamStart));

        reloaded.WriteRom(0x4000, 0x01);
        Assert.Equal(0x22, reloaded.ReadRam(AddressMap.ExternalRamStart));
        Assert.Equal(12, ReadRtcRegister(reloaded, Mbc3RealTimeClock.SecondsRegister));
        Assert.Equal(34, ReadRtcRegister(reloaded, Mbc3RealTimeClock.MinutesRegister));
    }

    [Fact]
    public void BatterySave_Mbc3RtcExportUsesStandardRtcTailOffsets()
    {
        FakeClock clock = new() { UnixTimeSeconds = 123456 };
        Cartridge cartridge = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, clock);

        cartridge.WriteRom(0x0000, 0x0A);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.SecondsRegister, 1);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.MinutesRegister, 2);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.HoursRegister, 3);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.DayLowRegister, 4);
        WriteRtcRegister(cartridge, Mbc3RealTimeClock.DayHighRegister, 0x41);
        LatchRtc(cartridge);

        byte[] save = cartridge.ExportBatterySave();
        byte[] expectedTimestamp = [0x40, 0xE2, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00];

        Assert.Equal(1, save[0]);
        Assert.Equal(2, save[4]);
        Assert.Equal(3, save[8]);
        Assert.Equal(4, save[12]);
        Assert.Equal(0x41, save[16]);
        Assert.Equal(1, save[20]);
        Assert.Equal(2, save[24]);
        Assert.Equal(3, save[28]);
        Assert.Equal(4, save[32]);
        Assert.Equal(0x41, save[36]);
        Assert.Equal(expectedTimestamp, save[40..48]);
        Assert.Equal(0, save[1]);
        Assert.Equal(0, save[19]);
        Assert.Equal(0, save[21]);
        Assert.Equal(0, save[39]);
    }

    [Fact]
    public void BatterySave_Mbc3RtcImportIgnoresPaddingAndMasksRegisterBits()
    {
        FakeClock clock = new();
        Cartridge cartridge = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, clock);
        byte[] save = cartridge.ExportBatterySave();
        const int latchedRtcOffset = 20;
        save[latchedRtcOffset] = 0xFF;
        save[latchedRtcOffset + 1] = 0xEE;
        save[latchedRtcOffset + 4] = 0xFF;
        save[latchedRtcOffset + 5] = 0xDD;
        save[latchedRtcOffset + 8] = 0xFF;
        save[latchedRtcOffset + 9] = 0xCC;
        save[latchedRtcOffset + 16] = 0xFF;
        save[latchedRtcOffset + 17] = 0xBB;
        Cartridge reloaded = LoadMbc3TimerCartridge(CartridgeType.Mbc3TimerBattery, clock);

        Result import = reloaded.ImportBatterySave(save);

        Assert.True(
            import.IsSuccess,
            string.Join(Environment.NewLine, import.Errors.Select(error => error.Message))
        );
        reloaded.WriteRom(0x0000, 0x0A);
        Assert.Equal(0x3F, ReadRtcRegister(reloaded, Mbc3RealTimeClock.SecondsRegister));
        Assert.Equal(0x3F, ReadRtcRegister(reloaded, Mbc3RealTimeClock.MinutesRegister));
        Assert.Equal(0x1F, ReadRtcRegister(reloaded, Mbc3RealTimeClock.HoursRegister));
        Assert.Equal(0xC1, ReadRtcRegister(reloaded, Mbc3RealTimeClock.DayHighRegister));
    }

    [Fact]
    public void BatterySave_Mbc3TimerBatteryHasSaveWithoutExternalRam()
    {
        Cartridge cartridge = LoadMbc3TimerCartridge(
            CartridgeType.Mbc3TimerBattery,
            new FakeClock()
        );

        Assert.True(cartridge.HasBatteryBackedSave);
        Assert.Equal(Mbc3RealTimeClock.SaveStateSize, cartridge.BatterySaveSize);
        Assert.NotEmpty(cartridge.ExportBatterySave());
    }

    [Fact]
    public void BatterySave_RejectsInvalidMbc3RtcSaveSize()
    {
        Cartridge cartridge = LoadMbc3TimerCartridge(
            CartridgeType.Mbc3TimerBattery,
            new FakeClock()
        );

        Result result = cartridge.ImportBatterySave(new byte[1]);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void BatterySave_RejectsInvalidMbc3SaveSize()
    {
        byte[] rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc3RamBattery;
            bytes[0x0149] = 0x02;
        });
        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        Result result = cartridge.ImportBatterySave(new byte[1]);

        Assert.True(result.IsFailed);
    }

    private static Cartridge LoadMbc3TimerCartridge(CartridgeType cartridgeType, FakeClock clock)
    {
        byte[] rom = TestRomFactory.Create(bytes => bytes[0x0147] = (byte)cartridgeType);
        return ResultAssertions.AssertSuccess(Cartridge.Load(rom, () => clock.UnixTimeSeconds));
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
