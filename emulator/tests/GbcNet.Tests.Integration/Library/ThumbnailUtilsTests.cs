// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia;
using GbcNet.App.Library;
using SkiaSharp;

namespace GbcNet.Tests.Integration.Library;

public sealed class ThumbnailUtilsTests
{
    // Avalonia bitmap decoding needs Skia initialized, but these tests do not create windows.
    static ThumbnailUtilsTests() =>
        AppBuilder
            .Configure<Application>()
            .UseStandardRuntimePlatformSubsystem()
            .UseWindowingSubsystem(() => GC.KeepAlive(typeof(Application)), "Tests")
            .UseSkia()
            .UseHarfBuzz()
            .SetupWithoutStarting();

    public static TheoryData<int, int> SourceDimensions => new() { { 2048, 1024 }, { 1024, 2048 } };

    [Theory]
    [MemberData(nameof(SourceDimensions))]
    public void TryLoad_BoundsDimensionsAndDecodedMemory(int width, int height)
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        Directory.CreateDirectory(tempDirectory.Path);
        var path = Path.Combine(tempDirectory.Path, "cover.png");
        WritePng(path, width, height);

        using var thumbnail = ThumbnailUtils.TryLoad(path);

        thumbnail.Should().NotBeNull();
        var pixelSize = thumbnail.PixelSize;
        Math.Max(pixelSize.Width, pixelSize.Height).Should().Be(ThumbnailUtils.MaxDimension);
        (pixelSize.Width * pixelSize.Height * 4)
            .Should()
            .BeLessThanOrEqualTo(ThumbnailUtils.MaxDecodedBytes);
    }

    [Fact]
    public void TryLoad_ReturnsNullForInvalidImage()
    {
        using var tempDirectory = TestDirectories.CreateTemporaryDirectory();
        Directory.CreateDirectory(tempDirectory.Path);
        var path = Path.Combine(tempDirectory.Path, "invalid.png");
        File.WriteAllBytes(path, "not an image"u8);

        ThumbnailUtils.TryLoad(path).Should().BeNull();
    }

    private static void WritePng(string path, int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, quality: 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }
}
