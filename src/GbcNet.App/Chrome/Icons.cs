using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Svg;

namespace GbcNet.App.Chrome;

internal static class Icons
{
#pragma warning disable S1075 // URIs should not be hardcoded

    public const string FileCog = "avares://GbcNet/Assets/Icons/file-cog.svg";
    public const string Gamepad = "avares://GbcNet/Assets/Icons/gamepad.svg";

#pragma warning restore S1075 // URIs should not be hardcoded

    public static Image Make(string assetPath, double size = 16) =>
        new()
        {
            Source = new SvgImage { Source = SvgSource.Load(assetPath, baseUri: null) },
            Width = size,
            Height = size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
}
