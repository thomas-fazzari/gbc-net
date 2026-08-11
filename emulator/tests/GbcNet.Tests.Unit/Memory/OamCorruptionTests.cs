// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Hardware;
using GbcNet.Core.Hardware.Profiles;
using GbcNet.Core.Memory;
using GbcNet.Core.Sm83;

namespace GbcNet.Tests.Unit.Memory;

public sealed class OamCorruptionTests
{
    private const byte LcdEnable = 0x80;
    private const int OamRowBytes = 8;

    [Theory]
    [InlineData(HardwareModel.Dmg)]
    [InlineData(HardwareModel.Sgb)]
    public void WriteByte_CorruptsCurrentOamScanRowOnMonochromeHardware(HardwareModel model)
    {
        // Pan Docs `oam-corruption-bug.md` and `memory-map.md` give DMG and SGB the same OAM bus behavior
        var bus = CreateBus(model);
        SeedWriteFormulaRowsAndEnterScan(bus);

        bus.WriteByte(AddressMap.NotUsableStart, 0xFF);

        ReadRow(bus, 5).Should().Equal(0x1717, 0x2222, 0x3333, 0x4444);
    }

    [Fact]
    public void ReadByte_AppliesReadCorruptionFormula()
    {
        // Pan Docs `oam-corruption-bug.md` defines the distinct read formula
        var bus = CreateBus(DmgHardwareProfile.Instance);
        SeedWriteFormulaRowsAndEnterScan(bus);

        bus.ReadByte(AddressMap.ObjectAttributeMemoryEnd).Should().Be(0xFF);

        ReadRow(bus, 5).Should().Equal(0x1F1F, 0x2222, 0x3333, 0x4444);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadByte_DoesNotCorruptOamOnCgbHardware(bool compatibilityMode)
    {
        // Pan Docs `oam-corruption-bug.md` excludes CGB hardware even in monochrome mode
        var bus = CreateBus(
            new CgbHardwareProfile(
                compatibilityMode ? CgbOperatingMode.DmgCompatibility : CgbOperatingMode.Cgb
            )
        );
        SeedWriteFormulaRowsAndEnterScan(bus);

        bus.ReadByte(AddressMap.ObjectAttributeMemoryStart);

        ReadRow(bus, 5).Should().Equal(0x5555, 0x6666, 0x7777, 0x8888);
    }

    [Fact]
    public void WriteByte_CorruptsAfterFirstOamRowOnly()
    {
        // Pan Docs `oam-corruption-bug.md` excludes the first OAM row
        var bus = CreateBus(DmgHardwareProfile.Instance);
        WriteRow(bus, 0, 0x1111, 0x2222, 0x3333, 0x4444);
        WriteRow(bus, 1, 0xAAAA, 0xBBBB, 0xCCCC, 0xDDDD);
        bus.WriteByte(AddressMap.LcdControlRegister, LcdEnable);
        bus.Ppu.Tick(452);

        bus.WriteByte(AddressMap.NotUsableStart, 0xFF);
        ReadRow(bus, 0).Should().Equal(0x1111, 0x2222, 0x3333, 0x4444);

        bus.Ppu.Tick(4);
        bus.WriteByte(AddressMap.NotUsableStart, 0xFF);
        ReadRow(bus, 1).Should().Equal(0x3333, 0x2222, 0x3333, 0x4444);
    }

    [Fact]
    public void ReadByte_DoesNotCorruptAfterOamScan()
    {
        // Pan Docs `oam-corruption-bug.md` limits corruption to PPU mode 2
        var bus = CreateBus(DmgHardwareProfile.Instance);
        SeedWriteFormulaRowsAndEnterScan(bus);
        bus.Ppu.Tick(60);

        bus.ReadByte(AddressMap.ObjectAttributeMemoryStart);

        ReadRow(bus, 5).Should().Equal(0x5555, 0x6666, 0x7777, 0x8888);
    }

    [Fact]
    public void IncrementRegisterPair_CorruptsOamFromIncrementDecrementAddress()
    {
        // Pan Docs `oam-corruption-bug.md` models standalone 16-bit IDU activity as a write
        var (cpu, bus) = CreateCpu(0x03);
        SeedWriteFormulaRowsAndEnterScan(bus);
        cpu.Registers.BC = AddressMap.NotUsableStart;

        cpu.Step().Should().Be(2);

        cpu.Registers.BC.Should().Be(AddressMap.NotUsableStart + 1);
        ReadRow(bus, 5).Should().Equal(0x1717, 0x2222, 0x3333, 0x4444);
    }

    [Fact]
    public void LoadFromHlIncrement_AppliesCombinedReadIncrementCorruption()
    {
        // Pan Docs `oam-corruption-bug.md` defines the combined read and IDU row copies
        var (cpu, bus) = CreateCpu(0x2A);
        WriteRow(bus, 3, 0x0F0F, 0xAAAA, 0xBBBB, 0xCCCC);
        WriteRow(bus, 4, 0x5555, 0x1111, 0x0F0F, 0x2222);
        WriteRow(bus, 5, 0xAAAA, 0x3333, 0x4444, 0x5555);
        EnterOamScanRow(bus, 5);
        cpu.Registers.HL = AddressMap.NotUsableStart;

        cpu.Step().Should().Be(2);

        cpu.Registers.HL.Should().Be(AddressMap.NotUsableStart + 1);
        ReadRow(bus, 3).Should().Equal(0x0F0F, 0x1111, 0x0F0F, 0x2222);
        ReadRow(bus, 4).Should().Equal(0x0F0F, 0x1111, 0x0F0F, 0x2222);
        ReadRow(bus, 5).Should().Equal(0x0F0F, 0x1111, 0x0F0F, 0x2222);
    }

    [Fact]
    public void Push_CorruptsOamWhenPreDecrementStackPointerIsOnOamBus()
    {
        // Pan Docs `oam-corruption-bug.md` applies stack corruption to SP before decrement
        var (cpu, bus) = CreateCpu(0xC5);
        SeedWriteFormulaRowsAndEnterScan(bus);
        cpu.Registers.SP = AddressMap.ObjectAttributeMemoryStart;

        cpu.Step().Should().Be(4);

        cpu.Registers.SP.Should().Be(AddressMap.ObjectAttributeMemoryStart - 2);
        ReadRow(bus, 5).Should().Equal(0x1717, 0x2222, 0x3333, 0x4444);
    }

    private static MemoryBus CreateBus(IHardwareProfile profile) =>
        new(TestRomFactory.LoadCartridge(), profile);

    private static MemoryBus CreateBus(HardwareModel model)
    {
        var cartridge = TestRomFactory.LoadCartridge();
        return new MemoryBus(cartridge, HardwareProfileFactory.Create(model, cartridge.Header));
    }

    private static (Cpu Cpu, MemoryBus Bus) CreateCpu(byte opcode)
    {
        var bus = new MemoryBus(
            TestRomFactory.LoadCartridge(bytes =>
                bytes[AddressMap.CartridgeEntryPointAddress] = opcode
            ),
            DmgHardwareProfile.Instance
        );
        return (new Cpu(bus), bus);
    }

    private static void EnterOamScanRow(MemoryBus bus, int row)
    {
        bus.WriteByte(AddressMap.LcdControlRegister, LcdEnable);
        bus.Ppu.Tick(452 + (row * 4));
    }

    private static void SeedWriteFormulaRowsAndEnterScan(MemoryBus bus)
    {
        WriteRow(bus, 4, 0x0F0F, 0x2222, 0x3333, 0x4444);
        WriteRow(bus, 5, 0x5555, 0x6666, 0x7777, 0x8888);
        EnterOamScanRow(bus, 5);
    }

    private static void WriteRow(
        MemoryBus bus,
        int row,
        ushort first,
        ushort second,
        ushort third,
        ushort fourth
    )
    {
        WriteWord(bus, row, 0, first);
        WriteWord(bus, row, 1, second);
        WriteWord(bus, row, 2, third);
        WriteWord(bus, row, 3, fourth);
    }

    private static ushort[] ReadRow(MemoryBus bus, int row) =>
        [
            ReadWord(bus, row, 0),
            ReadWord(bus, row, 1),
            ReadWord(bus, row, 2),
            ReadWord(bus, row, 3),
        ];

    private static ushort ReadWord(MemoryBus bus, int row, int word)
    {
        var address = GetWordAddress(row, word);
        return (ushort)(
            bus.Ppu.ObjectAttributeMemory.Read(address)
            | (bus.Ppu.ObjectAttributeMemory.Read((ushort)(address + 1)) << 8)
        );
    }

    private static void WriteWord(MemoryBus bus, int row, int word, ushort value)
    {
        var address = GetWordAddress(row, word);
        bus.Ppu.ObjectAttributeMemory.Write(address, (byte)value);
        bus.Ppu.ObjectAttributeMemory.Write((ushort)(address + 1), (byte)(value >> 8));
    }

    private static ushort GetWordAddress(int row, int word) =>
        (ushort)(AddressMap.ObjectAttributeMemoryStart + (row * OamRowBytes) + (word * 2));
}
