using FluentResults;
using GbcNet.Core.Cartridges;

namespace GbcNet.Tests.Cartridges;

public sealed class CartridgeTests
{
    [Fact]
    public void Load_AcceptsRomOnlyCartridge()
    {
        var rom = TestRomFactory.Create();

        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        Assert.Equal("TEST ROM", cartridge.Header.Title);
        Assert.Equal(CgbSupport.None, cartridge.Header.CgbSupport);
        Assert.Equal(CartridgeType.RomOnly, cartridge.Header.CartridgeType);
        Assert.Equal(32 * 1024, cartridge.Header.RomSizeBytes);
        Assert.Equal(2, cartridge.Header.RomBankCount);
        Assert.Equal(0, cartridge.Header.RamSizeBytes);
        Assert.Equal(0, cartridge.Header.RamBankCount);
    }

    [Fact]
    public void Load_DetectsCgbEnhancedCartridge()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0143] = 0x80);

        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        Assert.Equal(CgbSupport.Enhanced, cartridge.Header.CgbSupport);
    }

    [Fact]
    public void Load_DoesNotIncludeCgbFlagInTitle()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            "FIFTEENCHARROM!"u8.CopyTo(bytes.AsSpan(0x0134));
            bytes[0x0143] = 0x80;
        });

        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        Assert.Equal("FIFTEENCHARROM!", cartridge.Header.Title);
    }

    [Fact]
    public void Load_DoesNotIncludeManufacturerCodeInNewHeaderTitle()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            "ELEVENCHARS"u8.CopyTo(bytes.AsSpan(0x0134));
            "MAKR"u8.CopyTo(bytes.AsSpan(0x013F));
            bytes[0x014B] = 0x33;
        });

        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        Assert.Equal("ELEVENCHARS", cartridge.Header.Title);
    }

    [Fact]
    public void Load_DetectsCgbRequiredCartridge()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0143] = 0xC0);

        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        Assert.Equal(CgbSupport.Required, cartridge.Header.CgbSupport);
    }

    [Fact]
    public void Load_DetectsSgbCartridgeWhenSgbFlagAndLicenseeUnlockArePresent()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0146] = 0x03;
            bytes[0x014B] = 0x33;
        });

        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        Assert.Equal(CartridgeHardwareKind.SGB, cartridge.Header.HardwareKind);
    }

    [Fact]
    public void Load_DoesNotDetectSgbCartridgeWhenLicenseeUnlockIsMissing()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0146] = 0x03);

        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        Assert.Equal(CartridgeHardwareKind.GB, cartridge.Header.HardwareKind);
    }

    [Fact]
    public void Load_RejectsRomSmallerThanHeader()
    {
        var rom = new byte[0x014F];

        var result = Cartridge.Load(rom);

        Assert.True(result.IsFailed);
        Assert.Equal(CartridgeLoadErrorCode.RomTooSmall, GetErrorCode(result.Errors[0]));
    }

    [Fact]
    public void Load_RejectsInvalidHeaderChecksum()
    {
        var rom = TestRomFactory.Create();
        rom[0x014D]++;

        var result = Cartridge.Load(rom);

        Assert.True(result.IsFailed);
        Assert.Equal(CartridgeLoadErrorCode.InvalidHeaderChecksum, GetErrorCode(result.Errors[0]));
    }

    [Fact]
    public void Load_RejectsUnsupportedCartridgeType()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0147] = 0x0B);

        var result = Cartridge.Load(rom);

        Assert.True(result.IsFailed);
        Assert.Equal(
            CartridgeLoadErrorCode.UnsupportedCartridgeType,
            GetErrorCode(result.Errors[0])
        );
    }

    [Fact]
    public void Load_RejectsMismatchedRomSize()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0148] = 0x01);

        var result = Cartridge.Load(rom);

        Assert.True(result.IsFailed);
        Assert.Equal(CartridgeLoadErrorCode.RomLengthMismatch, GetErrorCode(result.Errors[0]));
    }

    [Fact]
    public void CalculateHeaderChecksum_RejectsRomWithoutChecksumByte()
    {
        var rom = new byte[0x014D];

        Assert.Throws<ArgumentException>(() => CartridgeHeader.CalculateHeaderChecksum(rom));
    }

    [Fact]
    public void ReadRom_ReturnsBytesFromFixedRomArea()
    {
        var rom = TestRomFactory.Create();
        rom[0x0000] = 0x31;
        rom[0x4000] = 0xC3;
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));

        Assert.Equal(0x31, cartridge.ReadRom(0x0000));
        Assert.Equal(0xC3, cartridge.ReadRom(0x4000));
        Assert.Equal(rom[0x7FFF], cartridge.ReadRom(0x7FFF));
    }

    private static CartridgeLoadErrorCode GetErrorCode(IError error)
    {
        var cartridgeError = Assert.IsType<CartridgeLoadError>(error);
        return cartridgeError.Code;
    }
}
