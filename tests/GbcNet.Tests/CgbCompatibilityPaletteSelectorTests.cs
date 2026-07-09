// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Hardware.Profiles;
using GbcNet.Tests.Cartridges;

namespace GbcNet.Tests;

public sealed class CgbCompatibilityPaletteSelectorTests
{
    private static readonly CgbCompatibilityPalettes _defaultPalettes = new(
        Background: new CgbCompatibilityPalette(0x7FFF, 0x1BEF, 0x6180, 0x0000),
        ObjectPalette0: new CgbCompatibilityPalette(0x7FFF, 0x421F, 0x1CF2, 0x0000),
        ObjectPalette1: new CgbCompatibilityPalette(0x7FFF, 0x421F, 0x1CF2, 0x0000)
    );

    [Fact]
    public void Select_FallsBackToDefaultPaletteForNonNintendoLicensee()
    {
        var selection = CgbCompatibilityPaletteSelector.Select(
            LoadCartridge("POKEMON RED", oldLicenseeCode: 0x08)
        );

        Assert.Equal(0x00, selection.TitleChecksum);
        Assert.Equal(0x00, selection.PaletteId);
        Assert.False(selection.UsesCompatibilityLogoTilemap);
        Assert.Equal(_defaultPalettes, selection.Palettes);
    }

    [Fact]
    public void Select_UsesPokemonRedPaletteForNintendoNewLicensee()
    {
        var selection = CgbCompatibilityPaletteSelector.Select(
            LoadCartridge("POKEMON RED", oldLicenseeCode: 0x33, newLicenseeCode: "01")
        );

        Assert.Equal(0x14, selection.TitleChecksum);
        Assert.Equal(0x0D, selection.PaletteId);
        Assert.False(selection.UsesCompatibilityLogoTilemap);
        Assert.Equal(
            new CgbCompatibilityPalettes(
                Background: new CgbCompatibilityPalette(0x7FFF, 0x421F, 0x1CF2, 0x0000),
                ObjectPalette0: new CgbCompatibilityPalette(0x7FFF, 0x1BEF, 0x0200, 0x0000),
                ObjectPalette1: new CgbCompatibilityPalette(0x7FFF, 0x421F, 0x1CF2, 0x0000)
            ),
            selection.Palettes
        );
    }

    [Fact]
    public void Select_UsesFourthLetterToResolveDuplicateChecksum()
    {
        var selection = CgbCompatibilityPaletteSelector.Select(
            LoadCartridge("POKEMON BLUE", oldLicenseeCode: 0x01)
        );

        Assert.Equal(0x61, selection.TitleChecksum);
        Assert.Equal(0x0B, selection.PaletteId);
        Assert.False(selection.UsesCompatibilityLogoTilemap);
        Assert.Equal(
            new CgbCompatibilityPalettes(
                Background: new CgbCompatibilityPalette(0x7FFF, 0x7E8C, 0x7C00, 0x0000),
                ObjectPalette0: new CgbCompatibilityPalette(0x7FFF, 0x421F, 0x1CF2, 0x0000),
                ObjectPalette1: new CgbCompatibilityPalette(0x7FFF, 0x7E8C, 0x7C00, 0x0000)
            ),
            selection.Palettes
        );
    }

    [Fact]
    public void Select_MarksCompatibilityLogoTilemapForMatchingTitle()
    {
        var selection = CgbCompatibilityPaletteSelector.Select(
            LoadCartridge("X", oldLicenseeCode: 0x01)
        );

        Assert.Equal(0x58, selection.TitleChecksum);
        Assert.Equal(0x07, selection.PaletteId);
        Assert.True(selection.UsesCompatibilityLogoTilemap);
    }

    private static Cartridge LoadCartridge(
        string title,
        byte oldLicenseeCode,
        string? newLicenseeCode = null
    ) =>
        TestRomFactory.LoadCartridge(bytes =>
        {
            bytes.AsSpan(0x0134, 16).Clear();
            Encoding.ASCII.GetBytes(title[..Math.Min(title.Length, 16)]).CopyTo(bytes, 0x0134);
            bytes[0x014B] = oldLicenseeCode;

            if (newLicenseeCode is null)
            {
                return;
            }

            bytes[0x0144] = (byte)newLicenseeCode[0];
            bytes[0x0145] = (byte)newLicenseeCode[1];
        });
}
