// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Input;
using GbcNet.App.Emulation;

namespace GbcNet.Tests.App.Emulation;

public sealed class RomFileFilterTests
{
    [Theory]
    [InlineData("game.gb")]
    [InlineData("game.GB")]
    [InlineData("game.gbc")]
    [InlineData("game.GBC")]
    [InlineData("game.sgb")]
    [InlineData("game.SGB")]
    public void IsRomFileName_AcceptsGameBoyExtensions(string fileName)
    {
        Assert.True(RomFileFilter.IsRomFileName(fileName));
    }

    [Theory]
    [InlineData("game.zip")]
    [InlineData("game")]
    [InlineData("")]
    public void IsRomFileName_RejectsUnsupportedExtensions(string fileName)
    {
        Assert.False(RomFileFilter.IsRomFileName(fileName));
    }

    [Fact]
    public void GetDragEffects_ReturnsCopyWhenDataContainsFileFormat()
    {
        Assert.Equal(DragDropEffects.Copy, RomFileFilter.GetDragEffects([DataFormat.File]));
    }

    [Fact]
    public void GetDragEffects_ReturnsNoneWhenDataDoesNotContainFileFormat()
    {
        Assert.Equal(DragDropEffects.None, RomFileFilter.GetDragEffects([]));
    }

    [Fact]
    public void GetFirstDroppedRom_ReturnsNullWhenNoItemsExist()
    {
        Assert.Null(RomFileFilter.GetFirstDroppedRom(null));
    }
}
