// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cartridges;

namespace GbcNet.App.Database.Entities;

internal sealed class LibraryRom
{
    private LibraryRom() { }

    private LibraryRom(
        string romHash,
        string lastKnownPath,
        string fileName,
        string? cartridgeTitle,
        CartridgeHardwareKind hardwareKind,
        DateTimeOffset openedAt
    )
    {
        RomHash = romHash;
        LastKnownPath = lastKnownPath;
        FileName = fileName;
        CartridgeTitle = cartridgeTitle;
        HardwareKind = hardwareKind;
        LastOpenedAt = openedAt;
        LaunchCount = 1;
    }

    public string RomHash { get; } = string.Empty;

    public string LastKnownPath { get; private set; } = string.Empty;

    public string FileName { get; private set; } = string.Empty;

    public string? CartridgeTitle { get; private set; }

    public CartridgeHardwareKind HardwareKind { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset LastOpenedAt { get; private set; }

    public int LaunchCount { get; private set; }

    public string? CoverPath { get; private set; }

    public static LibraryRom Opened(
        string romHash,
        string lastKnownPath,
        string fileName,
        string? cartridgeTitle,
        CartridgeHardwareKind hardwareKind,
        DateTimeOffset openedAt
    ) =>
        new(
            romHash: romHash,
            lastKnownPath: lastKnownPath,
            fileName: fileName,
            cartridgeTitle: cartridgeTitle,
            hardwareKind,
            openedAt
        );

    public void RecordOpen(
        string lastKnownPath,
        string fileName,
        string? cartridgeTitle,
        CartridgeHardwareKind hardwareKind,
        DateTimeOffset openedAt
    )
    {
        LastKnownPath = lastKnownPath;
        FileName = fileName;
        CartridgeTitle = cartridgeTitle;
        HardwareKind = hardwareKind;
        LastOpenedAt = openedAt;
        LaunchCount++;
    }

    public void SetCoverPath(string? coverPath) => CoverPath = coverPath;

    public void StampCreated(DateTimeOffset timestamp) => AddedAt = timestamp;

    public void StampUpdated(DateTimeOffset timestamp) => UpdatedAt = timestamp;
}
