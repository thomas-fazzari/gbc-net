// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cartridges;
using JetBrains.Annotations;

namespace GbcNet.App.Entities;

internal sealed class LibraryRom
{
    [UsedImplicitly]
    private LibraryRom() { }

    public string RomHash { get; private init; } = string.Empty;

    public string LastKnownPath { get; private set; } = string.Empty;

    public string FileName { get; private set; } = string.Empty;

    public string? CartridgeTitle { get; private set; }

    public CartridgeHardwareKind HardwareKind { get; private set; }

    public string? NoIntroHash { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset LastOpenedAt { get; private set; }

    public int LaunchCount { get; private set; }

    public long PlayTimeTicks { get; private set; }

    public string? CoverPath { get; private set; }

    public static LibraryRom Create(
        string romHash,
        string lastKnownPath,
        string fileName,
        string? cartridgeTitle,
        CartridgeHardwareKind hardwareKind,
        string noIntroHash,
        DateTimeOffset openedAt
    )
    {
        return new LibraryRom
        {
            RomHash = romHash,
            LastKnownPath = lastKnownPath,
            FileName = fileName,
            CartridgeTitle = cartridgeTitle,
            HardwareKind = hardwareKind,
            NoIntroHash = noIntroHash,
            LastOpenedAt = openedAt,
            LaunchCount = 1,
        };
    }

    public void RecordOpen(
        string lastKnownPath,
        string fileName,
        string? cartridgeTitle,
        CartridgeHardwareKind hardwareKind,
        string noIntroHash,
        DateTimeOffset openedAt
    )
    {
        LastKnownPath = lastKnownPath;
        FileName = fileName;
        CartridgeTitle = cartridgeTitle;
        HardwareKind = hardwareKind;
        NoIntroHash = noIntroHash;
        LastOpenedAt = openedAt;
        LaunchCount++;
    }

    public void SetCoverPath(string? coverPath) => CoverPath = coverPath;

    public void AddPlayTime(TimeSpan duration) => PlayTimeTicks += duration.Ticks;

    public void StampCreated(DateTimeOffset timestamp) => AddedAt = timestamp;

    public void StampUpdated(DateTimeOffset timestamp) => UpdatedAt = timestamp;
}
