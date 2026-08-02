// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cartridges;

namespace GbcNet.Tests.Unit.Cartridges;

public sealed class CartridgeTests
{
    [Fact]
    public void Load_AcceptsRomOnlyCartridge()
    {
        var rom = TestRomFactory.Create();

        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.Header.Title.Should().Be("TEST ROM");
        cartridge.Header.CgbSupport.Should().Be(CgbSupport.None);
        cartridge.Header.CartridgeType.Should().Be(CartridgeType.RomOnly);
        cartridge.Header.RomSizeBytes.Should().Be(32 * 1024);
        cartridge.Header.RomBankCount.Should().Be(2);
        cartridge.Header.RamSizeBytes.Should().Be(0);
        cartridge.Header.RamBankCount.Should().Be(0);
    }

    [Fact]
    public void LoadResult_ExposesOnlyActivePayload()
    {
        var success = Cartridge.Load(TestRomFactory.Create());

        success.IsSuccess.Should().BeTrue();
        success.IsFailure.Should().BeFalse();
        success.Cartridge.Header.Title.Should().Be("TEST ROM");
        var successException = FluentActions
            .Invoking(() => success.Error)
            .Should()
            .ThrowExactly<InvalidOperationException>()
            .Which;
        successException.Message.Should().Be("Cartridge load did not fail.");

        var failure = Cartridge.Load(new byte[0x014F]);

        failure.IsSuccess.Should().BeFalse();
        failure.IsFailure.Should().BeTrue();
        failure.Error.Code.Should().Be(CartridgeLoadErrorCode.RomTooSmall);
        var failureException = FluentActions
            .Invoking(() => failure.Cartridge)
            .Should()
            .ThrowExactly<InvalidOperationException>()
            .Which;
        failureException.Message.Should().Be("Cartridge load did not succeed.");
    }

