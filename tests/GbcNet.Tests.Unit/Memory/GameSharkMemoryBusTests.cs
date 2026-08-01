// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core;
using GbcNet.Core.Cheats;
using GbcNet.Core.Clock;
using GbcNet.Core.Hardware;
using GbcNet.Core.Hardware.Profiles;
using GbcNet.Core.Interrupts;
using GbcNet.Core.Memory;
using GbcNet.Core.Ppu;

namespace GbcNet.Tests.Unit.Memory;

public sealed class GameSharkMemoryBusTests
{
    [Fact]
    public void VBlankWrite_WaitsForVBlankAndUsesLastCodeAtSameAddress()
    {
        var bus = CreateDmgBus();
        var clock = new MachineClock(bus);
        bus.WriteByte(AddressMap.WorkRamStart, 0x44);
        bus.SetCheatCodes([Parse("01AA00C0"), Parse("01BB00C0")]);

        AdvanceToFirstVBlank(bus, clock, AddressMap.WorkRamStart, expectedValue: 0x44);

        Assert.Equal(0xBB, bus.ReadByte(AddressMap.WorkRamStart));
    }

    [Fact]
    public void VBlankWrite_AppliesWhenVideoRenderingIsDisabled()
    {
        var bus = CreateDmgBus();
        var clock = new MachineClock(bus);
        bus.Ppu.VideoRenderingEnabled = false;
        bus.SetCheatCodes([Parse("01AA00C0")]);

        AdvanceToFirstVBlank(bus, clock, AddressMap.WorkRamStart, expectedValue: 0x00);

        Assert.Equal(0xAA, bus.ReadByte(AddressMap.WorkRamStart));
    }

    [Fact]
    public void VBlankWrite_RemainsGatedWhileBootRomIsMapped()
    {
        var bootRom = BootRom.Create(
            HardwareModel.Cgb,
            new BootRomOptions { CgbBootRom = BootRomTestFactory.CreateCgb() }
        );
        var bus = new MemoryBus(
            TestRomFactory.LoadCartridge(),
            new CgbHardwareProfile(CgbOperatingMode.Cgb),
            bootRom
        );
        var clock = new MachineClock(bus);
        bus.WriteByte(AddressMap.LcdControlRegister, 0x80);
        bus.SetCheatCodes([Parse("01AA00C0")]);

        AdvanceToFirstVBlank(bus, clock, AddressMap.WorkRamStart, expectedValue: 0x00);

        Assert.Equal(0x00, bus.ReadByte(AddressMap.WorkRamStart));
    }

    [Fact]
    public void SetCheatCodes_ClearingGameSharkCodesDoesNotRestoreWrittenMemory()
    {
        var bus = CreateDmgBus();
        bus.SetCheatCodes([Parse("01AA00C0")]);

        bus.ApplyGameSharkCodes();
        bus.SetCheatCodes([]);

        Assert.Equal(0xAA, bus.ReadByte(AddressMap.WorkRamStart));
    }

    private static MemoryBus CreateDmgBus()
    {
        var bus = new MemoryBus(TestRomFactory.LoadCartridge(), DmgHardwareProfile.Instance);
        bus.WriteByte(AddressMap.LcdControlRegister, 0x80);
        return bus;
    }

    private static void AdvanceToFirstVBlank(
        MemoryBus bus,
        MachineClock clock,
        ushort observedAddress,
        byte expectedValue
    )
    {
        const byte vBlankMask = (byte)(1 << (int)InterruptSource.VBlank);
        const int maximumMachineCycles =
            (PpuGeometry.ScanlineDots * (PpuGeometry.LastScanline + 1))
            / HardwareTiming.MachineCycleTCycles;

        for (var machineCycle = 0; machineCycle < maximumMachineCycles; machineCycle++)
        {
            if ((bus.Interrupts.InterruptFlag & vBlankMask) != 0)
            {
                return;
            }

            Assert.Equal(expectedValue, bus.ReadByte(observedAddress));
            clock.TickMachineCycle();
        }

        Assert.Fail("The PPU did not enter VBlank within one frame.");
    }

    private static CheatCode Parse(string text)
    {
        Assert.True(CheatCode.TryParse(CheatCodeType.GameShark, text, out var code));
        return code;
    }
}
