using GbcNet.Core.Cartridges;
using GbcNet.Core.Memory;

namespace GbcNet.Tests.Cartridges;

public sealed class Mbc5CartridgeTests
{
    private const int RomBankSize = Cartridge.FixedRomBankSize;

    [Theory]
    [InlineData(CartridgeType.Mbc5)]
    [InlineData(CartridgeType.Mbc5Ram)]
    [InlineData(CartridgeType.Mbc5RamBattery)]
    public void Load_AcceptsMbc5Cartridge(CartridgeType cartridgeType)
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0147] = (byte)cartridgeType);

        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        Assert.Equal(cartridgeType, cartridge.Header.CartridgeType);
    }

    [Fact]
    public void ReadRom_MapsSwitchableAreaToBankOneByDefault()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc5;
            bytes[RomBankSize] = 0x42;
        });
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        Assert.Equal(0x42, cartridge.ReadRom(0x4000));
    }

    [Fact]
    public void WriteRom_AllowsMbc5RomBankZeroInSwitchableArea()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc5;
            bytes[0] = 0x11;
        });
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x2000, 0x00);

        Assert.Equal(0x11, cartridge.ReadRom(0x4000));
    }

    [Fact]
    public void WriteRom_UsesMbc5LowRomBankBits()
    {
        var rom = TestRomFactory.Create(
            romSizeCode: 0x01,
            bytes =>
            {
                bytes[0x0147] = (byte)CartridgeType.Mbc5;
                bytes[1 * RomBankSize] = 0x11;
                bytes[2 * RomBankSize] = 0x22;
            }
        );
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x2000, 0x02);

        Assert.Equal(0x22, cartridge.ReadRom(0x4000));
    }

    [Fact]
    public void WriteRom_UsesMbc5HighRomBankBit()
    {
        const int bank257 = 0x101;

        var rom = TestRomFactory.Create(
            romSizeCode: 0x08,
            bytes =>
            {
                bytes[0x0147] = (byte)CartridgeType.Mbc5;
                bytes[1 * RomBankSize] = 0x11;
                bytes[bank257 * RomBankSize] = 0x57;
            }
        );
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x2000, 0x01);
        cartridge.WriteRom(0x3000, 0x01);

        Assert.Equal(0x57, cartridge.ReadRom(0x4000));
    }

    [Fact]
    public void ReadWriteRam_RequiresMbc5RamEnable()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc5Ram;
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
    public void ReadWriteRam_UsesMbc5RamBank()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc5Ram;
            bytes[0x0149] = 0x03;
        });
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

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
    public void BatterySave_ExportsAndImportsMbc5RamBanks()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc5RamBattery;
            bytes[0x0149] = 0x03;
        });
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x11);
        cartridge.WriteRom(0x4000, 0x01);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x22);

        var save = cartridge.ExportBatterySave();

        Assert.True(cartridge.HasBatteryBackedSave);
        Assert.Equal(32 * 1024, cartridge.BatterySaveSize);
        Assert.True(cartridge.IsBatterySaveDirty);
        Assert.Equal(0x11, save[0]);
        Assert.Equal(0x22, save[AddressMap.ExternalRamWindowSize]);

        var reloaded = ResultAssertions.AssertSuccess(Cartridge.Load(rom));
        var import = reloaded.ImportBatterySave(save);

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
}
