// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cartridges;
using GbcNet.Core.Cartridges.Memory;
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

        var cartridge = TestRomFactory.LoadCartridge(rom);

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
        var cartridge = TestRomFactory.LoadCartridge(rom);

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
        var cartridge = TestRomFactory.LoadCartridge(rom);

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
        var cartridge = TestRomFactory.LoadCartridge(rom);

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
        var cartridge = TestRomFactory.LoadCartridge(rom);

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
        var cartridge = TestRomFactory.LoadCartridge(rom);

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
        var cartridge = TestRomFactory.LoadCartridge(rom);

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
        var cartridge = TestRomFactory.LoadCartridge(rom);

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

        var reloaded = TestRomFactory.LoadCartridge(rom);
        var import = reloaded.TryImportBatterySave(save, out var errorMessage);

        Assert.True(import, errorMessage);
        Assert.False(reloaded.IsBatterySaveDirty);

        reloaded.WriteRom(0x0000, 0x0A);
        Assert.Equal(0x11, reloaded.ReadRam(AddressMap.ExternalRamStart));

        reloaded.WriteRom(0x4000, 0x01);
        Assert.Equal(0x22, reloaded.ReadRam(AddressMap.ExternalRamStart));
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

        Assert.Equal(0xA3, cartridge.ReadRom(0x4000));
        Assert.Equal(0x7F, cartridge.ReadRam(ramAddress));
        cartridge.WriteRom(0x4000, 0x00);
        Assert.Equal(0x11, cartridge.ReadRam(ramAddress));
        cartridge.WriteRom(0x4000, 0x0F);
        Assert.Equal(cartridgeType == CartridgeType.Mbc5RamBattery, cartridge.IsBatterySaveDirty);

        cartridge.WriteRom(0x2000, 0x00);
        Assert.Equal(0x80, cartridge.ReadRom(0x4000));
        cartridge.WriteRom(0x2000, 0x23);
        cartridge.WriteRom(0x3000, 0x00);
        Assert.Equal(0x23, cartridge.ReadRom(0x4000));
        cartridge.WriteRom(0x2000, 0x00);
        Assert.Equal(0x00, cartridge.ReadRom(0x4000));

        cartridge.WriteRom(0x3000, 0x01);
        var zeroLowState = cartridge.CaptureState();
        cartridge.WriteRom(0x3000, 0x00);
        cartridge.RestoreState(zeroLowState);
        Assert.Equal(0x80, cartridge.ReadRom(0x4000));

        var validState = cartridge.CaptureState();
        var validMbc5State = (Mbc5MemoryControllerState)validState.Controller;
        var invalidRomHighState = new CartridgeState(
            new Mbc5MemoryControllerState(
                validMbc5State.ExternalRam,
                validMbc5State.RomBankLow,
                0x02,
                validMbc5State.RamBank
            )
        );
        var invalidRamBankState = new CartridgeState(
            new Mbc5MemoryControllerState(
                validMbc5State.ExternalRam,
                validMbc5State.RomBankLow,
                validMbc5State.RomBankHigh,
                0x10
            )
        );

        Assert.Throws<ArgumentException>(() => cartridge.RestoreState(invalidRomHighState));
        Assert.Throws<ArgumentException>(() => cartridge.RestoreState(invalidRamBankState));
        Assert.Equal(0x80, cartridge.ReadRom(0x4000));
        Assert.Equal(0x7F, cartridge.ReadRam(ramAddress));
        Assert.Equal(cartridgeType == CartridgeType.Mbc5RamBattery, cartridge.IsBatterySaveDirty);
    }
}
