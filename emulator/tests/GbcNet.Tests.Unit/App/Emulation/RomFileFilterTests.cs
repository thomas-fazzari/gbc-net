// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using Avalonia.Input;
using GbcNet.App.Emulation;

namespace GbcNet.Tests.Unit.App.Emulation;

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
        RomFileFilter.IsRomFileName(fileName).Should().BeTrue();
    }

    [Theory]
    [InlineData("game.zip")]
    [InlineData("game")]
    [InlineData("")]
    public void IsRomFileName_RejectsUnsupportedExtensions(string fileName)
    {
        RomFileFilter.IsRomFileName(fileName).Should().BeFalse();
    }

    [Fact]
    public void GetDragEffects_ReturnsCopyWhenDataContainsFileFormat()
    {
        RomFileFilter.GetDragEffects([DataFormat.File]).Should().Be(DragDropEffects.Copy);
    }

    [Fact]
    public void GetDragEffects_ReturnsNoneWhenDataDoesNotContainFileFormat()
    {
        RomFileFilter.GetDragEffects([]).Should().Be(DragDropEffects.None);
    }

    [Fact]
    public void GetFirstDroppedRom_ReturnsNullWhenNoItemsExist()
    {
        RomFileFilter.GetFirstDroppedRom(null).Should().BeNull();
    }
}
