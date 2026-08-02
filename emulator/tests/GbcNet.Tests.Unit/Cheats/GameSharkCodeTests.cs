// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cheats;

namespace GbcNet.Tests.Unit.Cheats;

public sealed class GameSharkCodeTests
{
    [Fact]
    public void TryParse_NormalizesAndDecodesLittleEndianMemoryWrite()
    {
        CheatCode.TryParse(CheatCodeType.GameShark, "01ab34c0", out var code).Should().BeTrue();

        code.Type.Should().Be(CheatCodeType.GameShark);
        code.CanonicalCode.Should().Be("01AB34C0");
        code.ToString().Should().Be(code.CanonicalCode);
        code.Value.Should().Be(0xAB);
        code.Address.Should().Be(0xC034);
        code.CompareValue.Should().BeNull();
    }

    [Theory]
    [InlineData("01AA0080", 0x8000)]
    [InlineData("01AAFF9F", 0x9FFF)]
    [InlineData("01AA00C0", 0xC000)]
    [InlineData("01AA9FFE", 0xFE9F)]
    [InlineData("01AA00FF", 0xFF00)]
    [InlineData("01AAFFFF", 0xFFFF)]
    public void TryParse_AcceptsWritableAddressRanges(string text, ushort address)
    {
        CheatCode.TryParse(CheatCodeType.GameShark, text, out var code).Should().BeTrue();

        code.Address.Should().Be(address);
    }

    [Theory]
    [InlineData("")]
    [InlineData("01AAC0C")]
    [InlineData("01AAC0C00")]
    [InlineData("01AAC0CG")]
    [InlineData("00AAC0C0")]
    [InlineData("02AAC0C0")]
    [InlineData("80AAC0C0")]
    [InlineData("90AAC0C0")]
    [InlineData("01AA0000")]
    [InlineData("01AA00A0")]
    [InlineData("01AAA0FE")]
    public void TryParse_RejectsMalformedUnsupportedOrUnwritableCodes(string text)
    {
        CheatCode.TryParse(CheatCodeType.GameShark, text, out _).Should().BeFalse();
    }
}
