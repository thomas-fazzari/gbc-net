using FluentResults;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Memory;
using GbcNet.Gui.Saves;
using GbcNet.Tests.Cartridges;

namespace GbcNet.Tests.Gui.Saves;

public sealed class CartridgeSaveFileServiceTests
{
    [Fact]
    public void SaveAndLoad_PersistsBatteryRamByTitleAndRomHash()
    {
        string tempDirectory = CreateTempDirectory();
        byte[] rom = CreateBatteryBackedMbc1Rom();
        var saveFiles = new CartridgeSaveFileService(tempDirectory);

        try
        {
            Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));
            cartridge.WriteRom(0x0000, 0x0A);
            cartridge.WriteRam(AddressMap.ExternalRamStart, 0x42);

            Result save = saveFiles.Save(cartridge, rom);

            AssertSuccess(save);
            Assert.False(cartridge.IsBatteryRamDirty);
            string savePath = saveFiles.GetSavePath(cartridge, rom);
            Assert.True(File.Exists(savePath));
            Assert.StartsWith("TEST_ROM-", Path.GetFileName(savePath), StringComparison.Ordinal);

            Cartridge reloaded = ResultAssertions.AssertSuccess(Cartridge.Load(rom));
            Result load = saveFiles.Load(reloaded, rom);

            AssertSuccess(load);
            Assert.False(reloaded.IsBatteryRamDirty);

            reloaded.WriteRom(0x0000, 0x0A);
            Assert.Equal(0x42, reloaded.ReadRam(AddressMap.ExternalRamStart));
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void Load_RejectsInvalidSaveSize()
    {
        string tempDirectory = CreateTempDirectory();
        byte[] rom = CreateBatteryBackedMbc1Rom();
        var saveFiles = new CartridgeSaveFileService(tempDirectory);

        try
        {
            Directory.CreateDirectory(tempDirectory);
            Cartridge cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(rom));
            File.WriteAllBytes(saveFiles.GetSavePath(cartridge, rom), [0x42]);

            Result load = saveFiles.Load(cartridge, rom);

            Assert.True(load.IsFailed);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private static byte[] CreateBatteryBackedMbc1Rom() =>
        TestRomFactory.Create(bytes =>
        {
            bytes[0x0147] = (byte)CartridgeType.Mbc1RamBattery;
            bytes[0x0149] = 0x02;
        });

    private static string CreateTempDirectory() =>
        Path.Combine(Path.GetTempPath(), "gbc-net-tests", Guid.NewGuid().ToString("N"));

    private static void AssertSuccess(Result result)
    {
        Assert.True(
            result.IsSuccess,
            string.Join(Environment.NewLine, result.Errors.Select(static error => error.Message))
        );
    }
}
