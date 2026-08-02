// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cheats;
using JetBrains.Annotations;

namespace GbcNet.App.Database.Entities;

internal sealed class StoredCheatCode
{
    [UsedImplicitly]
    private StoredCheatCode() { }

    internal StoredCheatCode(
        string romHash,
        CheatCodeType type,
        int sortOrder,
        string code,
        bool isEnabled,
        string? name = null
    )
    {
        RomHash = romHash;
        Type = type;
        SortOrder = sortOrder;
        Code = code;
        IsEnabled = isEnabled;
        Name = name;
    }

    public string RomHash { get; } = string.Empty;

    public CheatCodeType Type { get; }

    public int SortOrder { get; }

    public string Code { get; } = string.Empty;

    public string? Name { get; }

    public bool IsEnabled { get; }
}
