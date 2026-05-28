using GbcNet.Core;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Joypad;
using GbcNet.Core.Memory;
using GbcNet.Tests.Cartridges;

namespace GbcNet.Tests;

public sealed class GameBoyTests
{
    [Fact]
    public void Step_ReturnsCpuMachineCyclesAndTicksTimer()
    {
        Cartridge cartridge = ResultAssertions.AssertSuccess(
            Cartridge.Load(TestRomFactory.Create())
        );
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        gameBoy.Bus.WriteByte(AddressMap.TimerControlRegister, 0b0000_0101);

        int machineCycles = gameBoy.Step();
        gameBoy.Step();
        gameBoy.Step();
        gameBoy.Step();

        Assert.Equal(1, machineCycles);
        Assert.Equal(0x01, gameBoy.Bus.ReadByte(AddressMap.TimerCounterRegister));
    }

    [Fact]
    public void Step_TicksDmaAfterCpuStep()
    {
        Cartridge cartridge = ResultAssertions.AssertSuccess(
            Cartridge.Load(TestRomFactory.Create())
        );
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        gameBoy.Bus.WriteByte(AddressMap.WorkRamStart, 0x42);

        gameBoy.Bus.WriteByte(AddressMap.DmaRegister, 0xC0);
        gameBoy.Step();

        Assert.Equal(0x00, gameBoy.Bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));

        gameBoy.Step();

        Assert.Equal(0x42, gameBoy.Bus.ReadByte(AddressMap.ObjectAttributeMemoryStart));
    }

    [Fact]
    public void Constructor_AppliesDmgPostBootState()
    {
        Cartridge cartridge = ResultAssertions.AssertSuccess(
            Cartridge.Load(TestRomFactory.Create())
        );

        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);

        Assert.Equal(HardwareModel.Dmg, gameBoy.HardwareModel);
        Assert.Equal(0xAB, gameBoy.Bus.ReadByte(AddressMap.DividerRegister));
        Assert.Equal(0xE1, gameBoy.Bus.ReadByte(AddressMap.InterruptFlagRegister));
    }

    [Fact]
    public void SetButtonState_UpdatesJoypadInputState()
    {
        Cartridge cartridge = ResultAssertions.AssertSuccess(
            Cartridge.Load(TestRomFactory.Create())
        );
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        gameBoy.Bus.WriteByte(AddressMap.JoypadRegister, 0x10);

        gameBoy.SetButtonState(JoypadButton.A, pressed: true);

        Assert.Equal(0xDE, gameBoy.Bus.ReadByte(AddressMap.JoypadRegister));
    }
}
