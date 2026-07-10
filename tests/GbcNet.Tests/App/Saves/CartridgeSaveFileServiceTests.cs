// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using GbcNet.App.Saves;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Memory;
using GbcNet.Tests.Cartridges;

namespace GbcNet.Tests.App.Saves;

public sealed class CartridgeBatterySaveFileServiceTests
{
    [Fact]
    public void SaveAndLoad_PersistsBatterySaveByTitleAndRomHash()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var rom = CreateBatteryBackedMbc1Rom();
        CartridgeBatterySaveFileService saveFiles = new(tempDirectory.Path);

        var cartridge = TestRomFactory.LoadCartridge(rom);
        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);

        saveFiles.Save(cartridge, rom);
        Assert.False(cartridge.IsBatterySaveDirty);
        var savePath = saveFiles.GetBatterySavePath(cartridge, rom);
        Assert.True(File.Exists(savePath));
        Assert.StartsWith("TEST_ROM-", Path.GetFileName(savePath), StringComparison.Ordinal);

        var reloaded = TestRomFactory.LoadCartridge(rom);
        saveFiles.Load(reloaded, rom);
        Assert.False(reloaded.IsBatterySaveDirty);

        reloaded.WriteRom(0x0000, 0x0A);
        Assert.Equal(0x42, reloaded.ReadRam(AddressMap.ExternalRamStart));
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

        Assert.Equal(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Save file is 1 bytes, but cartridge expects {cartridge.BatterySaveSize} bytes."
            ),
            Assert.Throws<InvalidOperationException>(() => saveFiles.Load(cartridge, rom)).Message
        );
    }

    private static byte[] CreateBatteryBackedMbc1Rom() =>
        TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc1RamBattery;
            bytes[0x0149] = 0x02;
        });
}
