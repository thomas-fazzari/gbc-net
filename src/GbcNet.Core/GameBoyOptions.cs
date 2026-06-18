namespace GbcNet.Core;

/// <summary>
/// Optional model-specific boot ROM images used to start execution at the hardware reset vector.
/// </summary>
public sealed class GameBoyOptions
{
    /// <summary>
    /// Required byte length of a DMG boot ROM image.
    /// </summary>
    public const int DmgBootRomSize = 256;

    /// <summary>
    /// Required byte length of a CGB boot ROM image.
    /// </summary>
    public const int CgbBootRomSize = 2048;

    /// <summary>
    /// Optional 256-byte DMG boot ROM image.
    /// </summary>
    /// <remarks>
    /// Empty keeps the post-boot hand-off seed path.
    /// </remarks>
    public ReadOnlyMemory<byte> DmgBootRom { get; init; }

    /// <summary>
    /// Optional 2048-byte CGB boot ROM image.
    /// </summary>
    /// <remarks>
    /// Empty keeps the post-boot hand-off seed path.
    /// </remarks>
    public ReadOnlyMemory<byte> CgbBootRom { get; init; }
}
