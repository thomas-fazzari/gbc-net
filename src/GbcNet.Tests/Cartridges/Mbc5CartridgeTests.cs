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
        byte[] rom = TestRomFactory.Create(bytes => bytes[0x0147] = (byte)cartridgeType);

        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        Assert.Equal(cartridgeType, cartridge.Header.CartridgeType);
    }

    [Fact]
    public void ReadRom_MapsSwitchableAreaToBankOneByDefault()
    {
        byte[] rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc5;
            bytes[RomBankSize] = 0x42;
        });
        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        Assert.Equal(0x42, cartridge.ReadRom(0x4000));
    }

    [Fact]
    public void WriteRom_AllowsMbc5RomBankZeroInSwitchableArea()
    {
        byte[] rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc5;
            bytes[0] = 0x11;
        });
        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x2000, 0x00);

        Assert.Equal(0x11, cartridge.ReadRom(0x4000));
    }

    [Fact]
    public void WriteRom_UsesMbc5LowRomBankBits()
    {
        byte[] rom = TestRomFactory.Create(
            romSizeCode: 0x01,
            bytes =>
            {
                bytes[0x0147] = (byte)CartridgeType.Mbc5;
                bytes[1 * RomBankSize] = 0x11;
                bytes[2 * RomBankSize] = 0x22;
            }
        );
        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x2000, 0x02);

        Assert.Equal(0x22, cartridge.ReadRom(0x4000));
    }

    [Fact]
    public void WriteRom_UsesMbc5HighRomBankBit()
    {
        const int bank257 = 0x101;

        byte[] rom = TestRomFactory.Create(
            romSizeCode: 0x08,
            bytes =>
            {
                bytes[0x0147] = (byte)CartridgeType.Mbc5;
                bytes[1 * RomBankSize] = 0x11;
                bytes[bank257 * RomBankSize] = 0x57;
            }
        );
        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x2000, 0x01);
        cartridge.WriteRom(0x3000, 0x01);

        Assert.Equal(0x57, cartridge.ReadRom(0x4000));
    }

    [Fact]
    public void ReadWriteRam_RequiresMbc5RamEnable()
    {
        byte[] rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc5Ram;
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
    public void ReadWriteRam_UsesMbc5RamBank()
    {
        byte[] rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc5Ram;
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
}
