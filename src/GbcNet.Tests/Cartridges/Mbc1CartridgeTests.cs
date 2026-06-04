using GbcNet.Core.Cartridges;
using GbcNet.Core.Memory;

namespace GbcNet.Tests.Cartridges;

public sealed class Mbc1CartridgeTests
{
    private const int RomBankSize = Cartridge.FixedRomBankSize;

    [Fact]
    public void Load_AcceptsMbc1Cartridge()
    {
        byte[] rom = TestRomFactory.Create(bytes => bytes[0x0147] = (byte)CartridgeType.Mbc1);

        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        Assert.Equal(CartridgeType.Mbc1, cartridge.Header.CartridgeType);
    }

    [Fact]
    public void ReadRom_MapsSwitchableAreaToBankOneByDefault()
    {
        byte[] rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc1;
            bytes[RomBankSize] = 0x42;
        });
        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        Assert.Equal(0x42, cartridge.ReadRom(0x4000));
    }

    [Fact]
    public void WriteRom_SwitchesMbc1RomBank()
    {
        byte[] rom = TestRomFactory.Create(
            romSizeCode: 0x01,
            bytes =>
            {
                bytes[0x0147] = (byte)CartridgeType.Mbc1;
                bytes[1 * RomBankSize] = 0x11;
                bytes[2 * RomBankSize] = 0x22;
            }
        );
        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x2000, 0x02);

        Assert.Equal(0x22, cartridge.ReadRom(0x4000));
    }

    [Fact]
    public void WriteRom_TreatsMbc1LowRomBankZeroAsOneBeforeMasking()
    {
        byte[] rom = TestRomFactory.Create(
            romSizeCode: 0x01,
            bytes =>
            {
                bytes[0x0147] = (byte)CartridgeType.Mbc1;
                bytes[0 * RomBankSize] = 0x00;
                bytes[1 * RomBankSize] = 0x11;
            }
        );
        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x2000, 0x00);

        Assert.Equal(0x11, cartridge.ReadRom(0x4000));
    }

    [Fact]
    public void WriteRom_UsesFullMbc1LowRomBankRegisterBeforeRomBankMask()
    {
        byte[] rom = TestRomFactory.Create(
            romSizeCode: 0x01,
            bytes =>
            {
                bytes[0x0147] = (byte)CartridgeType.Mbc1;
                bytes[0 * RomBankSize] = 0x00;
                bytes[1 * RomBankSize] = 0x11;
            }
        );
        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x2000, 0x10);

        Assert.Equal(0x00, cartridge.ReadRom(0x4000));
    }

    [Fact]
    public void WriteRom_UsesMbc1HighBankBitsForLargeRom()
    {
        const int bank1 = 0x01;
        const int bank21 = 0x21;

        byte[] rom = TestRomFactory.Create(
            romSizeCode: 0x05,
            bytes =>
            {
                bytes[0x0147] = (byte)CartridgeType.Mbc1;
                bytes[bank1 * RomBankSize] = 0x11;
                bytes[bank21 * RomBankSize] = 0x21;
            }
        );
        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x4000, 0x01);

        Assert.Equal(0x21, cartridge.ReadRom(0x4000));
    }

    [Fact]
    public void WriteRom_UsesMbc1AdvancedModeForFixedRomArea()
    {
        const int bank20 = 0x20;

        byte[] rom = TestRomFactory.Create(
            romSizeCode: 0x05,
            bytes =>
            {
                bytes[0x0147] = (byte)CartridgeType.Mbc1;
                bytes[AddressMap.CartridgeEntryPointStart] = 0x00;
                bytes[(bank20 * RomBankSize) + AddressMap.CartridgeEntryPointStart] = 0x20;
            }
        );
        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        cartridge.WriteRom(0x4000, 0x01);
        cartridge.WriteRom(0x6000, 0x01);

        Assert.Equal(0x20, cartridge.ReadRom(AddressMap.CartridgeEntryPointStart));
    }

    [Fact]
    public void ReadWriteRam_RequiresMbc1RamEnable()
    {
        byte[] rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc1Ram;
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
    public void ReadWriteRam_UsesMbc1AdvancedModeRamBank()
    {
        byte[] rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc1Ram;
            bytes[0x0149] = 0x03;
        });
        Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

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
}
