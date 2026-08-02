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

        return !string.IsNullOrWhiteSpace(value: name)
            && !IsIntegerText(value: name)
            && Enum.TryParse(value: name, ignoreCase: true, result: out value)
            && Enum.IsDefined(value: value);
    }

    public static bool TryParseCanonicalName<TEnum>(string? name, out TEnum value)
        where TEnum : struct, Enum =>
        TryParseDefinedName(name: name, value: out value)
        && string.Equals(
            name,
            Enum.GetName(value: value),
            comparisonType: StringComparison.Ordinal
        );

    private static bool IsIntegerText(string value) =>
        int.TryParse(
            s: value,
            style: NumberStyles.Integer,
            provider: CultureInfo.InvariantCulture,
            result: out _
        );
}
