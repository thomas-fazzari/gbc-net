// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using FluentResults;

namespace GbcNet.Tests;

internal static class ResultAssertions
{
    public static void AssertSuccess(Result result)
    {
        Assert.True(
            result.IsSuccess,
            string.Join(Environment.NewLine, result.Errors.Select(static error => error.Message))
        );
    }

    public static TValue AssertSuccess<TValue>(Result<TValue> result)
    {
        Assert.True(
            result.IsSuccess,
            string.Join(Environment.NewLine, result.Errors.Select(static error => error.Message))
        );
        return result.Value;
    }
}
