// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cartridges;
using GbcNet.Core.Memory;

namespace GbcNet.Tests.Cartridges;

public sealed class NoMbcCartridgeTests
{
    [Theory]
    [InlineData(CartridgeType.RomRam)]
    [InlineData(CartridgeType.RomRamBattery)]
    public void Load_AcceptsRomRamCartridge(CartridgeType cartridgeType)
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)cartridgeType;
            bytes[0x0149] = 0x02;
        });

        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        Assert.Equal(cartridgeType, cartridge.Header.CartridgeType);
        Assert.Equal(8 * 1024, cartridge.Header.RamSizeBytes);
    }

    [Fact]
    public void ReadWriteRam_UsesFixedRomRamBankWithoutEnableRegister()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.RomRam;
            bytes[0x0149] = 0x02;
        });
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);

        Assert.Equal(0x42, cartridge.ReadRam(AddressMap.ExternalRamStart));
    }

    [Fact]
    public void ReadWriteRam_ReturnsFFWhenNoRomRamIsConnected()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0147] = (byte)CartridgeType.RomRam);
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);

        Assert.Equal(0xFF, cartridge.ReadRam(AddressMap.ExternalRamStart));
    }

    [Fact]
    public void BatterySave_IsUnavailableForRomRamWithoutBattery()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.RomRam;
            bytes[0x0149] = 0x02;
        });
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);

        Assert.False(cartridge.HasBatteryBackedSave);
        Assert.Equal(0, cartridge.BatterySaveSize);
        Assert.False(cartridge.IsBatterySaveDirty);
        Assert.Empty(cartridge.ExportBatterySave());
    }

    [Fact]
    public void BatterySave_ExportsAndImportsRomRamBattery()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.RomRamBattery;
            bytes[0x0149] = 0x02;
        });
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x11);
        cartridge.WriteRam(AddressMap.ExternalRamStart + 0x0100, 0x22);

        var save = cartridge.ExportBatterySave();

        Assert.True(cartridge.HasBatteryBackedSave);
        Assert.Equal(8 * 1024, cartridge.BatterySaveSize);
        Assert.True(cartridge.IsBatterySaveDirty);
        Assert.Equal(0x11, save[0]);
        Assert.Equal(0x22, save[0x0100]);

        var reloaded = ResultAssertions.AssertSuccess(Cartridge.Load(rom));
        var import = reloaded.ImportBatterySave(save);

        Assert.True(
            import.IsSuccess,
            string.Join(Environment.NewLine, import.Errors.Select(error => error.Message))
        );
        Assert.False(reloaded.IsBatterySaveDirty);
        Assert.Equal(0x11, reloaded.ReadRam(AddressMap.ExternalRamStart));
        Assert.Equal(0x22, reloaded.ReadRam(AddressMap.ExternalRamStart + 0x0100));

        reloaded.WriteRam(AddressMap.ExternalRamStart, 0x33);
        Assert.True(reloaded.IsBatterySaveDirty);

        reloaded.ClearBatterySaveDirty();
        Assert.False(reloaded.IsBatterySaveDirty);
    }

    [Fact]
    public void BatterySave_RejectsInvalidRomRamBatterySaveSize()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.RomRamBattery;
            bytes[0x0149] = 0x02;
        });
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        var result = cartridge.ImportBatterySave(new byte[1]);

        Assert.True(result.IsFailed);
    }
}
