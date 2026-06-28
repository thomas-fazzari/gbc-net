namespace GbcNet.App.Library.Entities;

/// <summary>
/// A ROM, identified by its hash.
/// </summary>
internal sealed record LibraryEntry(
    string RomHash,
    string LastKnownPath,
    string FileName,
    string? CartridgeTitle,
    DateTimeOffset AddedAt,
    DateTimeOffset LastOpenedAt,
    int LaunchCount,
    // TODO: Populate when implementing cover art feature.
    string? CoverPath
);
