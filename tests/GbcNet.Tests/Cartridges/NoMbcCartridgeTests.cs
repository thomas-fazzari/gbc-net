// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cartridges;
using GbcNet.Core.Cartridges.Memory;
using GbcNet.Core.Memory;

namespace GbcNet.Tests.Cartridges;

public sealed class NoMbcCartridgeTests
{
    [Theory]
    [InlineData(CartridgeType.RomRam)]
    [InlineData(CartridgeType.RomRamBattery)]
    public void Load_AcceptsRomRamCartridge(CartridgeType cartridgeType)
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)cartridgeType;
            bytes[0x0149] = 0x02;
        });

        var cartridge = TestRomFactory.LoadCartridge(rom);

        Assert.Equal(cartridgeType, cartridge.Header.CartridgeType);
        Assert.Equal(8 * 1024, cartridge.Header.RamSizeBytes);
    }

    [Fact]
    public void ReadWriteRam_UsesFixedRomRamBankWithoutEnableRegister()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.RomRam;
            bytes[0x0149] = 0x02;
        });
        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);

        Assert.Equal(0x42, cartridge.ReadRam(AddressMap.ExternalRamStart));
    }

    [Fact]
    public void ReadWriteRam_ReturnsFFWhenNoRomRamIsConnected()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0147] = (byte)CartridgeType.RomRam);
        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);

        Assert.Equal(0xFF, cartridge.ReadRam(AddressMap.ExternalRamStart));
    }

    [Fact]
    public void BatterySave_IsUnavailableForRomRamWithoutBattery()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.RomRam;
            bytes[0x0149] = 0x02;
        });
        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);

        Assert.False(cartridge.HasBatteryBackedSave);
        Assert.Equal(0, cartridge.BatterySaveSize);
        Assert.False(cartridge.IsBatterySaveDirty);
        Assert.Empty(cartridge.ExportBatterySave());
    }

    [Fact]
    public void BatterySave_ExportsAndImportsRomRamBattery()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.RomRamBattery;
            bytes[0x0149] = 0x02;
        });
        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x11);
        cartridge.WriteRam(AddressMap.ExternalRamStart + 0x0100, 0x22);

        var save = cartridge.ExportBatterySave();

        Assert.True(cartridge.HasBatteryBackedSave);
        Assert.Equal(8 * 1024, cartridge.BatterySaveSize);
        Assert.True(cartridge.IsBatterySaveDirty);
        Assert.Equal(0x11, save[0]);
        Assert.Equal(0x22, save[0x0100]);

        var reloaded = TestRomFactory.LoadCartridge(rom);
        var import = reloaded.TryImportBatterySave(save, out var errorMessage);

        Assert.True(import, errorMessage);
        Assert.False(reloaded.IsBatterySaveDirty);
        Assert.Equal(0x11, reloaded.ReadRam(AddressMap.ExternalRamStart));
        Assert.Equal(0x22, reloaded.ReadRam(AddressMap.ExternalRamStart + 0x0100));

        reloaded.WriteRam(AddressMap.ExternalRamStart, 0x33);
        Assert.True(reloaded.IsBatterySaveDirty);

        reloaded.ClearBatterySaveDirty();
        Assert.False(reloaded.IsBatterySaveDirty);
    }

    [Fact]
    public void BatterySave_RejectsInvalidRomRamBatterySaveSize()
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.RomRamBattery;
            bytes[0x0149] = 0x02;
        });
        var cartridge = TestRomFactory.LoadCartridge(rom);

        var result = cartridge.TryImportBatterySave(new byte[1], out _);

        Assert.False(result);
    }

    [Fact]
    public void CaptureRestoreState_RestoresVolatileRamAndCanReuseSnapshot()
    {
        var source = CreateController(CartridgeType.RomRam);
        source.WriteRamOffset(0, 0x11);
        source.WriteRamOffset(0x0100, 0x22);
        var state = source.CaptureState();
        source.WriteRamOffset(0, 0x33);

        var restored = CreateController(CartridgeType.RomRam);
        restored.RestoreState(state);

        Assert.Equal(0x11, restored.ReadRamOffset(0));
        Assert.Equal(0x22, restored.ReadRamOffset(0x0100));
        Assert.False(restored.SaveData.IsBatterySaveDirty);

        restored.WriteRamOffset(0, 0x44);
        var restoredAgain = CreateController(CartridgeType.RomRam);
        restoredAgain.RestoreState(state);

        Assert.Equal(0x11, restoredAgain.ReadRamOffset(0));
        Assert.Equal(0x22, restoredAgain.ReadRamOffset(0x0100));
    }

    [Fact]
    public void CaptureRestoreState_PreservesCleanAndDirtyBatteryRam()
    {
        var controller = CreateController(CartridgeType.RomRamBattery);
        var cleanState = controller.CaptureState();
        controller.WriteRamOffset(0, 0x11);
        var dirtyState = controller.CaptureState();
        controller.SaveData.ClearBatterySaveDirty();
        controller.WriteRamOffset(0, 0x22);

        controller.RestoreState(cleanState);

        Assert.Equal(0x00, controller.ReadRamOffset(0));
        Assert.False(controller.SaveData.IsBatterySaveDirty);

        controller.RestoreState(dirtyState);

        Assert.Equal(0x11, controller.ReadRamOffset(0));
        Assert.True(controller.SaveData.IsBatterySaveDirty);
    }

    [Fact]
    public void RestoreState_RejectsInvalidRamLengthWithoutMutating()
    {
        var controller = CreateController(CartridgeType.RomRamBattery);
        controller.WriteRamOffset(0, 0x5A);
        var invalidState = new NoMbcMemoryControllerState(
            new CartridgeRamState(new byte[1], IsDirty: false)
        );

        Assert.Throws<ArgumentException>(() => controller.RestoreState(invalidState));

        Assert.Equal(0x5A, controller.ReadRamOffset(0));
        Assert.True(controller.SaveData.IsBatterySaveDirty);
    }

    [Fact]
    public void RestoreState_RejectsDirtyVolatileRamWithoutMutating()
    {
        var controller = CreateController(CartridgeType.RomRam);
        controller.WriteRamOffset(0, 0x5A);
        var invalidState = new NoMbcMemoryControllerState(
            new CartridgeRamState(new byte[8 * 1024], IsDirty: true)
        );

        Assert.Throws<ArgumentException>(() => controller.RestoreState(invalidState));

        Assert.Equal(0x5A, controller.ReadRamOffset(0));
        Assert.False(controller.SaveData.IsBatterySaveDirty);
    }

    private static NoMbcMemoryController CreateController(CartridgeType cartridgeType)
    {
        var rom = TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)cartridgeType;
            bytes[0x0149] = 0x02;
        });
        var header = TestRomFactory.LoadCartridge(rom).Header;
        return new NoMbcMemoryController(rom, header, cartridgeType == CartridgeType.RomRamBattery);
    }
}
