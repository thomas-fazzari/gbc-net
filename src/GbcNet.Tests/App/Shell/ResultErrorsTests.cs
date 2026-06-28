using FluentResults;
using GbcNet.App.Shell;

namespace GbcNet.Tests.App.Shell;

public sealed class ResultErrorsTests
{
    [Fact]
    public void Format_JoinsErrorMessages()
    {
        var formatted = ResultErrors.Format([new Error("first"), new Error("second")]);

        Assert.Equal($"first{Environment.NewLine}second", formatted);
    }
}
