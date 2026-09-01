// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.App.Library;

internal enum SortDirection
{
    Ascending = 0,
    Descending = 1,
}

internal static class SortDirectionExtensions
{
    extension(SortDirection direction)
    {
        internal bool IsAscending() =>
            direction switch
            {
                SortDirection.Ascending => true,
                SortDirection.Descending => false,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(direction),
                    direction,
                    message: null
                ),
            };
    }
}
