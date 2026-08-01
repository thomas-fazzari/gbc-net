// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cheats;

namespace GbcNet.Tests.Unit.Cheats;

public sealed class GameSharkCodeTests
{
    [Fact]
    public void TryParse_NormalizesAndDecodesLittleEndianMemoryWrite()
    {
        Assert.True(CheatCode.TryParse(CheatCodeType.GameShark, "01ab34c0", out var code));

        Assert.Equal(CheatCodeType.GameShark, code.Type);
        Assert.Equal("01AB34C0", code.CanonicalCode);
        Assert.Equal(code.CanonicalCode, code.ToString());
        Assert.Equal(0xAB, code.Value);
        Assert.Equal(0xC034, code.Address);
        Assert.Null(code.CompareValue);
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
        Assert.True(CheatCode.TryParse(CheatCodeType.GameShark, text, out var code));

        Assert.Equal(address, code.Address);
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
        Assert.False(CheatCode.TryParse(CheatCodeType.GameShark, text, out _));
    }
}
