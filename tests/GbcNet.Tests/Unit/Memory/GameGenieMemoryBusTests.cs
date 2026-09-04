// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Cheats;
using GbcNet.Core.Clock;
using GbcNet.Core.Hardware;
using GbcNet.Core.Hardware.Profiles;
using GbcNet.Core.Memory;

namespace GbcNet.Tests.Unit.Memory;

public sealed class GameGenieMemoryBusTests
{
    [Fact]
    public void ReadByte_AppliesUnconditionalReplacementWithoutMutatingCartridge()
    {
        var cartridge = TestRomFactory.LoadCartridge(bytes => bytes[0x01B9] = 0x44);
        var bus = new MemoryBus(cartridge, DmgHardwareProfile.Instance);

        bus.SetCheatCodes([CheatCodeParser.Parse(CheatCodeType.GameGenie, "0A1-B9F")]);

        bus.ReadByte(0x01B9).Should().Be(0x0A);
        cartridge.ReadRom(0x01B9).Should().Be(0x44);

        bus.SetCheatCodes([CheatCodeParser.Parse(CheatCodeType.GameGenie, "0C1-B9F")]);

        bus.ReadByte(0x01B9).Should().Be(0x0C);
        cartridge.ReadRom(0x01B9).Should().Be(0x44);

        bus.SetCheatCodes([]);

        bus.ReadByte(0x01B9).Should().Be(0x44);
        cartridge.ReadRom(0x01B9).Should().Be(0x44);
    }

    [Fact]
    public void ReadByte_RequiresCompareValueToMatchOriginalCartridgeByte()
    {
        var rom = TestRomFactory.Create(bytes => bytes[0x0855] = 0x04);
        var bus = new MemoryBus(TestRomFactory.LoadCartridge(rom), DmgHardwareProfile.Instance);

        bus.SetCheatCodes([CheatCodeParser.Parse(CheatCodeType.GameGenie, "068-55F-E66")]);

        bus.ReadByte(0x0855).Should().Be(0x04);

        rom[0x0855] = 0x03;
        bus = new MemoryBus(TestRomFactory.LoadCartridge(rom), DmgHardwareProfile.Instance);
        bus.SetCheatCodes([CheatCodeParser.Parse(CheatCodeType.GameGenie, "068-55F-E66")]);

        bus.ReadByte(0x0855).Should().Be(0x06);
    }

    [Fact]
    public void ReadByte_UsesFirstMatchingCodeAtSameAddress()
    {
        var bus = new MemoryBus(
            TestRomFactory.LoadCartridge(bytes => bytes[0x4000] = 0x44),
            DmgHardwareProfile.Instance
        );

        bus.SetCheatCodes([
            CheatCodeParser.Parse(CheatCodeType.GameGenie, "110-00B"),
            CheatCodeParser.Parse(CheatCodeType.GameGenie, "220-00B"),
        ]);

        bus.ReadByte(0x4000).Should().Be(0x11);
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

        bus.SetCheatCodes([CheatCodeParser.Parse(CheatCodeType.GameGenie, "CC0-00B-602")]);

        bus.ReadByte(0x4000).Should().Be(0x11);

        bus.WriteByte(0x2000, 0x02);

        bus.ReadByte(0x4000).Should().Be(0xCC);
        cartridge.ReadRom(0x4000).Should().Be(0x22);
    }

    [Fact]
    public void SetCheatCodes_RejectsInvalidSnapshotWithoutReplacingCurrentCodes()
    {
        var bus = new MemoryBus(
            TestRomFactory.LoadCartridge(bytes => bytes[0x01B9] = 0x44),
            DmgHardwareProfile.Instance
        );
        bus.SetCheatCodes([CheatCodeParser.Parse(CheatCodeType.GameGenie, "0A1-B9F")]);

        var exception = FluentActions
            .Invoking(() => bus.SetCheatCodes([default]))
            .Should()
            .ThrowExactly<ArgumentException>()
            .Which;

        exception.ParamName.Should().Be("codes");
        bus.ReadByte(0x01B9).Should().Be(0x0A);
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
        bus.SetCheatCodes([CheatCodeParser.Parse(CheatCodeType.GameGenie, "AA1-00F")]);

        bus.ReadByte(0x0100).Should().Be(0x55);

        bus.WriteByte(AddressMap.BootRomDisableRegister, 0x01);

        bus.ReadByte(0x0100).Should().Be(0xAA);
    }

    [Fact]
    public void Dma_CopiesGameGenieReplacementFromRom()
    {
        var bus = new MemoryBus(
            TestRomFactory.LoadCartridge(bytes => bytes[0x0855] = 0x44),
            new CgbHardwareProfile(CgbOperatingMode.Cgb)
        );
        bus.SetCheatCodes([CheatCodeParser.Parse(CheatCodeType.GameGenie, "068-55F")]);
        var clock = new MachineClock(bus);

        bus.WriteByte(AddressMap.DmaRegister, 0x08);
        bus.TickDma(2);
        bus.TickDma(160);
        bus.Ppu.ObjectAttributeMemory.Read(AddressMap.ObjectAttributeMemoryStart + 0x55)
            .Should()
            .Be(0x06);

        bus.WriteByte(AddressMap.VideoRamDmaSourceHighRegister, 0x08);
        bus.WriteByte(AddressMap.VideoRamDmaSourceLowRegister, 0x50);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationHighRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaDestinationLowRegister, 0x00);
        bus.WriteByte(AddressMap.VideoRamDmaLengthModeStartRegister, 0x00);

        bus.Ppu.ObjectAttributeMemory.Read(AddressMap.ObjectAttributeMemoryStart + 0x55)
            .Should()
            .Be(0x06);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart + 0x05).Should().Be(0x00);

        for (var machineCycle = 0; machineCycle < 9; machineCycle++)
        {
            clock.TickMachineCycle();
        }

        bus.Ppu.ObjectAttributeMemory.Read(AddressMap.ObjectAttributeMemoryStart + 0x55)
            .Should()
            .Be(0x06);
        bus.Ppu.VideoRam.Read(AddressMap.VideoRamStart + 0x05).Should().Be(0x06);
    }
}
