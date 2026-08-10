// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cartridges;
using GbcNet.Core.Cartridges.Memory;
using GbcNet.Core.Memory;

namespace GbcNet.Tests.Unit.Cartridges;

public sealed class Mbc5CartridgeTests
{
    private const int RomBankSize = Cartridge.FixedRomBankSize;

    [Theory]
    [InlineData(CartridgeType.Mbc5)]
    [InlineData(CartridgeType.Mbc5Ram)]
    [InlineData(CartridgeType.Mbc5RamBattery)]
    [InlineData(CartridgeType.Mbc5Rumble)]
    [InlineData(CartridgeType.Mbc5RumbleRam)]
    [InlineData(CartridgeType.Mbc5RumbleRamBattery)]
    public void Load_AcceptsMbc5Cartridge(CartridgeType cartridgeType)
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0147] = (byte)cartridgeType);

        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.Header.CartridgeType.Should().Be(cartridgeType);
    }

    [Theory]
    [InlineData(CartridgeType.Mbc5Rumble, 0x00, false, false)]
    [InlineData(CartridgeType.Mbc5RumbleRam, 0x02, true, false)]
    [InlineData(CartridgeType.Mbc5RumbleRamBattery, 0x02, true, true)]
    public void Load_MapsRumbleVariantRamBatteryAndMotor(
        CartridgeType cartridgeType,
        byte ramSizeCode,
        bool hasRam,
        bool hasBattery
    )
    {
        // Pan Docs the-cartridge-header.md defines RAM and battery presence for 1C-1E.
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)cartridgeType;
            bytes[0x0149] = ramSizeCode;
        });
        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);

        cartridge
            .ReadRam(AddressMap.ExternalRamStart)
            .Should()
            .Be(hasRam ? (byte)0x42 : (byte)0xFF);
        cartridge.HasBatteryBackedSave.Should().Be(hasBattery);
        cartridge.IsRumbleActive.Should().BeFalse();

        cartridge.WriteRom(0x4000, 0x08);
        cartridge.IsRumbleActive.Should().BeTrue();
        cartridge.WriteRom(0x4000, 0x00);
        cartridge.IsRumbleActive.Should().BeFalse();
    }

    [Fact]
    public void ReadRom_MapsSwitchableAreaToBankOneByDefault()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc5;
            bytes[RomBankSize] = 0x42;
        });
        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.ReadRom(0x4000).Should().Be(0x42);
    }

    [Fact]
    public void WriteRom_AllowsMbc5RomBankZeroInSwitchableArea()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc5;
            bytes[0] = 0x11;
        });
        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.WriteRom(0x2000, 0x00);

        cartridge.ReadRom(0x4000).Should().Be(0x11);
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
        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.WriteRom(0x2000, 0x02);

        cartridge.ReadRom(0x4000).Should().Be(0x22);
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
        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.WriteRom(0x2000, 0x01);
        cartridge.WriteRom(0x3000, 0x01);

        cartridge.ReadRom(0x4000).Should().Be(0x57);
    }

    [Fact]
    public void ReadWriteRam_RequiresMbc5RamEnable()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc5Ram;
            bytes[0x0149] = 0x02;
        });
        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);

        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(0xFF);

        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);

        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(0x42);
    }

    [Fact]
    public void ReadWriteRam_UsesMbc5RamBank()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc5Ram;
            bytes[0x0149] = 0x03;
        });
        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x11);
        cartridge.WriteRom(0x4000, 0x01);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x22);
        cartridge.WriteRom(0x4000, 0x00);

        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(0x11);

        cartridge.WriteRom(0x4000, 0x01);

        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(0x22);
    }

    [Fact]
    public void WriteRom_NonRumbleMbc5KeepsFourRamBankBits()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc5Ram;
            bytes[0x0149] = 0x04;
        });
        var cartridge = TestRomFactory.LoadCartridge(rom);
        cartridge.WriteRom(0x0000, 0x0A);

        // Pan Docs mbc5.md gives non-rumble cartridges all four RAM-bank bits.
        cartridge.WriteRom(0x4000, 0x08);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x88);
        cartridge.WriteRom(0x4000, 0x00);

        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(0x00);
        cartridge.WriteRom(0x4000, 0x08);
        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(0x88);
        cartridge.IsRumbleActive.Should().BeFalse();
    }

    [Fact]
    public void WriteRom_RumbleMbc5UsesBitThreeForMotorAndThreeBitsForRamBank()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc5RumbleRam;
            bytes[0x0149] = 0x04;
        });
        var cartridge = TestRomFactory.LoadCartridge(rom);
        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x10);

        // Pan Docs mbc5.md assigns bit 3 to rumble and bits 0-2 to RAM banking.
        cartridge.WriteRom(0x4000, 0x08);

        cartridge.IsRumbleActive.Should().BeTrue();
        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(0x10);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x80);

        cartridge.WriteRom(0x4000, 0x00);
        cartridge.IsRumbleActive.Should().BeFalse();
        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(0x80);

        cartridge.WriteRom(0x4000, 0x0F);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x77);
        cartridge.WriteRom(0x4000, 0x07);

        cartridge.IsRumbleActive.Should().BeFalse();
        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(0x77);
    }

    [Fact]
    public void BatterySave_ExportsAndImportsMbc5RamBanks()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc5RamBattery;
            bytes[0x0149] = 0x03;
        });
        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x11);
        cartridge.WriteRom(0x4000, 0x01);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x22);

        var save = cartridge.ExportBatterySave();

        cartridge.HasBatteryBackedSave.Should().BeTrue();
        cartridge.BatterySaveSize.Should().Be(32 * 1024);
        cartridge.IsBatterySaveDirty.Should().BeTrue();
        save[0].Should().Be(0x11);
        save[AddressMap.ExternalRamWindowSize].Should().Be(0x22);

        var reloaded = TestRomFactory.LoadCartridge(rom);
        var import = reloaded.TryImportBatterySave(save, out var errorMessage);

        import.Should().BeTrue(errorMessage);
        reloaded.IsBatterySaveDirty.Should().BeFalse();

        reloaded.WriteRom(0x0000, 0x0A);
        reloaded.ReadRam(AddressMap.ExternalRamStart).Should().Be(0x11);

        reloaded.WriteRom(0x4000, 0x01);
        reloaded.ReadRam(AddressMap.ExternalRamStart).Should().Be(0x22);
    }

    [Theory]
    [InlineData(CartridgeType.Mbc5Ram)]
    [InlineData(CartridgeType.Mbc5RamBattery)]
    public void CaptureRestore_ContinuesFullMbc5MapperAndRamState(CartridgeType cartridgeType)
    {
        const int bank23 = 0x023;
        const int bank100 = 0x100;
        const int bank123 = 0x123;
        const ushort ramOffset = 0x0010;
        const ushort ramAddress = AddressMap.ExternalRamStart + ramOffset;

        var rom = TestRomFactory.Create(
            romSizeCode: 0x08,
            bytes =>
            {
                bytes[0x0147] = (byte)cartridgeType;
                bytes[0x0149] = 0x03;
                bytes[0 * RomBankSize] = 0x00;
                bytes[bank23 * RomBankSize] = 0x23;
                bytes[bank100 * RomBankSize] = 0x80;
                bytes[bank123 * RomBankSize] = 0xA3;
            }
        );
        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRom(0x4000, 0x00);
        cartridge.WriteRam(ramAddress, 0x11);
        cartridge.WriteRom(0x4000, 0x0F);
        cartridge.WriteRam(ramAddress, 0x7F);
        cartridge.WriteRom(0x2000, 0x23);
        cartridge.WriteRom(0x3000, 0x01);

        var state = cartridge.CaptureState();

        cartridge.WriteRom(0x2000, 0x00);
        cartridge.WriteRom(0x3000, 0x00);
        cartridge.WriteRom(0x4000, 0x00);
        cartridge.WriteRam(ramAddress, 0xAA);
        cartridge.ClearBatterySaveDirty();

        cartridge.RestoreState(state);

        cartridge.ReadRom(0x4000).Should().Be(0xA3);
        cartridge.ReadRam(ramAddress).Should().Be(0x7F);
        cartridge.WriteRom(0x4000, 0x00);
        cartridge.ReadRam(ramAddress).Should().Be(0x11);
        cartridge.WriteRom(0x4000, 0x0F);
        cartridge.IsBatterySaveDirty.Should().Be(cartridgeType == CartridgeType.Mbc5RamBattery);

        cartridge.WriteRom(0x2000, 0x00);
        cartridge.ReadRom(0x4000).Should().Be(0x80);
        cartridge.WriteRom(0x2000, 0x23);
        cartridge.WriteRom(0x3000, 0x00);
        cartridge.ReadRom(0x4000).Should().Be(0x23);
        cartridge.WriteRom(0x2000, 0x00);
        cartridge.ReadRom(0x4000).Should().Be(0x00);

        cartridge.WriteRom(0x3000, 0x01);
        var zeroLowState = cartridge.CaptureState();
        cartridge.WriteRom(0x3000, 0x00);
        cartridge.RestoreState(zeroLowState);
        cartridge.ReadRom(0x4000).Should().Be(0x80);

        var validState = cartridge.CaptureState();
        var validMbc5State = (Mbc5MemoryControllerState)validState.Controller;
        var invalidRomHighState = new CartridgeState(validMbc5State with { RomBankHigh = 0x02 });
        var invalidRamBankState = new CartridgeState(validMbc5State with { RamBank = 0x10 });
        var invalidRumbleState = new CartridgeState(validMbc5State with { IsRumbleActive = true });

        FluentActions
            .Invoking(() => cartridge.RestoreState(invalidRomHighState))
            .Should()
            .ThrowExactly<ArgumentException>();
        FluentActions
            .Invoking(() => cartridge.RestoreState(invalidRamBankState))
            .Should()
            .ThrowExactly<ArgumentException>();
        FluentActions
            .Invoking(() => cartridge.RestoreState(invalidRumbleState))
            .Should()
            .ThrowExactly<ArgumentException>();
        cartridge.ReadRom(0x4000).Should().Be(0x80);
        cartridge.ReadRam(ramAddress).Should().Be(0x7F);
        cartridge.IsRumbleActive.Should().BeFalse();
        cartridge.IsBatterySaveDirty.Should().Be(cartridgeType == CartridgeType.Mbc5RamBattery);
    }

    [Fact]
    public void CaptureRestore_RumbleStateAndRamBankAreAtomic()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc5RumbleRamBattery;
            bytes[0x0149] = 0x04;
        });
        var cartridge = TestRomFactory.LoadCartridge(rom);
        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRom(0x4000, 0x0B);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x33);
        var state = cartridge.CaptureState();

        cartridge.WriteRom(0x4000, 0x01);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x11);
        cartridge.RestoreState(state);

        cartridge.IsRumbleActive.Should().BeTrue();
        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(0x33);
        var restoredState = (Mbc5MemoryControllerState)cartridge.CaptureState().Controller;
        restoredState.RamBank.Should().Be(0x03);
        restoredState.IsRumbleActive.Should().BeTrue();

        var invalidState = new CartridgeState(restoredState with { RamBank = 0x08 });
        FluentActions
            .Invoking(() => cartridge.RestoreState(invalidState))
            .Should()
            .ThrowExactly<ArgumentException>();

        cartridge.IsRumbleActive.Should().BeTrue();
        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(0x33);
    }
}
