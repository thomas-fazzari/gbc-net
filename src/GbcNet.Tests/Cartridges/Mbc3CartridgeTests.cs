using FluentResults;
using GbcNet.Core.Cartridges;
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
    [InlineData(0x0F)]
    [InlineData(0x10)]
    public void Load_RejectsMbc3TimerCartridges(byte cartridgeType)
    {
        byte[] rom = TestRomFactory.Create(bytes => bytes[0x0147] = cartridgeType);

        Result<Cartridge> result = Cartridge.Load(rom);

        Assert.True(result.IsFailed);
        Assert.Equal(
            CartridgeLoadErrorCode.UnsupportedCartridgeType,
            GetErrorCode(result.Errors[0])
        );
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
    public void WriteRam_RtcRegisterSelectionDoesNotDirtyBatteryRam()
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

        Assert.False(cartridge.IsBatteryRamDirty);
    }

    [Fact]
    public void BatteryRam_ExportsAndImportsMbc3RamBanks()
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

        byte[] save = cartridge.ExportBatteryRam();

        Assert.True(cartridge.HasBatteryBackedRam);
        Assert.Equal(32 * 1024, cartridge.BatteryRamSize);
        Assert.True(cartridge.IsBatteryRamDirty);
        Assert.Equal(0x11, save[0]);
        Assert.Equal(0x22, save[AddressMap.ExternalRamWindowSize]);

        Cartridge reloaded = ResultAssertions.AssertSuccess(Cartridge.Load(rom));
        Result import = reloaded.ImportBatteryRam(save);

        Assert.True(
            import.IsSuccess,
            string.Join(Environment.NewLine, import.Errors.Select(error => error.Message))
        );
        Assert.False(reloaded.IsBatteryRamDirty);

        reloaded.WriteRom(0x0000, 0x0A);
        Assert.Equal(0x11, reloaded.ReadRam(AddressMap.ExternalRamStart));

        reloaded.WriteRom(0x4000, 0x01);
        Assert.Equal(0x22, reloaded.ReadRam(AddressMap.ExternalRamStart));
    }

    [Fact]
    public void BatteryRam_RejectsInvalidMbc3SaveSize()
    {
        byte[] rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc3RamBattery;
            bytes[0x0149] = 0x02;
        });
        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        Result result = cartridge.ImportBatteryRam(new byte[1]);

        Assert.True(result.IsFailed);
    }

    private static CartridgeLoadErrorCode GetErrorCode(IError error)
    {
        CartridgeLoadError cartridgeError = Assert.IsType<CartridgeLoadError>(error);
        return cartridgeError.Code;
    }
}
