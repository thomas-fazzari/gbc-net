using GbcNet.Core;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Joypad;
using GbcNet.Core.Memory;
using GbcNet.Tests.Cartridges;

namespace GbcNet.Tests;

public sealed class GameBoyTests
{
    private const byte HaltOpcode = 0x76;

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
    public void Step_TicksSerial()
    {
        Cartridge cartridge = ResultAssertions.AssertSuccess(
            Cartridge.Load(TestRomFactory.Create(bytes => bytes[0x0100] = HaltOpcode))
        );
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        byte? transferredByte = null;
        gameBoy.SerialByteTransferred += (_, e) => transferredByte = e.Value;
        gameBoy.Bus.WriteByte(AddressMap.SerialTransferDataRegister, 0x41);
        gameBoy.Bus.WriteByte(AddressMap.SerialTransferControlRegister, 0x81);

        for (int step = 0; step < 1024; step++)
        {
            gameBoy.Step();
        }

        Assert.Equal(0xFF, gameBoy.Bus.ReadByte(AddressMap.SerialTransferDataRegister));
        Assert.Equal(0x7F, gameBoy.Bus.ReadByte(AddressMap.SerialTransferControlRegister));
        Assert.Equal((byte)0x41, transferredByte);
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
