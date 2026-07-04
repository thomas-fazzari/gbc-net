// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using FluentResults;
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
            var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));
            cartridge.WriteRom(0x0000, 0x0A);
            cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);

            var save = saveFiles.Save(cartridge, rom);

            AssertSuccess(save);
            Assert.False(cartridge.IsBatterySaveDirty);
            var savePath = saveFiles.GetBatterySavePath(cartridge, rom);
            Assert.True(File.Exists(savePath));
            Assert.StartsWith("TEST_ROM-", Path.GetFileName(savePath), StringComparison.Ordinal);

            var reloaded = ResultAssertions.AssertSuccess(Cartridge.Load(rom));
            var load = saveFiles.Load(reloaded, rom);

            AssertSuccess(load);
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
            var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));
            File.WriteAllBytes(saveFiles.GetBatterySavePath(cartridge, rom), [0x42]);

            var load = saveFiles.Load(cartridge, rom);

            Assert.True(load.IsFailed);
        }
        finally
        {
            TestDirectories.DeleteDirectoryIfExists(tempDirectory);
        }
    }

    private static byte[] CreateBatteryBackedMbc1Rom() =>
        TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc1RamBattery;
            bytes[0x0149] = 0x02;
        });

    private static void AssertSuccess(Result result)
    {
        Assert.True(
            result.IsSuccess,
            string.Join(Environment.NewLine, result.Errors.Select(static error => error.Message))
        );
    }
}
