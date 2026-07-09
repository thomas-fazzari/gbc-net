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
        var tempDirectory = TestDirectories.GetTemporaryDirectoryPath();
        var rom = CreateBatteryBackedMbc1Rom();
        CartridgeBatterySaveFileService saveFiles = new(tempDirectory);

        try
        {
            var cartridge = LoadCartridge(rom);
            cartridge.WriteRom(0x0000, 0x0A);
            cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);

            saveFiles.Save(cartridge, rom);
            Assert.False(cartridge.IsBatterySaveDirty);
            var savePath = saveFiles.GetBatterySavePath(cartridge, rom);
            Assert.True(File.Exists(savePath));
            Assert.StartsWith("TEST_ROM-", Path.GetFileName(savePath), StringComparison.Ordinal);

            var reloaded = LoadCartridge(rom);
            saveFiles.Load(reloaded, rom);
            Assert.False(reloaded.IsBatterySaveDirty);

            reloaded.WriteRom(0x0000, 0x0A);
            Assert.Equal(0x42, reloaded.ReadRam(AddressMap.ExternalRamStart));
        }
        finally
        {
            TestDirectories.DeleteDirectoryIfExists(tempDirectory);
        }
    }

    [Fact]
    public void Load_RejectsInvalidSaveSize()
    {
        var tempDirectory = TestDirectories.GetTemporaryDirectoryPath();
        var rom = CreateBatteryBackedMbc1Rom();
        CartridgeBatterySaveFileService saveFiles = new(tempDirectory);

        try
        {
            Directory.CreateDirectory(tempDirectory);
            var cartridge = LoadCartridge(rom);
            File.WriteAllBytes(saveFiles.GetBatterySavePath(cartridge, rom), [0x42]);

            Assert.Equal(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Save file is 1 bytes, but cartridge expects {cartridge.BatterySaveSize} bytes."
                ),
                Assert
                    .Throws<InvalidOperationException>(() => saveFiles.Load(cartridge, rom))
                    .Message
            );
        }
        finally
        {
            TestDirectories.DeleteDirectoryIfExists(tempDirectory);
        }
    }

    private static Cartridge LoadCartridge(byte[] rom) =>
        Cartridge.Load(rom).Cartridge
        ?? throw new InvalidOperationException("Test ROM failed to load.");

    private static byte[] CreateBatteryBackedMbc1Rom() =>
        TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc1RamBattery;
            bytes[0x0149] = 0x02;
        });
}
