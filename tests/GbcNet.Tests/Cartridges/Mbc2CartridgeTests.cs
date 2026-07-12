// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cartridges;
using GbcNet.Core.Cartridges.Memory;
using GbcNet.Core.Memory;

namespace GbcNet.Tests.Cartridges;

public sealed class Mbc2CartridgeTests
{
    private const int RomBankSize = Cartridge.FixedRomBankSize;

    [Theory]
    [InlineData(CartridgeType.Mbc2)]
    [InlineData(CartridgeType.Mbc2Battery)]
    public void Load_AcceptsMbc2Cartridge(CartridgeType cartridgeType)
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0147] = (byte)cartridgeType);

        var cartridge = TestRomFactory.LoadCartridge(rom);

        Assert.Equal(cartridgeType, cartridge.Header.CartridgeType);
    }

    [Fact]
    public void WriteRom_UsesAddressBit8ClearForMbc2RamEnable()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0147] = (byte)CartridgeType.Mbc2);
        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.WriteRom(0x0100, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x02);

        Assert.Equal(0xFF, cartridge.ReadRam(AddressMap.ExternalRamStart));

        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x02);

        Assert.Equal(0xF2, cartridge.ReadRam(AddressMap.ExternalRamStart));

        cartridge.WriteRom(0x0000, 0x00);

        Assert.Equal(0xFF, cartridge.ReadRam(AddressMap.ExternalRamStart));
    }

    [Fact]
    public void WriteRom_UsesAddressBit8SetForMbc2RomBank()
    {
        var rom = TestRomFactory.Create(
            romSizeCode: 0x03,
            bytes =>
            {
                bytes[0x0147] = (byte)CartridgeType.Mbc2;
                bytes[1 * RomBankSize] = 0x11;
                bytes[2 * RomBankSize] = 0x22;
            }
        );
        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.WriteRom(0x2000, 0x02);

        Assert.Equal(0x11, cartridge.ReadRom(0x4000));

        cartridge.WriteRom(0x2100, 0x02);

        Assert.Equal(0x22, cartridge.ReadRom(0x4000));
    }

    [Fact]
    public void WriteRom_TreatsMbc2RomBankZeroAsOne()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc2;
            bytes[0 * RomBankSize] = 0x00;
            bytes[1 * RomBankSize] = 0x11;
        });
        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.WriteRom(0x2100, 0x00);

        Assert.Equal(0x11, cartridge.ReadRom(0x4000));
    }

    [Fact]
    public void ReadRam_ReturnsMbc2StoredNibbleWithHighNibbleSet()
    {
        var cartridge = LoadMbc2WithEnabledRam(CartridgeType.Mbc2);

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x05);

        Assert.Equal(0xF5, cartridge.ReadRam(AddressMap.ExternalRamStart));
    }

    [Fact]
    public void WriteRam_StoresOnlyMbc2LowNibble()
    {
        var cartridge = LoadMbc2WithEnabledRam(CartridgeType.Mbc2);

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0xAB);

        Assert.Equal(0xFB, cartridge.ReadRam(AddressMap.ExternalRamStart));
    }

    [Fact]
    public void ReadWriteRam_MirrorsMbc2RamByLowNineAddressBits()
    {
        var cartridge = LoadMbc2WithEnabledRam(CartridgeType.Mbc2);

        cartridge.WriteRam(AddressMap.ExternalRamStart + 0x0201, 0x07);

        Assert.Equal(0xF7, cartridge.ReadRam(AddressMap.ExternalRamStart + 0x0001));
    }

    [Fact]
    public void BatterySave_ExportsAndImportsMbc2Ram()
    {
        var cartridge = LoadMbc2WithEnabledRam(CartridgeType.Mbc2Battery);

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0xAB);
        cartridge.WriteRam(AddressMap.ExternalRamStart + 1, 0x0C);

        var save = cartridge.ExportBatterySave();

        Assert.True(cartridge.HasBatteryBackedSave);
        Assert.Equal(512, cartridge.BatterySaveSize);
        Assert.True(cartridge.IsBatterySaveDirty);
        Assert.Equal(512, save.Length);
        Assert.Equal(0x0B, save[0]);
        Assert.Equal(0x0C, save[1]);

        save[1] = 0xBC;
        var reloaded = LoadMbc2WithEnabledRam(CartridgeType.Mbc2Battery);
        var import = reloaded.TryImportBatterySave(save, out var errorMessage);

        Assert.True(import, errorMessage);
        Assert.False(reloaded.IsBatterySaveDirty);
        Assert.Equal(0xFB, reloaded.ReadRam(AddressMap.ExternalRamStart));
        Assert.Equal(0xFC, reloaded.ReadRam(AddressMap.ExternalRamStart + 1));
    }

    [Fact]
    public void BatterySave_IsUnavailableForMbc2WithoutBattery()
    {
        var cartridge = LoadMbc2WithEnabledRam(CartridgeType.Mbc2);

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x0B);

        Assert.False(cartridge.HasBatteryBackedSave);
        Assert.Equal(0, cartridge.BatterySaveSize);
        Assert.False(cartridge.IsBatterySaveDirty);
        Assert.Empty(cartridge.ExportBatterySave());
    }

    [Fact]
    public void BatterySave_RejectsInvalidMbc2SaveSize()
    {
        var cartridge = LoadMbc2WithEnabledRam(CartridgeType.Mbc2Battery);

        var result = cartridge.TryImportBatterySave(new byte[1], out _);

        Assert.False(result);
    }

    [Fact]
    public void State_RestoresEnabledMbc2ContinuationAndCapturedRamIsIndependent()
    {
        var controller = CreateMbc2Controller(
            CartridgeType.Mbc2Battery,
            0x03,
            bytes =>
            {
                bytes[2 * RomBankSize] = 0x22;
                bytes[3 * RomBankSize] = 0x33;
            }
        );
        controller.WriteRom(0x2100, 0x03);
        controller.WriteRom(0x0000, 0x0A);
        controller.WriteRamOffset(0x0201, 0xAB);

        var state = controller.CaptureState();
        var restoreState = controller.CaptureState();
        ((Mbc2MemoryControllerState)state).Ram.Bytes[1] = 0x00;

        Assert.Equal(0xFB, controller.ReadRamOffset(0x0001));
        Assert.True(controller.SaveData.IsBatterySaveDirty);

        controller.WriteRom(0x2100, 0x02);
        controller.WriteRom(0x0000, 0x00);
        controller.WriteRom(0x0000, 0x0A);
        controller.WriteRamOffset(0x0001, 0x04);
        controller.SaveData.ClearBatterySaveDirty();

        controller.RestoreState(restoreState);

        Assert.Equal(0x33, controller.ReadRom(0x4000));
        Assert.Equal(0xFB, controller.ReadRamOffset(0x0201));
        Assert.True(controller.SaveData.IsBatterySaveDirty);

        controller.WriteRamOffset(0x0001, 0xAC);

        Assert.Equal(0xFC, controller.ReadRamOffset(0x0201));
    }

    [Fact]
    public void State_RestoresDisabledMbc2Continuation()
    {
        var controller = CreateMbc2Controller(
            CartridgeType.Mbc2,
            0x03,
            bytes =>
            {
                bytes[2 * RomBankSize] = 0x22;
                bytes[3 * RomBankSize] = 0x33;
            }
        );
        controller.WriteRom(0x2100, 0x02);
        controller.WriteRom(0x0000, 0x0A);
        controller.WriteRamOffset(0x0001, 0x0B);
        controller.WriteRom(0x0000, 0x00);

        var state = controller.CaptureState();

        controller.WriteRom(0x2100, 0x03);
        controller.WriteRom(0x0000, 0x0A);
        controller.WriteRamOffset(0x0001, 0x0C);

        controller.RestoreState(state);

        Assert.Equal(0x22, controller.ReadRom(0x4000));
        Assert.Equal(0xFF, controller.ReadRamOffset(0x0001));

        controller.WriteRamOffset(0x0001, 0x0D);
        controller.WriteRom(0x0000, 0x0A);

        Assert.Equal(0xFB, controller.ReadRamOffset(0x0001));
    }

    [Fact]
    public void State_RestoresAllVolatileMbc2NibblesWithoutDirtying()
    {
        var controller = CreateMbc2Controller(CartridgeType.Mbc2);
        controller.WriteRom(0x0000, 0x0A);
        for (ushort offset = 0; offset < 512; offset++)
        {
            controller.WriteRamOffset(offset, (byte)offset);
        }

        var state = controller.CaptureState();
        for (ushort offset = 0; offset < 512; offset++)
        {
            controller.WriteRamOffset(offset, 0);
        }

        controller.RestoreState(state);

        Assert.False(controller.SaveData.IsBatterySaveDirty);
        for (ushort offset = 0; offset < 512; offset++)
        {
            Assert.Equal((byte)(0xF0 | (offset & 0x0F)), controller.ReadRamOffset(offset));
        }

        var dirtyState = new Mbc2MemoryControllerState(
            new Mbc2RamState(((Mbc2MemoryControllerState)state).Ram.Bytes, true),
            1,
            true
        );

        Assert.Throws<ArgumentException>(() => controller.RestoreState(dirtyState));
        Assert.False(controller.SaveData.IsBatterySaveDirty);
    }

    [Fact]
    public void State_RejectsInvalidMbc2NibbleWithoutMutatingContinuation()
    {
        var controller = CreateMbc2Controller(
            CartridgeType.Mbc2Battery,
            0x03,
            bytes =>
            {
                bytes[2 * RomBankSize] = 0x22;
                bytes[3 * RomBankSize] = 0x33;
            }
        );
        controller.WriteRom(0x2100, 0x02);
        controller.WriteRom(0x0000, 0x0A);
        controller.WriteRamOffset(0x0001, 0x07);

        var bytes = (byte[])
            ((Mbc2MemoryControllerState)controller.CaptureState()).Ram.Bytes.Clone();
        bytes[511] = 0x10;
        var invalidState = new Mbc2MemoryControllerState(
            new Mbc2RamState(bytes, true),
            0x03,
            false
        );

        Assert.Throws<ArgumentException>(() => controller.RestoreState(invalidState));

        Assert.Equal(0x22, controller.ReadRom(0x4000));
        Assert.Equal(0xF7, controller.ReadRamOffset(0x0001));
        Assert.True(controller.SaveData.IsBatterySaveDirty);
    }

    private static Mbc2MemoryController CreateMbc2Controller(
        CartridgeType cartridgeType,
        byte romSizeCode = 0x00,
        Action<byte[]>? configure = null
    )
    {
        var rom = TestRomFactory.Create(
            romSizeCode,
            bytes =>
            {
                bytes[0x0147] = (byte)cartridgeType;
                configure?.Invoke(bytes);
            }
        );
        var header = TestRomFactory.LoadCartridge(rom).Header;
        return new Mbc2MemoryController(rom, header, cartridgeType is CartridgeType.Mbc2Battery);
    }

    private static Cartridge LoadMbc2WithEnabledRam(CartridgeType cartridgeType)
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0147] = (byte)cartridgeType);
        var cartridge = TestRomFactory.LoadCartridge(rom);
        cartridge.WriteRom(0x0000, 0x0A);
        return cartridge;
    }
}
