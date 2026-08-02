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
    public void Load_AcceptsMbc5Cartridge(CartridgeType cartridgeType)
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0147] = (byte)cartridgeType);

        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.Header.CartridgeType.Should().Be(cartridgeType);
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

        FluentActions
            .Invoking(() => cartridge.RestoreState(invalidRomHighState))
            .Should()
            .ThrowExactly<ArgumentException>();
        FluentActions
            .Invoking(() => cartridge.RestoreState(invalidRamBankState))
            .Should()
            .ThrowExactly<ArgumentException>();
        cartridge.ReadRom(0x4000).Should().Be(0x80);
        cartridge.ReadRam(ramAddress).Should().Be(0x7F);
        cartridge.IsBatterySaveDirty.Should().Be(cartridgeType == CartridgeType.Mbc5RamBattery);
    }
}
