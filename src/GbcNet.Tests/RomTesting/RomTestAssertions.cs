namespace GbcNet.Tests.RomTesting;

internal static class RomTestAssertions
{
    public static void AssertPassed(RomTestResult result)
    {
        Assert.True(result.Status is RomTestStatus.Passed, result.ToFailureMessage());
    }

    public static void AssertPassed(
        IReadOnlyDictionary<string, RomTestResult> results,
        string relativePath
    )
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(relativePath);

        if (!results.TryGetValue(relativePath, out var result))
        {
            Assert.Fail($"Missing ROM result: {relativePath}");
        }

        Assert.True(
            result.Status is RomTestStatus.Passed,
            relativePath + Environment.NewLine + result.ToFailureMessage()
        );
    }
}
