// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cheats;
using JetBrains.Annotations;

namespace GbcNet.App.Database.Entities;

internal sealed class StoredCheatCode
{
    [UsedImplicitly]
    private StoredCheatCode() { }

    public static StoredCheatCode Create(
        string romHash,
        CheatCodeType type,
        int sortOrder,
        string code,
        bool isEnabled,
        string? name = null
    )
    {
        return new StoredCheatCode
        {
            RomHash = romHash,
            Type = type,
            SortOrder = sortOrder,
            Code = code,
            IsEnabled = isEnabled,
            Name = name,
        };
    }

    public string RomHash { get; private init; } = string.Empty;

    public CheatCodeType Type { get; private init; }

    public int SortOrder { get; private init; }

    public string Code { get; private init; } = string.Empty;

    public string? Name { get; private init; }

    public bool IsEnabled { get; private init; }
}
