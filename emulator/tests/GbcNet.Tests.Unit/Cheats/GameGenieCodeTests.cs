// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cheats;

namespace GbcNet.Tests.Unit.Cheats;

public sealed class GameGenieCodeTests
{
    [Theory]
    [InlineData("0A1-B9F", 0x0A, 0x01B9, -1)]
    [InlineData("068-55F-E66", 0x06, 0x0855, 0x03)]
    [InlineData("05D-49C-E62", 0x05, 0x3D49, 0x02)]
    [InlineData("000-FEF-080", 0x00, 0x00FE, 0xBA)]
    public void TryParse_DecodesPublishedVectors(
        string text,
        byte replacementValue,
        ushort address,
        int compareValue
    )
    {
        Assert.True(GameGenieCode.TryParse(text, out var code));

        Assert.Equal(replacementValue, code.ReplacementValue);
        Assert.Equal(address, code.Address);
        Assert.Equal(compareValue < 0 ? null : (byte?)compareValue, code.CompareValue);
    }

    [Theory]
    [InlineData("06855fe66")]
    [InlineData("  068-55f-e66  ")]
    public void TryParse_NormalizesAcceptedForms(string text)
    {
        Assert.True(GameGenieCode.TryParse(text, out var code));

        Assert.Equal("068-55F-E66", code.CanonicalCode);
        Assert.Equal(code.CanonicalCode, code.ToString());
    }

    [Theory]
    [InlineData("120-00F", 0x0000)]
    [InlineData("120-01F", 0x0001)]
    [InlineData("12F-FF8", 0x7FFF)]
    public void TryParse_AcceptsEntireRomAddressRange(string text, ushort address)
    {
        Assert.True(GameGenieCode.TryParse(text, out var code));

        Assert.Equal(address, code.Address);
    }

    [Theory]
    [InlineData("")]
    [InlineData("120-007")]
    [InlineData("12-000F")]
    [InlineData("12000-F")]
    [InlineData("120-00")]
    [InlineData("120-000-00")]
    [InlineData("120-00G")]
    [InlineData("068-55F-E6X")]
    [InlineData("068-55F-EZ6")]
    public void TryParse_RejectsMalformedOrNonRomCodes(string text)
    {
        Assert.False(GameGenieCode.TryParse(text, out var code));

        Assert.False(code.IsValid);
        Assert.Equal(string.Empty, code.CanonicalCode);
        Assert.Equal(string.Empty, code.ToString());
    }

    [Fact]
    public void TryParse_PreservesIgnoredHNibbleWithoutChangingDecodedCode()
    {
        Assert.True(GameGenieCode.TryParse("068-55F-E06", out var first));
        Assert.True(GameGenieCode.TryParse("068-55F-EF6", out var second));

        Assert.NotEqual(first.CanonicalCode, second.CanonicalCode);
        Assert.Equal(first.Address, second.Address);
        Assert.Equal(first.ReplacementValue, second.ReplacementValue);
        Assert.Equal(first.CompareValue, second.CompareValue);
    }
}
