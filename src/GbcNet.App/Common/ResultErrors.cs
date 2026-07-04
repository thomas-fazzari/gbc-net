// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using FluentResults;

namespace GbcNet.App.Common;

internal static class ResultErrors
{
    public static string Format(IEnumerable<IError> errors) =>
        string.Join(Environment.NewLine, errors.Select(static error => error.Message));
}
