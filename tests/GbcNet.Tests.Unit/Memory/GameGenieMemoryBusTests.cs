// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Cheats;
using GbcNet.Core.Hardware;
using GbcNet.Core.Hardware.Profiles;
using GbcNet.Core.Memory;
using GbcNet.Tests.Shared;

namespace GbcNet.Tests.Unit.Memory;

public sealed class GameGenieMemoryBusTests
{
    [Fact]
    public void ReadByte_AppliesUnconditionalReplacementWithoutMutatingCartridge()
    {
        var cartridge = TestRomFactory.LoadCartridge(bytes => bytes[0x01B9] = 0x44);
        var bus = new MemoryBus(cartridge, DmgHardwareProfile.Instance);

        bus.SetCheatCodes([Parse("0A1-B9F")]);

        Assert.Equal(0x0A, bus.ReadByte(0x01B9));
        Assert.Equal(0x44, cartridge.ReadRom(0x01B9));

        bus.SetCheatCodes([Parse("0C1-B9F")]);

        Assert.Equal(0x0C, bus.ReadByte(0x01B9));
        Assert.Equal(0x44, cartridge.ReadRom(0x01B9));

        bus.SetCheatCodes([]);

        Assert.Equal(0x44, bus.ReadByte(0x01B9));
        Assert.Equal(0x44, cartridge.ReadRom(0x01B9));
    }

    [Fact]
    public void ReadByte_RequiresCompareValueToMatchOriginalCartridgeByte()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0855] = 0x04);
        var bus = new MemoryBus(TestRomFactory.LoadCartridge(rom), DmgHardwareProfile.Instance);

        bus.SetCheatCodes([Parse("068-55F-E66")]);

        Assert.Equal(0x04, bus.ReadByte(0x0855));

        rom[0x0855] = 0x03;
        bus = new MemoryBus(TestRomFactory.LoadCartridge(rom), DmgHardwareProfile.Instance);
        bus.SetCheatCodes([Parse("068-55F-E66")]);

        Assert.Equal(0x06, bus.ReadByte(0x0855));
    }

    [Fact]
    public void ReadByte_UsesFirstMatchingCodeAtSameAddress()
    {
        var bus = new MemoryBus(
            TestRomFactory.LoadCartridge(bytes => bytes[0x4000] = 0x44),
            DmgHardwareProfile.Instance
        );

        bus.SetCheatCodes([Parse("110-00B"), Parse("220-00B")]);

        Assert.Equal(0x11, bus.ReadByte(0x4000));
    }

    [Fact]
    public void ReadByte_AppliesCodesToCurrentlyVisibleMbcBank()
    {
        var cartridge = TestRomFactory.LoadCartridge(
            TestRomFactory.Create(
                romSizeCode: 0x01,
                bytes =>
                {
                    bytes[0x0147] = (byte)CartridgeType.Mbc1;
                    bytes[Cartridge.FixedRomBankSize] = 0x11;
                    bytes[2 * Cartridge.FixedRomBankSize] = 0x22;
                }
            )
        );
        var bus = new MemoryBus(cartridge, DmgHardwareProfile.Instance);

        bus.SetCheatCodes([Parse("CC0-00B-602")]);

        Assert.Equal(0x11, bus.ReadByte(0x4000));

        bus.WriteByte(0x2000, 0x02);

        Assert.Equal(0xCC, bus.ReadByte(0x4000));
        Assert.Equal(0x22, cartridge.ReadRom(0x4000));
    }

    [Fact]
    public void SetCheatCodes_RejectsInvalidSnapshotWithoutReplacingCurrentCodes()
    {
        var bus = new MemoryBus(
            TestRomFactory.LoadCartridge(bytes => bytes[0x01B9] = 0x44),
            DmgHardwareProfile.Instance
        );
        bus.SetCheatCodes([Parse("0A1-B9F")]);

        var exception = Assert.Throws<ArgumentException>(() => bus.SetCheatCodes([default]));

        Assert.Equal("codes", exception.ParamName);
        Assert.Equal(0x0A, bus.ReadByte(0x01B9));
    }

    [Fact]
    public void ReadByte_KeepsCheatsGatedUntilCgbBootRomIsDisabled()
    {
        var cartridge = TestRomFactory.LoadCartridge(bytes => bytes[0x0100] = 0x55);
        var bootRom = BootRom.Create(
            HardwareModel.Cgb,
            new BootRomOptions { CgbBootRom = BootRomTestFactory.CreateCgb() }
        );
        var bus = new MemoryBus(cartridge, new CgbHardwareProfile(CgbOperatingMode.Cgb), bootRom);
        bus.SetCheatCodes([Parse("AA1-00F")]);

        Assert.Equal(0x55, bus.ReadByte(0x0100));

        bus.WriteByte(AddressMap.BootRomDisableRegister, 0x01);

        Assert.Equal(0xAA, bus.ReadByte(0x0100));
    }

    [Fact]
    public void Dma_CopiesGameGenieReplacementFromRom()
    {
        var bus = new MemoryBus(
            TestRomFactory.LoadCartridge(bytes => bytes[0x0855] = 0x44),
            new CgbHardwareProfile(CgbOperatingMode.Cgb)
        );
        bus.SetCheatCodes([Parse("068-55F")]);

        bus.WriteByte(AddressMap.DmaRegister, 0x08);
        bus.TickDma(2);
        bus.TickDma(160);

        bus.WriteByte(AddressMap.VideoRamDmaSourceHighRegister, 0x08);
        bus.WriteByte(AddressMap.VideoRamDmaSourceLowRegister, 0x50);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationHighRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationLowRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaLengthModeStartRegister, 0x00);

        Assert.Equal(
            0x06,
            bus.Ppu.ObjectAttributeMemory.Read(AddressMap.ObjectAttributeMemoryStart + 0x55)
        );
        Assert.Equal(0x06, bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart + 0x05));
    }

    private static CheatCode Parse(string text)
    {
        Assert.True(CheatCode.TryParse(CheatCodeType.GameGenie, text, out var code));
        return code;
    }
}
