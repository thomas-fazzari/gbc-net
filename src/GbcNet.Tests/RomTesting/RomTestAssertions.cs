namespace GbcNet.Tests.RomTesting;

internal static class RomTestAssertions
{
    public static void AssertPassed(RomTestResult result)
    {
        Assert.True(result.Status is RomTestStatus.Passed, result.ToFailureMessage());
    }
}
