using GbcNet.Core;
using GbcNet.Core.Apu;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Hardware;
using GbcNet.Core.Joypad;
using GbcNet.Core.Memory;
using GbcNet.Core.Ppu;
using GbcNet.Tests.Cartridges;

namespace GbcNet.Tests;

public sealed class GameBoyTests
{
    private const byte HaltOpcode = 0x76;
    private const byte StopOpcode = 0x10;
    private const byte JumpImmediate16Opcode = 0xC3;

    [Fact]
    public void Step_ReturnsCpuMachineCyclesAndTicksTimer()
    {
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(TestRomFactory.Create()));
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        gameBoy.Bus.WriteByte(AddressMap.TimerControlRegister, 0b0000_0101);

        var machineCycles = gameBoy.Step();
        gameBoy.Step();
        gameBoy.Step();
        gameBoy.Step();

        Assert.Equal(1, machineCycles);
        Assert.Equal(0x01, gameBoy.Bus.ReadByte(AddressMap.TimerCounterRegister));
    }

    [Fact]
    public void Step_ReturnsZeroAfterCpuEntersStop()
    {
        var cartridge = ResultAssertions.AssertSuccess(
            Cartridge.Load(
                TestRomFactory.Create(bytes =>
                {
                    bytes[0x0100] = StopOpcode;
                    bytes[0x0101] = 0x00;
                })
            )
        );
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);

        Assert.Equal(2, gameBoy.Step());
        Assert.Equal(0, gameBoy.Step());
    }

    [Fact]
    public void Step_TicksSerial()
    {
        var cartridge = ResultAssertions.AssertSuccess(
            Cartridge.Load(TestRomFactory.Create(bytes => bytes[0x0100] = HaltOpcode))
        );
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        byte? transferredByte = null;
        gameBoy.SerialByteTransferred += (_, e) => transferredByte = e.Value;
        gameBoy.Bus.WriteByte(AddressMap.SerialTransferDataRegister, 0x41);
        gameBoy.Bus.WriteByte(AddressMap.SerialTransferControlRegister, 0x81);

        for (var step = 0; step < 1024; step++)
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
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(TestRomFactory.Create()));

        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);

        Assert.Equal(HardwareModel.Dmg, gameBoy.HardwareModel);
        Assert.Equal(0xAB, gameBoy.Bus.ReadByte(AddressMap.DividerRegister));
        Assert.Equal(0xE1, gameBoy.Bus.ReadByte(AddressMap.InterruptFlagRegister));
    }

    [Fact]
    public void DrainAudioSamples_ReturnsProducedSamples()
    {
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(TestRomFactory.Create()));
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        var destination = new ApuStereoSample[1];

        gameBoy.Bus.Apu.Tick(88);

        Assert.Equal(1, gameBoy.DrainAudioSamples(destination));
        Assert.Equal(default, destination[0]);
    }

    [Fact]
    public void DrainAudioSamples_PreservesSamplesThatDoNotFit()
    {
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(TestRomFactory.Create()));
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        var firstDrain = new ApuStereoSample[1];
        var secondDrain = new ApuStereoSample[2];

        gameBoy.Bus.Apu.Tick(264);

        Assert.Equal(1, gameBoy.DrainAudioSamples(firstDrain));
        Assert.Equal(2, gameBoy.DrainAudioSamples(secondDrain));
    }

    [Fact]
    public void DrainAudioSamples_ReturnsZeroWhenEmpty()
    {
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(TestRomFactory.Create()));
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        Span<ApuStereoSample> destination = stackalloc ApuStereoSample[1];

        Assert.Equal(0, gameBoy.DrainAudioSamples(destination));
    }

    [Fact]
    public void SetButtonState_UpdatesJoypadInputState()
    {
        var cartridge = ResultAssertions.AssertSuccess(Cartridge.Load(TestRomFactory.Create()));
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        gameBoy.Bus.WriteByte(AddressMap.JoypadRegister, 0x10);

        gameBoy.SetButtonState(JoypadButton.A, pressed: true);

        Assert.Equal(0xDE, gameBoy.Bus.ReadByte(AddressMap.JoypadRegister));
    }

    [Fact]
    public void Step_RaisesFrameCompletedAfterCpuInstruction()
    {
        var cartridge = ResultAssertions.AssertSuccess(
            Cartridge.Load(
                TestRomFactory.Create(bytes =>
                {
                    bytes[0x0100] = JumpImmediate16Opcode;
                    bytes[0x0101] = 0x00;
                    bytes[0x0102] = 0x01;
                })
            )
        );
        var gameBoy = new GameBoy(cartridge, HardwareModel.Dmg);
        var completedFrames = new List<LcdFrame>();
        gameBoy.FrameCompleted += (_, e) => completedFrames.Add(e.Frame);

        for (var step = 0; completedFrames.Count == 0 && step < 20_000; step++)
        {
            gameBoy.Step();
        }

        var completedFrame = Assert.Single(completedFrames);
        Assert.Equal(160, completedFrame.Width);
        Assert.Equal(144, completedFrame.Height);
        Assert.Equal(LcdPixelFormat.DmgShadeIndex8, completedFrame.PixelFormat);
    }
}
