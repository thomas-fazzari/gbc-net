using FluentResults;

namespace GbcNet.Tests;

internal static class ResultAssertions
{
    public static TValue AssertSuccess<TValue>(Result<TValue> result)
    {
        Assert.True(
            result.IsSuccess,
            string.Join(Environment.NewLine, result.Errors.Select(static error => error.Message))
        );
        return result.Value;
    }
}
