// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cartridges;
using GbcNet.Core.Cartridges.Memory;
using GbcNet.Core.Memory;

namespace GbcNet.Tests.Unit.Cartridges;

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

        cartridge.Header.CartridgeType.Should().Be(cartridgeType);
        cartridge.Header.RamSizeBytes.Should().Be(8 * 1024);
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

        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(0x42);
    }

    [Fact]
    public void ReadWriteRam_ReturnsFFWhenNoRomRamIsConnected()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0147] = (byte)CartridgeType.RomRam);
        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);

        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(0xFF);
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

        cartridge.HasBatteryBackedSave.Should().BeFalse();
        cartridge.BatterySaveSize.Should().Be(0);
        cartridge.IsBatterySaveDirty.Should().BeFalse();
        cartridge.ExportBatterySave().Should().BeEmpty();
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

        cartridge.HasBatteryBackedSave.Should().BeTrue();
        cartridge.BatterySaveSize.Should().Be(8 * 1024);
        cartridge.IsBatterySaveDirty.Should().BeTrue();
        save[0].Should().Be(0x11);
        save[0x0100].Should().Be(0x22);

        var reloaded = TestRomFactory.LoadCartridge(rom);
        var import = reloaded.TryImportBatterySave(save, out var errorMessage);

        import.Should().BeTrue(errorMessage);
        reloaded.IsBatterySaveDirty.Should().BeFalse();
        reloaded.ReadRam(AddressMap.ExternalRamStart).Should().Be(0x11);
        reloaded.ReadRam(AddressMap.ExternalRamStart + 0x0100).Should().Be(0x22);

        reloaded.WriteRam(AddressMap.ExternalRamStart, 0x33);
        reloaded.IsBatterySaveDirty.Should().BeTrue();

        reloaded.ClearBatterySaveDirty();
        reloaded.IsBatterySaveDirty.Should().BeFalse();
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

        result.Should().BeFalse();
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

        restored.ReadRamOffset(0).Should().Be(0x11);
        restored.ReadRamOffset(0x0100).Should().Be(0x22);
        restored.SaveData.IsBatterySaveDirty.Should().BeFalse();

        restored.WriteRamOffset(0, 0x44);
        var restoredAgain = CreateController(CartridgeType.RomRam);
        restoredAgain.RestoreState(state);

        restoredAgain.ReadRamOffset(0).Should().Be(0x11);
        restoredAgain.ReadRamOffset(0x0100).Should().Be(0x22);
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

        controller.ReadRamOffset(0).Should().Be(0x00);
        controller.SaveData.IsBatterySaveDirty.Should().BeFalse();

        controller.RestoreState(dirtyState);

        controller.ReadRamOffset(0).Should().Be(0x11);
        controller.SaveData.IsBatterySaveDirty.Should().BeTrue();
    }

    [Fact]
    public void RestoreState_RejectsInvalidRamLengthWithoutMutating()
    {
        var controller = CreateController(CartridgeType.RomRamBattery);
        controller.WriteRamOffset(0, 0x5A);
        var invalidState = new NoMbcMemoryControllerState(
            new CartridgeRamState(new byte[1], IsDirty: false)
        );

        FluentActions
            .Invoking(() => controller.RestoreState(invalidState))
            .Should()
            .ThrowExactly<ArgumentException>();

        controller.ReadRamOffset(0).Should().Be(0x5A);
        controller.SaveData.IsBatterySaveDirty.Should().BeTrue();
    }

    [Fact]
    public void RestoreState_RejectsDirtyVolatileRamWithoutMutating()
    {
        var controller = CreateController(CartridgeType.RomRam);
        controller.WriteRamOffset(0, 0x5A);
        var invalidState = new NoMbcMemoryControllerState(
            new CartridgeRamState(new byte[8 * 1024], IsDirty: true)
        );

        FluentActions
            .Invoking(() => controller.RestoreState(invalidState))
            .Should()
            .ThrowExactly<ArgumentException>();

        controller.ReadRamOffset(0).Should().Be(0x5A);
        controller.SaveData.IsBatterySaveDirty.Should().BeFalse();
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
