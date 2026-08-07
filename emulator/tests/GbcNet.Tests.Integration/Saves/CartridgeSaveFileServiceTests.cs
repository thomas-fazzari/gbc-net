// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using GbcNet.App.Saves;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Memory;

namespace GbcNet.Tests.Integration.Saves;

public sealed class CartridgeBatterySaveFileServiceTests
{
    [Fact]
    public async Task SaveAsyncAndLoad_PersistsBatterySaveByTitleAndRomHash()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var rom = CreateBatteryBackedMbc1Rom();
        CartridgeBatterySaveFileService saveFiles = new(tempDirectory.Path);

        var cartridge = TestRomFactory.LoadCartridge(rom);
        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);
        var savePath = saveFiles.Load(cartridge, rom);

        savePath.Should().NotBeNull();
        await saveFiles.SaveAsync(savePath, cartridge.ExportBatterySave());
        File.Exists(savePath).Should().BeTrue();
        Path.GetFileName(savePath).Should().StartWith("TEST_ROM-");

        var reloaded = TestRomFactory.LoadCartridge(rom);
        var reloadedSavePath = saveFiles.Load(reloaded, rom);

        reloadedSavePath.Should().Be(savePath);
        reloaded.IsBatterySaveDirty.Should().BeFalse();
        reloaded.WriteRom(0x0000, 0x0A);
        reloaded.ReadRam(AddressMap.ExternalRamStart).Should().Be(0x42);
    }

    [Fact]
    public void Load_RejectsInvalidSaveSize()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var rom = CreateBatteryBackedMbc1Rom();
        CartridgeBatterySaveFileService saveFiles = new(tempDirectory.Path);

        Directory.CreateDirectory(tempDirectory.Path);
        var cartridge = TestRomFactory.LoadCartridge(rom);
        File.WriteAllBytes(saveFiles.GetBatterySavePath(cartridge, rom), [0x42]);

        FluentActions
            .Invoking(() => saveFiles.Load(cartridge, rom))
            .Should()
            .ThrowExactly<InvalidOperationException>()
            .Which.Message.Should()
            .Be(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Save file is 1 bytes, but cartridge expects {cartridge.BatterySaveSize} bytes."
                )
            );
    }

    private static byte[] CreateBatteryBackedMbc1Rom() =>
        TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc1RamBattery;
            bytes[0x0149] = 0x02;
        });
}
