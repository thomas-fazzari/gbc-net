// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using FluentResults;
using GbcNet.App.Common;

namespace GbcNet.Tests.App.Common;

public sealed class ResultErrorsTests
{
    [Fact]
    public void Format_JoinsErrorMessages()
    {
        var formatted = ResultErrors.Format([new Error("first"), new Error("second")]);

        Assert.Equal($"first{Environment.NewLine}second", formatted);
    }
}
