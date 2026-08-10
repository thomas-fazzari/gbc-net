// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Security.Cryptography;
using GbcNet.App.Saves;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace GbcNet.Tests.Integration.Saves;

public sealed class CartridgeBatterySaveFileServiceTests
{
    [Fact]
    public async Task SaveAsyncAndLoad_PersistsBatterySaveByTitleAndRomHash()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        var rom = CreateBatteryBackedMbc1Rom();
        CartridgeBatterySaveFileService saveFiles = new(
            tempDirectory.Path,
            NullLogger<CartridgeBatterySaveFileService>.Instance
        );

        var cartridge = TestRomFactory.LoadCartridge(rom);
        var identity = RomStorageIdentity.Create(cartridge.Header.Title, rom);
        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);
        var savePath = saveFiles.Load(cartridge, identity);

        savePath.Should().NotBeNull();
        await saveFiles.SaveAsync(savePath, cartridge.ExportBatterySave());
        File.Exists(savePath).Should().BeTrue();
        Path.GetFileName(savePath)
            .Should()
            .Be($"TEST_ROM-{Convert.ToHexString(SHA256.HashData(rom))}.sav");

        var reloaded = TestRomFactory.LoadCartridge(rom);
        var reloadedSavePath = saveFiles.Load(reloaded, identity);

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
        CartridgeBatterySaveFileService saveFiles = new(
            tempDirectory.Path,
            NullLogger<CartridgeBatterySaveFileService>.Instance
        );

        Directory.CreateDirectory(tempDirectory.Path);
        var cartridge = TestRomFactory.LoadCartridge(rom);
        var identity = RomStorageIdentity.Create(cartridge.Header.Title, rom);
        File.WriteAllBytes(saveFiles.GetBatterySavePath(identity), [0x42]);

        FluentActions
            .Invoking(() => saveFiles.Load(cartridge, identity))
            .Should()
            .ThrowExactly<InvalidOperationException>();
    }

    [Fact]
    public async Task SaveAsync_WhenCommitFails_CleansTemporaryFileAndPreservesDestination()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        Directory.CreateDirectory(tempDirectory.Path);
        var savePath = Path.Combine(tempDirectory.Path, "existing.sav");
        Directory.CreateDirectory(savePath);
        var sentinelPath = Path.Combine(savePath, "sentinel");
        byte[] originalBytes = [0x11];
        await File.WriteAllBytesAsync(sentinelPath, originalBytes, ct);
        CartridgeBatterySaveFileService saveFiles = new(
            tempDirectory.Path,
            NullLogger<CartridgeBatterySaveFileService>.Instance
        );

        var exception = (
            await FluentActions
                .Awaiting(() => saveFiles.SaveAsync(savePath, new byte[] { 0x42 }))
                .Should()
                .ThrowExactlyAsync<IOException>()
        ).Which;

        exception.InnerException.Should().BeOfType<IOException>();
        var destinationBytes = await File.ReadAllBytesAsync(sentinelPath, ct);
        destinationBytes.Should().Equal(originalBytes);
        Directory.EnumerateFiles(tempDirectory.Path, "existing.sav.*.tmp").Should().BeEmpty();
    }

    private static byte[] CreateBatteryBackedMbc1Rom() =>
        TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc1RamBattery;
            bytes[0x0149] = 0x02;
        });
}
