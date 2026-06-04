using GbcNet.Core.Apu;

namespace GbcNet.Tests.Apu;

public sealed class ApuControllerTests
{
    [Theory]
    [InlineData(0xFF10, 0x80, 0x00, 0x80)]
    [InlineData(0xFF10, 0x80, 0x80, 0x80)]
    [InlineData(0xFF1A, 0x7F, 0x00, 0x7F)]
    [InlineData(0xFF1A, 0x7F, 0x7F, 0x7F)]
    [InlineData(0xFF1C, 0x9F, 0x00, 0x9F)]
    [InlineData(0xFF1C, 0x9F, 0x9F, 0x9F)]
    [InlineData(0xFF20, 0xC0, 0x00, 0xC0)]
    [InlineData(0xFF20, 0xC0, 0xC0, 0xC0)]
    [InlineData(0xFF23, 0x3F, 0x00, 0x3F)]
    [InlineData(0xFF23, 0x3F, 0x3F, 0x3F)]
    [InlineData(0xFF26, 0x70, 0x80, 0x70)]
    [InlineData(0xFF26, 0x70, 0xF0, 0x70)]
    public void ReadRegister_ForcesUnusedBitsHigh(
        ushort address,
        byte mask,
        byte writeValue,
        byte expected
    )
    {
        var apu = new ApuController();

        apu.WriteRegister(address, writeValue);

        Assert.Equal(expected, (byte)(apu.ReadRegister(address) & mask));
    }

    [Fact]
    public void SetRegisterState_CanSeedAudioMasterStatusBits()
    {
        var apu = new ApuController();

        apu.SetRegisterState(0xFF26, 0x81);

        Assert.Equal(0xF1, apu.ReadRegister(0xFF26));
    }

    [Fact]
    public void WriteRegister_CannotSetAudioMasterStatusBits()
    {
        var apu = new ApuController();

        apu.WriteRegister(0xFF26, 0x81);

        Assert.Equal(0xF0, apu.ReadRegister(0xFF26));
    }

    [Theory]
    [InlineData(0xFF10)]
    [InlineData(0xFF14)]
    [InlineData(0xFF1E)]
    [InlineData(0xFF26)]
    public void ContainsRegister_ReturnsTrueForApuRegisters(ushort address)
    {
        Assert.True(ApuController.ContainsRegister(address));
    }

    [Theory]
    [InlineData(0xFF15)]
    [InlineData(0xFF1F)]
    public void ContainsRegister_ReturnsFalseForUnusedApuAddresses(ushort address)
    {
        Assert.False(ApuController.ContainsRegister(address));
    }
}
