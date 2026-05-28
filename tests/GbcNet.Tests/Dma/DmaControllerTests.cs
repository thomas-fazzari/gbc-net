using GbcNet.Core.Dma;

namespace GbcNet.Tests.Dma;

public sealed class DmaControllerTests
{
    [Fact]
    public void StartOamTransfer_StoresSourceHighByte()
    {
        var dma = new DmaController();

        dma.StartOamTransfer(0xC0);

        Assert.Equal(0xC0, dma.ReadRegister());
    }

    [Fact]
    public void SetRegisterState_SeedsRegisterWithoutStartingTransfer()
    {
        var dma = new DmaController();

        dma.SetRegisterState(0xFF);

        Assert.Equal(0xFF, dma.ReadRegister());
    }
}
