// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cartridges;
using GbcNet.Core.Memory;

namespace GbcNet.Tests.Cartridges;

public sealed class Mbc1CartridgeTests
{
    private const int RomBankSize = Cartridge.FixedRomBankSize;

    [Fact]
    public void Load_AcceptsMbc1Cartridge()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0147] = (byte)CartridgeType.Mbc1);

        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        Assert.Equal(CartridgeType.Mbc1, cartridge.Header.CartridgeType);
    }

    [Fact]
    public void ReadRom_MapsSwitchableAreaToBankOneByDefault()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc1;
            bytes[RomBankSize] = 0x42;
        });
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        Assert.Equal(0x42, cartridge.ReadRom(0x4000));
    }

    [Fact]
    public void WriteRom_SwitchesMbc1RomBank()
    {
        var rom = TestRomFactory.Create(
            romSizeCode: 0x01,
            bytes =>
            {
                bytes[0x0147] = (byte)CartridgeType.Mbc1;
                bytes[1 * RomBankSize] = 0x11;
                bytes[2 * RomBankSize] = 0x22;
            }
        );
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x2000, 0x02);

        Assert.Equal(0x22, cartridge.ReadRom(0x4000));
    }

    [Fact]
    public void WriteRom_TreatsMbc1LowRomBankZeroAsOneBeforeMasking()
    {
        var rom = TestRomFactory.Create(
            romSizeCode: 0x01,
            bytes =>
            {
                bytes[0x0147] = (byte)CartridgeType.Mbc1;
                bytes[0 * RomBankSize] = 0x00;
                bytes[1 * RomBankSize] = 0x11;
            }
        );
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x2000, 0x00);

        Assert.Equal(0x11, cartridge.ReadRom(0x4000));
    }

    [Fact]
    public void WriteRom_UsesFullMbc1LowRomBankRegisterBeforeRomBankMask()
    {
        var rom = TestRomFactory.Create(
            romSizeCode: 0x01,
            bytes =>
            {
                bytes[0x0147] = (byte)CartridgeType.Mbc1;
                bytes[0 * RomBankSize] = 0x00;
                bytes[1 * RomBankSize] = 0x11;
            }
        );
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x2000, 0x10);

        Assert.Equal(0x00, cartridge.ReadRom(0x4000));
    }

    [Fact]
    public void WriteRom_UsesMbc1HighBankBitsForLargeRom()
    {
        const int bank1 = 0x01;
        const int bank21 = 0x21;

        var rom = TestRomFactory.Create(
            romSizeCode: 0x05,
            bytes =>
            {
                bytes[0x0147] = (byte)CartridgeType.Mbc1;
                bytes[bank1 * RomBankSize] = 0x11;
                bytes[bank21 * RomBankSize] = 0x21;
            }
        );
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x4000, 0x01);

        Assert.Equal(0x21, cartridge.ReadRom(0x4000));
    }

    [Fact]
    public void WriteRom_UsesMbc1AdvancedModeForFixedRomArea()
    {
        const int bank20 = 0x20;

        var rom = TestRomFactory.Create(
            romSizeCode: 0x05,
            bytes =>
            {
                bytes[0x0147] = (byte)CartridgeType.Mbc1;
                bytes[AddressMap.CartridgeEntryPointAddress] = 0x00;
                bytes[(bank20 * RomBankSize) + AddressMap.CartridgeEntryPointAddress] = 0x20;
            }
        );
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x4000, 0x01);
        cartridge.WriteRom(0x6000, 0x01);

        Assert.Equal(0x20, cartridge.ReadRom(AddressMap.CartridgeEntryPointAddress));
    }

    [Fact]
    public void ReadWriteRam_RequiresMbc1RamEnable()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc1Ram;
            bytes[0x0149] = 0x02;
        });
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);

        Assert.Equal(0xFF, cartridge.ReadRam(AddressMap.ExternalRamStart));

        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);

        Assert.Equal(0x42, cartridge.ReadRam(AddressMap.ExternalRamStart));
    }

    [Fact]
    public void ReadWriteRam_UsesMbc1AdvancedModeRamBank()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc1Ram;
            bytes[0x0149] = 0x03;
        });
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRom(0x6000, 0x01);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x11);
        cartridge.WriteRom(0x4000, 0x01);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x22);
        cartridge.WriteRom(0x4000, 0x00);

        Assert.Equal(0x11, cartridge.ReadRam(AddressMap.ExternalRamStart));

        cartridge.WriteRom(0x4000, 0x01);

        Assert.Equal(0x22, cartridge.ReadRam(AddressMap.ExternalRamStart));
    }

    [Fact]
    public void BatterySave_IsUnavailableForMbc1RamWithoutBattery()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc1Ram;
            bytes[0x0149] = 0x02;
        });
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);

        Assert.False(cartridge.HasBatteryBackedSave);
        Assert.Equal(0, cartridge.BatterySaveSize);
        Assert.False(cartridge.IsBatterySaveDirty);
        Assert.Empty(cartridge.ExportBatterySave());
    }

    [Fact]
    public void BatterySave_ExportsMbc1RamBanksAndTracksDirty()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc1RamBattery;
            bytes[0x0149] = 0x03;
        });
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRom(0x6000, 0x01);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x11);
        cartridge.WriteRom(0x4000, 0x01);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x22);

        var save = cartridge.ExportBatterySave();

        Assert.True(cartridge.HasBatteryBackedSave);
        Assert.Equal(32 * 1024, cartridge.BatterySaveSize);
        Assert.True(cartridge.IsBatterySaveDirty);
        Assert.Equal(0x11, save[0]);
        Assert.Equal(0x22, save[AddressMap.ExternalRamWindowSize]);

        save[0] = 0x99;
        Assert.Equal(0x11, cartridge.ExportBatterySave()[0]);

        cartridge.ClearBatterySaveDirty();
        Assert.False(cartridge.IsBatterySaveDirty);
    }

    [Fact]
    public void BatterySave_ImportsMbc1RamBanks()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc1RamBattery;
            bytes[0x0149] = 0x03;
        });
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));
        var save = new byte[32 * 1024];
        save[0] = 0x33;
        save[AddressMap.ExternalRamWindowSize] = 0x44;

        var result = cartridge.ImportBatterySave(save);

        Assert.True(
            result.IsSuccess,
            string.Join(Environment.NewLine, result.Errors.Select(error => error.Message))
        );
        Assert.False(cartridge.IsBatterySaveDirty);

        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRom(0x6000, 0x01);
        Assert.Equal(0x33, cartridge.ReadRam(AddressMap.ExternalRamStart));

        cartridge.WriteRom(0x4000, 0x01);
        Assert.Equal(0x44, cartridge.ReadRam(AddressMap.ExternalRamStart));
    }

    [Fact]
    public void BatterySave_RejectsInvalidMbc1SaveSize()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc1RamBattery;
            bytes[0x0149] = 0x02;
        });
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        var result = cartridge.ImportBatterySave(new byte[1]);

        Assert.True(result.IsFailed);
    }
}
