// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;

namespace GbcNet.App.Utils;

internal static class EnumParser
{
    public static bool TryParseDefinedName<TEnum>(string? name, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;

        return !string.IsNullOrWhiteSpace(name)
            && !IsIntegerText(name)
            && Enum.TryParse(name, ignoreCase: true, out value)
            && Enum.IsDefined(value);
    }

    public static bool TryParseCanonicalName<TEnum>(string? name, out TEnum value)
        where TEnum : struct, Enum =>
        TryParseDefinedName(name, out value)
        && string.Equals(name, Enum.GetName(value), StringComparison.Ordinal);

    private static bool IsIntegerText(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
}
