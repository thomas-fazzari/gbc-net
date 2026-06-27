namespace GbcNet.App.Library.Entities;

internal sealed record LibraryEntry(
    string RomHash,
    string LastKnownPath,
    string FileName,
    string? CartridgeTitle,
    string AddedAt,
    string LastOpenedAt,
    int LaunchCount,
    // TODO: Populate when implementing cover art feature.
    string? CoverPath
);
