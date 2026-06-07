using FluentResults;
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
        byte[] rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)cartridgeType;
            bytes[0x0149] = 0x02;
        });

        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        Assert.Equal(cartridgeType, cartridge.Header.CartridgeType);
        Assert.Equal(8 * 1024, cartridge.Header.RamSizeBytes);
    }

    [Fact]
    public void ReadWriteRam_UsesFixedRomRamBankWithoutEnableRegister()
    {
        byte[] rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.RomRam;
            bytes[0x0149] = 0x02;
        });
        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);

        Assert.Equal(0x42, cartridge.ReadRam(AddressMap.ExternalRamStart));
    }

    [Fact]
    public void ReadWriteRam_ReturnsFFWhenNoRomRamIsConnected()
    {
        byte[] rom = TestRomFactory.Create(bytes => bytes[0x0147] = (byte)CartridgeType.RomRam);
        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);

        Assert.Equal(0xFF, cartridge.ReadRam(AddressMap.ExternalRamStart));
    }

    [Fact]
    public void BatteryRam_IsUnavailableForRomRamWithoutBattery()
    {
        byte[] rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.RomRam;
            bytes[0x0149] = 0x02;
        });
        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);

        Assert.False(cartridge.HasBatteryBackedRam);
        Assert.Equal(0, cartridge.BatteryRamSize);
        Assert.False(cartridge.IsBatteryRamDirty);
        Assert.Empty(cartridge.ExportBatteryRam());
    }

    [Fact]
    public void BatteryRam_ExportsAndImportsRomRamBattery()
    {
        byte[] rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.RomRamBattery;
            bytes[0x0149] = 0x02;
        });
        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x11);
        cartridge.WriteRam(AddressMap.ExternalRamStart + 0x0100, 0x22);

        byte[] save = cartridge.ExportBatteryRam();

        Assert.True(cartridge.HasBatteryBackedRam);
        Assert.Equal(8 * 1024, cartridge.BatteryRamSize);
        Assert.True(cartridge.IsBatteryRamDirty);
        Assert.Equal(0x11, save[0]);
        Assert.Equal(0x22, save[0x0100]);

        Cartridge reloaded = ResultAssertions.AssertSuccess(Cartridge.Load(rom));
        Result import = reloaded.ImportBatteryRam(save);

        Assert.True(
            import.IsSuccess,
            string.Join(Environment.NewLine, import.Errors.Select(error => error.Message))
        );
        Assert.False(reloaded.IsBatteryRamDirty);
        Assert.Equal(0x11, reloaded.ReadRam(AddressMap.ExternalRamStart));
        Assert.Equal(0x22, reloaded.ReadRam(AddressMap.ExternalRamStart + 0x0100));

        reloaded.WriteRam(AddressMap.ExternalRamStart, 0x33);
        Assert.True(reloaded.IsBatteryRamDirty);

        reloaded.ClearBatteryRamDirty();
        Assert.False(reloaded.IsBatteryRamDirty);
    }

    [Fact]
    public void BatteryRam_RejectsInvalidRomRamBatterySaveSize()
    {
        byte[] rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.RomRamBattery;
            bytes[0x0149] = 0x02;
        });
        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        Result result = cartridge.ImportBatteryRam(new byte[1]);

        Assert.True(result.IsFailed);
    }
}
