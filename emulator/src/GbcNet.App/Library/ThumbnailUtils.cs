// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace GbcNet.App.Library;

internal static class ThumbnailUtils
{
    internal const int GridCoverSize = 164;
    internal const int MaxDimension = GridCoverSize * 2;
    internal const int MaxDecodedBytes = MaxDimension * MaxDimension * 4;

    public static Bitmap? TryLoad(string? path)
    {
        var target = TryGetTargetSize(path);
        if (target is null)
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(path!);
            return target.Value.Width >= target.Value.Height
                ? Bitmap.DecodeToWidth(stream, target.Value.Width)
                : Bitmap.DecodeToHeight(stream, target.Value.Height);
        }
        catch (Exception exception) when (IsExpectedCoverException(exception))
        {
            return null;
        }
    }

    private static PixelSize? TryGetTargetSize(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        try
        {
            using var codec = SKCodec.Create(path, out var createResult);
            if (createResult is not SKCodecResult.Success)
            {
                return null;
            }

            var source = codec.Info;
            if (source.Width <= 0 || source.Height <= 0)
            {
                return null;
            }

            return CalculateTargetSize(source.Width, source.Height);
        }
        catch (Exception exception) when (IsExpectedCoverException(exception))
        {
            return null;
        }
    }

    private static PixelSize CalculateTargetSize(int width, int height)
    {
        var scale = Math.Min(1, MaxDimension / (double)Math.Max(width, height));
        return new PixelSize(
            Math.Max(1, (int)Math.Round(width * scale)),
            Math.Max(1, (int)Math.Round(height * scale))
        );
    }

    private static bool IsExpectedCoverException(Exception exception) =>
        exception
            is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or NotSupportedException
                or ArgumentException
                or OverflowException;
}
