// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cartridges;
using GbcNet.Core.Cartridges.Memory;
using GbcNet.Core.Memory;

namespace GbcNet.Tests.Unit.Cartridges;

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

        cartridge.Header.CartridgeType.Should().Be(cartridgeType);
    }

    [Fact]
    public void WriteRom_UsesAddressBit8ClearForMbc2RamEnable()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0147] = (byte)CartridgeType.Mbc2);
        var cartridge = TestRomFactory.LoadCartridge(rom);

        cartridge.WriteRom(0x0100, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x02);

        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(0xFF);

        cartridge.WriteRom(0x0000, 0x0A);
        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x02);

        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(0xF2);

        cartridge.WriteRom(0x0000, 0x00);

        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(0xFF);
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

        cartridge.ReadRom(0x4000).Should().Be(0x11);

        cartridge.WriteRom(0x2100, 0x02);

        cartridge.ReadRom(0x4000).Should().Be(0x22);
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

        cartridge.ReadRom(0x4000).Should().Be(0x11);
    }

    [Fact]
    public void ReadRam_ReturnsMbc2StoredNibbleWithHighNibbleSet()
    {
        var cartridge = LoadMbc2WithEnabledRam(CartridgeType.Mbc2);

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x05);

        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(0xF5);
    }

    [Fact]
    public void WriteRam_StoresOnlyMbc2LowNibble()
    {
        var cartridge = LoadMbc2WithEnabledRam(CartridgeType.Mbc2);

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0xAB);

        cartridge.ReadRam(AddressMap.ExternalRamStart).Should().Be(0xFB);
    }

    [Fact]
    public void ReadWriteRam_MirrorsMbc2RamByLowNineAddressBits()
    {
        var cartridge = LoadMbc2WithEnabledRam(CartridgeType.Mbc2);

        cartridge.WriteRam(AddressMap.ExternalRamStart + 0x0201, 0x07);

        cartridge.ReadRam(AddressMap.ExternalRamStart + 0x0001).Should().Be(0xF7);
    }

    [Fact]
    public void BatterySave_ExportsAndImportsMbc2Ram()
    {
        var cartridge = LoadMbc2WithEnabledRam(CartridgeType.Mbc2Battery);

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0xAB);
        cartridge.WriteRam(AddressMap.ExternalRamStart + 1, 0x0C);

        var save = cartridge.ExportBatterySave();

        cartridge.HasBatteryBackedSave.Should().BeTrue();
        cartridge.BatterySaveSize.Should().Be(512);
        cartridge.IsBatterySaveDirty.Should().BeTrue();
        save.Length.Should().Be(512);
        save[0].Should().Be(0x0B);
        save[1].Should().Be(0x0C);

        save[1] = 0xBC;
        var reloaded = LoadMbc2WithEnabledRam(CartridgeType.Mbc2Battery);
        var import = reloaded.TryImportBatterySave(save, out var errorMessage);

        import.Should().BeTrue(errorMessage);
        reloaded.IsBatterySaveDirty.Should().BeFalse();
        reloaded.ReadRam(AddressMap.ExternalRamStart).Should().Be(0xFB);
        reloaded.ReadRam(AddressMap.ExternalRamStart + 1).Should().Be(0xFC);
    }

    [Fact]
    public void BatterySave_IsUnavailableForMbc2WithoutBattery()
    {
        var cartridge = LoadMbc2WithEnabledRam(CartridgeType.Mbc2);

        cartridge.WriteRam(AddressMap.ExternalRamStart, 0x0B);

        cartridge.HasBatteryBackedSave.Should().BeFalse();
        cartridge.BatterySaveSize.Should().Be(0);
        cartridge.IsBatterySaveDirty.Should().BeFalse();
        cartridge.ExportBatterySave().Should().BeEmpty();
    }

    [Fact]
    public void BatterySave_RejectsInvalidMbc2SaveSize()
    {
        var cartridge = LoadMbc2WithEnabledRam(CartridgeType.Mbc2Battery);

        var result = cartridge.TryImportBatterySave(new byte[1], out _);

        result.Should().BeFalse();
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

        controller.ReadRamOffset(0x0001).Should().Be(0xFB);
        controller.SaveData.IsBatterySaveDirty.Should().BeTrue();

        controller.WriteRom(0x2100, 0x02);
        controller.WriteRom(0x0000, 0x00);
        controller.WriteRom(0x0000, 0x0A);
        controller.WriteRamOffset(0x0001, 0x04);
        controller.SaveData.ClearBatterySaveDirty();

        controller.RestoreState(restoreState);

        controller.ReadRom(0x4000).Should().Be(0x33);
        controller.ReadRamOffset(0x0201).Should().Be(0xFB);
        controller.SaveData.IsBatterySaveDirty.Should().BeTrue();

        controller.WriteRamOffset(0x0001, 0xAC);

        controller.ReadRamOffset(0x0201).Should().Be(0xFC);
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

        controller.ReadRom(0x4000).Should().Be(0x22);
        controller.ReadRamOffset(0x0001).Should().Be(0xFF);

        controller.WriteRamOffset(0x0001, 0x0D);
        controller.WriteRom(0x0000, 0x0A);

        controller.ReadRamOffset(0x0001).Should().Be(0xFB);
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

        controller.SaveData.IsBatterySaveDirty.Should().BeFalse();
        for (ushort offset = 0; offset < 512; offset++)
        {
            controller.ReadRamOffset(offset).Should().Be((byte)(0xF0 | (offset & 0x0F)));
        }

        var dirtyState = new Mbc2MemoryControllerState(
            new Mbc2RamState(((Mbc2MemoryControllerState)state).Ram.Bytes, true),
            1,
            true
        );

        FluentActions
            .Invoking(() => controller.RestoreState(dirtyState))
            .Should()
            .ThrowExactly<ArgumentException>();
        controller.SaveData.IsBatterySaveDirty.Should().BeFalse();
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

        FluentActions
            .Invoking(() => controller.RestoreState(invalidState))
            .Should()
            .ThrowExactly<ArgumentException>();

        controller.ReadRom(0x4000).Should().Be(0x22);
        controller.ReadRamOffset(0x0001).Should().Be(0xF7);
        controller.SaveData.IsBatterySaveDirty.Should().BeTrue();
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
