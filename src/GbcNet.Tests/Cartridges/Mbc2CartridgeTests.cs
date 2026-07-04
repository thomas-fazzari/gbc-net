// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cartridges;
using GbcNet.Core.Memory;

namespace GbcNet.Tests.Cartridges;

public sealed class Mbc2CartridgeTests
{
    private const int RomBankSize = Cartridge.FixedRomBankSize;

    [Theory]
    [InlineData(CartridgeType.Mbc2)]
    [InlineData(CartridgeType.Mbc2Battery)]
    public void Load_AcceptsMbc2Cartridge(CartridgeType cartridgeType)
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0147] = (byte)cartridgeType);

        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        Assert.Equal(cartridgeType, cartridge.Header.CartridgeType);
    }

    [Fact]
    public void WriteRom_UsesAddressBit8ClearForMbc2RamEnable()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0147] = (byte)CartridgeType.Mbc2);
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x0100, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x02);

        Assert.Equal(0xFF, cartridge.ReadRam(AddressMap.ExternalRamStart));

        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x02);

        Assert.Equal(0xF2, cartridge.ReadRam(AddressMap.ExternalRamStart));

        cartridge.WriteRom(0x0000, 0x00);

        Assert.Equal(0xFF, cartridge.ReadRam(AddressMap.ExternalRamStart));
    }

    [Fact]
    public void WriteRom_UsesAddressBit8SetForMbc2RomBank()
    {
        var rom = TestRomFactory.Create(
            romSizeCode: 0x03,
            bytes =>
            {
                bytes[0x0147] = (byte)CartridgeType.Mbc2;
                bytes[1 * RomBankSize] = 0x11;
                bytes[2 * RomBankSize] = 0x22;
            }
        );
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x2000, 0x02);

        Assert.Equal(0x11, cartridge.ReadRom(0x4000));

        cartridge.WriteRom(0x2100, 0x02);

        Assert.Equal(0x22, cartridge.ReadRom(0x4000));
    }

    [Fact]
    public void WriteRom_TreatsMbc2RomBankZeroAsOne()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc2;
            bytes[0 * RomBankSize] = 0x00;
            bytes[1 * RomBankSize] = 0x11;
        });
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x2100, 0x00);

        Assert.Equal(0x11, cartridge.ReadRom(0x4000));
    }

    [Fact]
    public void ReadRam_ReturnsMbc2StoredNibbleWithHighNibbleSet()
    {
        var cartridge = LoadMbc2WithEnabledRam(CartridgeType.Mbc2);

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x05);

        Assert.Equal(0xF5, cartridge.ReadRam(AddressMap.ExternalRamStart));
    }

    [Fact]
    public void WriteRam_StoresOnlyMbc2LowNibble()
    {
        var cartridge = LoadMbc2WithEnabledRam(CartridgeType.Mbc2);

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0xAB);

        Assert.Equal(0xFB, cartridge.ReadRam(AddressMap.ExternalRamStart));
    }

    [Fact]
    public void ReadWriteRam_MirrorsMbc2RamByLowNineAddressBits()
    {
        var cartridge = LoadMbc2WithEnabledRam(CartridgeType.Mbc2);

        cartridge.WriteRam(AddressMap.ExternalRamStart + 0x0201, 0x07);

        Assert.Equal(0xF7, cartridge.ReadRam(AddressMap.ExternalRamStart + 0x0001));
    }

    [Fact]
    public void BatterySave_ExportsAndImportsMbc2Ram()
    {
        var cartridge = LoadMbc2WithEnabledRam(CartridgeType.Mbc2Battery);

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0xAB);
        cartridge.WriteRam(AddressMap.ExternalRamStart + 1, 0x0C);

        var save = cartridge.ExportBatterySave();

        Assert.True(cartridge.HasBatteryBackedSave);
        Assert.Equal(512, cartridge.BatterySaveSize);
        Assert.True(cartridge.IsBatterySaveDirty);
        Assert.Equal(512, save.Length);
        Assert.Equal(0x0B, save[0]);
        Assert.Equal(0x0C, save[1]);

        save[1] = 0xBC;
        var reloaded = LoadMbc2WithEnabledRam(CartridgeType.Mbc2Battery);
        var import = reloaded.ImportBatterySave(save);

        Assert.True(
            import.IsSuccess,
            string.Join(Environment.NewLine, import.Errors.Select(error => error.Message))
        );
        Assert.False(reloaded.IsBatterySaveDirty);
        Assert.Equal(0xFB, reloaded.ReadRam(AddressMap.ExternalRamStart));
        Assert.Equal(0xFC, reloaded.ReadRam(AddressMap.ExternalRamStart + 1));
    }

    [Fact]
    public void BatterySave_IsUnavailableForMbc2WithoutBattery()
    {
        var cartridge = LoadMbc2WithEnabledRam(CartridgeType.Mbc2);

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x0B);

        Assert.False(cartridge.HasBatteryBackedSave);
        Assert.Equal(0, cartridge.BatterySaveSize);
        Assert.False(cartridge.IsBatterySaveDirty);
        Assert.Empty(cartridge.ExportBatterySave());
    }

    [Fact]
    public void BatterySave_RejectsInvalidMbc2SaveSize()
    {
        var cartridge = LoadMbc2WithEnabledRam(CartridgeType.Mbc2Battery);

        var result = cartridge.ImportBatterySave(new byte[1]);

        Assert.True(result.IsFailed);
    }

    private static Cartridge LoadMbc2WithEnabledRam(CartridgeType cartridgeType)
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0147] = (byte)cartridgeType);
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));
        cartridge.WriteRom(0x0000, 0x0A);
        return cartridge;
    }
}