    [Fact]
    public void Load_DetectsCgbEnhancedCartridge()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0143] = 0x80);

        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.Header.CgbSupport.Should().Be(CgbSupport.Enhanced);
    }

    [Fact]
    public void Load_DoesNotIncludeCgbFlagInTitle()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            "FIFTEENCHARROM!"u8.CopyTo(bytes.AsSpan(0x0134));
            bytes[0x0143] = 0x80;
        });

        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.Header.Title.Should().Be("FIFTEENCHARROM!");
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

        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.Header.Title.Should().Be("ELEVENCHARS");
    }

    [Fact]
    public void Load_DetectsCgbRequiredCartridge()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0143] = 0xC0);

        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.Header.CgbSupport.Should().Be(CgbSupport.Required);
    }

    [Fact]
    public void Load_DetectsSgbCartridgeWhenSgbFlagAndLicenseeUnlockArePresent()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0146] = 0x03;
            bytes[0x014B] = 0x33;
        });

        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.Header.HardwareKind.Should().Be(CartridgeHardwareKind.SGB);
    }

    [Fact]
    public void Load_DetectsCgbEnhancedCartridgeWhenCgbAndSgbFlagsArePresent()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0143] = 0x80;
            bytes[0x0146] = 0x03;
            bytes[0x014B] = 0x33;
        });

        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.Header.CgbSupport.Should().Be(CgbSupport.Enhanced);
        cartridge.Header.HardwareKind.Should().Be(CartridgeHardwareKind.GBC);
    }

    [Fact]
    public void Load_DoesNotDetectSgbCartridgeWhenLicenseeUnlockIsMissing()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0146] = 0x03);

        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.Header.HardwareKind.Should().Be(CartridgeHardwareKind.GB);
    }

    [Fact]
    public void Load_RejectsRomSmallerThanHeader()
    {
        var rom = new byte[0x014F];

        var result = Cartridge.Load(rom);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(CartridgeLoadErrorCode.RomTooSmall);
    }

    [Fact]
    public void Load_RejectsInvalidHeaderChecksum()
    {
        var rom = TestRomFactory.Create();
        rom[0x014D]++;

        var result = Cartridge.Load(rom);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(CartridgeLoadErrorCode.InvalidHeaderChecksum);
    }

    [Fact]
    public void Load_RejectsUnsupportedCartridgeType()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0147] = 0x0B);

        var result = Cartridge.Load(rom);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(CartridgeLoadErrorCode.UnsupportedCartridgeType);
    }

    [Fact]
    public void Load_RejectsMismatchedRomSize()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0148] = 0x01);

        var result = Cartridge.Load(rom);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(CartridgeLoadErrorCode.RomLengthMismatch);
    }

    [Fact]
    public void CalculateHeaderChecksum_RejectsRomWithoutChecksumByte()
    {
        var rom = new byte[0x014D];

        FluentActions
            .Invoking(() => CartridgeHeader.CalculateHeaderChecksum(rom))
            .Should()
            .ThrowExactly<ArgumentException>();
    }

    [Fact]
    public void ReadRom_ReturnsBytesFromFixedRomArea()
    {
        var rom = TestRomFactory.Create();
        rom[0x0000] = 0x31;
        rom[0x4000] = 0xC3;
        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.ReadRom(0x0000).Should().Be(0x31);
        cartridge.ReadRom(0x4000).Should().Be(0xC3);
        cartridge.ReadRom(0x7FFF).Should().Be(rom[0x7FFF]);
    }

    [Fact]
    public void State_RestoresMatchingCartridgeThroughPolymorphicController()
    {
        var cartridge = TestRomFactory.LoadCartridge(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.RomRamBattery;
            bytes[0x0149] = 0x02;
        });
        cartridge.WriteRam(0xA000, 0x23);
        cartridge.ClearBatterySaveDirty();
        var state = cartridge.CaptureState();

        cartridge.WriteRam(0xA000, 0xB4);

        cartridge.RestoreState(state);

        cartridge.ReadRam(0xA000).Should().Be(0x23);
        cartridge.IsBatterySaveDirty.Should().BeFalse();
    }

    [Fact]
    public void State_RejectsDefaultControllerStateWithoutChangingCartridge()
    {
        var cartridge = TestRomFactory.LoadCartridge(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.RomRamBattery;
            bytes[0x0149] = 0x02;
        });
        cartridge.WriteRam(0xA000, 0x3C);
        cartridge.ClearBatterySaveDirty();

        FluentActions
            .Invoking(() => cartridge.ValidateState(new CartridgeState(null!)))
            .Should()
            .ThrowExactly<ArgumentException>();

        FluentActions
            .Invoking(() => cartridge.RestoreState(new CartridgeState(null!)))
            .Should()
            .ThrowExactly<ArgumentException>();

        cartridge.ReadRam(0xA000).Should().Be(0x3C);
        cartridge.IsBatterySaveDirty.Should().BeFalse();
    }

    [Fact]
    public void State_RejectsDifferentMapperStateWithoutChangingCartridge()
    {
        var cartridge = TestRomFactory.LoadCartridge(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.RomRamBattery;
            bytes[0x0149] = 0x02;
        });
        cartridge.WriteRam(0xA000, 0x3C);
        cartridge.ClearBatterySaveDirty();
        var differentMapper = TestRomFactory.LoadCartridge(bytes =>
            bytes[0x0147] = (byte)CartridgeType.Mbc1
        );

        FluentActions
            .Invoking(() => cartridge.ValidateState(differentMapper.CaptureState()))
            .Should()
            .ThrowExactly<ArgumentException>();

        FluentActions
            .Invoking(() => cartridge.RestoreState(differentMapper.CaptureState()))
            .Should()
            .ThrowExactly<ArgumentException>();

        cartridge.ReadRam(0xA000).Should().Be(0x3C);
        cartridge.IsBatterySaveDirty.Should().BeFalse();
    }
}
