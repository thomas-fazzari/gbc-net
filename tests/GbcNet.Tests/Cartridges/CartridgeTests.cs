using FluentResults;
using GbcNet.Core.Cartridges;

namespace GbcNet.Tests.Cartridges;

public sealed class CartridgeTests
{
    [Fact]
    public void Load_AcceptsRomOnlyCartridge()
    {
        byte[] rom = TestRomFactory.Create();

        Result<Cartridge> result = Cartridge.Load(rom);

        Assert.True(result.IsSuccess, DescribeErrors(result.Errors));
        Assert.Equal("TEST ROM", result.Value.Header.Title);
        Assert.Equal(CgbSupport.None, result.Value.Header.CgbSupport);
        Assert.Equal(CartridgeType.RomOnly, result.Value.Header.CartridgeType);
        Assert.Equal(32 * 1024, result.Value.Header.RomSizeBytes);
        Assert.Equal(2, result.Value.Header.RomBankCount);
        Assert.Equal(0, result.Value.Header.RamSizeBytes);
        Assert.Equal(0, result.Value.Header.RamBankCount);
    }

    [Fact]
    public void Load_DetectsCgbEnhancedCartridge()
    {
        byte[] rom = TestRomFactory.Create(bytes => bytes[0x0143] = 0x80);

        Result<Cartridge> result = Cartridge.Load(rom);

        Assert.True(result.IsSuccess, DescribeErrors(result.Errors));
        Assert.Equal(CgbSupport.Enhanced, result.Value.Header.CgbSupport);
    }

    [Fact]
    public void Load_DoesNotIncludeCgbFlagInTitle()
    {
        byte[] rom = TestRomFactory.Create(bytes =>
        {
            "FIFTEENCHARROM!"u8.CopyTo(bytes.AsSpan(0x0134));
            bytes[0x0143] = 0x80;
        });

        Result<Cartridge> result = Cartridge.Load(rom);

        Assert.True(result.IsSuccess, DescribeErrors(result.Errors));
        Assert.Equal("FIFTEENCHARROM!", result.Value.Header.Title);
    }

    [Fact]
    public void Load_DoesNotIncludeManufacturerCodeInNewHeaderTitle()
    {
        byte[] rom = TestRomFactory.Create(bytes =>
        {
            "ELEVENCHARS"u8.CopyTo(bytes.AsSpan(0x0134));
            "MAKR"u8.CopyTo(bytes.AsSpan(0x013F));
            bytes[0x014B] = 0x33;
        });

        Result<Cartridge> result = Cartridge.Load(rom);

        Assert.True(result.IsSuccess, DescribeErrors(result.Errors));
        Assert.Equal("ELEVENCHARS", result.Value.Header.Title);
    }

    [Fact]
    public void Load_DetectsCgbRequiredCartridge()
    {
        byte[] rom = TestRomFactory.Create(bytes => bytes[0x0143] = 0xC0);

        Result<Cartridge> result = Cartridge.Load(rom);

        Assert.True(result.IsSuccess, DescribeErrors(result.Errors));
        Assert.Equal(CgbSupport.Required, result.Value.Header.CgbSupport);
    }

    [Fact]
    public void Load_RejectsRomSmallerThanHeader()
    {
        byte[] rom = new byte[0x014F];

        Result<Cartridge> result = Cartridge.Load(rom);

        Assert.True(result.IsFailed);
        Assert.Equal(CartridgeLoadErrorCode.RomTooSmall, GetErrorCode(result.Errors[0]));
    }

    [Fact]
    public void Load_RejectsInvalidHeaderChecksum()
    {
        byte[] rom = TestRomFactory.Create();
        rom[0x014D]++;

        Result<Cartridge> result = Cartridge.Load(rom);

        Assert.True(result.IsFailed);
        Assert.Equal(CartridgeLoadErrorCode.InvalidHeaderChecksum, GetErrorCode(result.Errors[0]));
    }

    [Fact]
    public void Load_RejectsUnsupportedCartridgeType()
    {
        byte[] rom = TestRomFactory.Create(bytes => bytes[0x0147] = (byte)CartridgeType.Mbc1);

        Result<Cartridge> result = Cartridge.Load(rom);

        Assert.True(result.IsFailed);
        Assert.Equal(
            CartridgeLoadErrorCode.UnsupportedCartridgeType,
            GetErrorCode(result.Errors[0])
        );
    }

    [Fact]
    public void Load_RejectsMismatchedRomSize()
    {
        byte[] rom = TestRomFactory.Create(bytes => bytes[0x0148] = 0x01);

        Result<Cartridge> result = Cartridge.Load(rom);

        Assert.True(result.IsFailed);
        Assert.Equal(CartridgeLoadErrorCode.RomLengthMismatch, GetErrorCode(result.Errors[0]));
    }

    [Fact]
    public void CalculateHeaderChecksum_RejectsRomWithoutChecksumByte()
    {
        byte[] rom = new byte[0x014D];

        Assert.Throws<ArgumentException>(() => CartridgeHeader.CalculateHeaderChecksum(rom));
    }

    [Fact]
    public void ReadRom_ReturnsBytesFromFixedRomArea()
    {
        byte[] rom = TestRomFactory.Create();
        rom[0x0000] = 0x31;
        rom[0x4000] = 0xC3;
        Result<Cartridge> result = Cartridge.Load(rom);

        Assert.True(result.IsSuccess, DescribeErrors(result.Errors));
        Assert.Equal(0x31, result.Value.ReadRom(0x0000));
        Assert.Equal(0xC3, result.Value.ReadRom(0x4000));
        Assert.Equal(rom[0x7FFF], result.Value.ReadRom(0x7FFF));
    }

    private static CartridgeLoadErrorCode GetErrorCode(IError error)
    {
        CartridgeLoadError cartridgeError = Assert.IsType<CartridgeLoadError>(error);
        return cartridgeError.Code;
    }

    private static string DescribeErrors(IReadOnlyList<IError> errors)
    {
        return string.Join(Environment.NewLine, errors.Select(error => error.Message));
    }
}
