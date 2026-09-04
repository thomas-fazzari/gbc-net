// Copyright (C) 2026 GBC.Net Contributors
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
        CheatCode.TryParse(CheatCodeType.GameGenie, text, out var code).Should().BeTrue();

        code.Type.Should().Be(CheatCodeType.GameGenie);
        code.Value.Should().Be(replacementValue);
        code.Address.Should().Be(address);
        code.CompareValue.Should().Be(compareValue < 0 ? null : (byte?)compareValue);
    }

    [Theory]
    [InlineData("06855fe66")]
    [InlineData("  068-55f-e66  ")]
    public void TryParse_NormalizesAcceptedForms(string text)
    {
        CheatCode.TryParse(CheatCodeType.GameGenie, text, out var code).Should().BeTrue();

        code.CanonicalCode.Should().Be("068-55F-E66");
        code.ToString().Should().Be(code.CanonicalCode);
    }

    [Theory]
    [InlineData("120-00F", 0x0000)]
    [InlineData("120-01F", 0x0001)]
    [InlineData("12F-FF8", 0x7FFF)]
    public void TryParse_AcceptsEntireRomAddressRange(string text, ushort address)
    {
        CheatCode.TryParse(CheatCodeType.GameGenie, text, out var code).Should().BeTrue();

        code.Address.Should().Be(address);
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
        CheatCode.TryParse(CheatCodeType.GameGenie, text, out var code).Should().BeFalse();

        code.IsValid.Should().BeFalse();
        code.CanonicalCode.Should().Be(string.Empty);
        code.ToString().Should().Be(string.Empty);
    }

    [Fact]
    public void TryParse_PreservesIgnoredHNibbleWithoutChangingDecodedCode()
    {
        CheatCode.TryParse(CheatCodeType.GameGenie, "068-55F-E06", out var first).Should().BeTrue();
        CheatCode
            .TryParse(CheatCodeType.GameGenie, "068-55F-EF6", out var second)
            .Should()
            .BeTrue();

        second.CanonicalCode.Should().NotBe(first.CanonicalCode);
        second.Address.Should().Be(first.Address);
        second.Value.Should().Be(first.Value);
        second.CompareValue.Should().Be(first.CompareValue);
    }
}
