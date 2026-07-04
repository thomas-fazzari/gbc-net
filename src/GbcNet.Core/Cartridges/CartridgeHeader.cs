// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Text;
using FluentResults;

namespace GbcNet.Core.Cartridges;

/// <summary>
/// Parsed metadata from the cartridge header at ROM addresses 0100-014F.
/// </summary>
/// <param name="Title">
/// ASCII game title from the header title area.
/// </param>
/// <param name="CgbSupport">
/// Declared CGB compatibility from header byte 0143.
/// </param>
/// <param name="HardwareKind">
/// Primary hardware family advertised by CGB and SGB header flags.
/// </param>
/// <param name="CartridgeType">
/// Cartridge hardware type from header byte 0147.
/// </param>
/// <param name="RomSizeBytes">
/// Declared ROM size, in bytes.
/// </param>
/// <param name="RomBankCount">
/// Declared count of 16 KiB ROM banks.
/// </param>
/// <param name="RamSizeBytes">
/// Declared external cartridge RAM size, in bytes.
/// </param>
/// <param name="RamBankCount">
/// Declared count of 8 KiB external RAM banks.
/// </param>
/// <param name="HeaderChecksum">
/// Header checksum byte at ROM address 014D.
/// </param>
public sealed record CartridgeHeader(
    string Title,
    CgbSupport CgbSupport,
    CartridgeHardwareKind HardwareKind,
    CartridgeType CartridgeType,
    int RomSizeBytes,
    int RomBankCount,
    int RamSizeBytes,
    int RamBankCount,
    byte HeaderChecksum
)
{
    private const int HeaderEndAddress = 0x014F;
    private const int TitleStartAddress = 0x0134;
    private const int TitleEndAddress = 0x0143;
    private const int NewHeaderTitleEndAddress = 0x013E;
    private const int CgbTitleEndAddress = 0x0142;
    private const int CgbFlagAddress = 0x0143;
    private const int SgbFlagAddress = 0x0146;
    private const int CartridgeTypeAddress = 0x0147;
    private const int RomSizeAddress = 0x0148;
    private const int RamSizeAddress = 0x0149;
    private const int OldLicenseeCodeAddress = 0x014B;
    private const int HeaderChecksumAddress = 0x014D;
    private const byte SgbEnhancedFlag = 0x03;
    private const byte SgbLicenseeCode = 0x33;

    /// <summary>
    /// Parses cartridge header fields and validates the header checksum.
    /// </summary>
    /// <returns>
    /// A parsed header, or a typed cartridge load error.
    /// </returns>
    public static Result<CartridgeHeader> Parse(ReadOnlySpan<byte> rom)
    {
        var headerValidation = ValidateHeaderRangeAndChecksum(rom);

        if (headerValidation.IsFailed)
        {
            return Result.Fail<CartridgeHeader>(headerValidation.Errors);
        }

        var romSizeResult = DecodeRomSize(rom[RomSizeAddress]);
        if (romSizeResult.IsFailed)
        {
            return Result.Fail<CartridgeHeader>(romSizeResult.Errors);
        }

        var ramSizeResult = DecodeRamSize(rom[RamSizeAddress]);
        if (ramSizeResult.IsFailed)
        {
            return Result.Fail<CartridgeHeader>(ramSizeResult.Errors);
        }

        var (romSizeBytes, romBankCount) = romSizeResult.Value;
        var (ramSizeBytes, ramBankCount) = ramSizeResult.Value;
        var cgbSupport = DecodeCgbSupport(rom[CgbFlagAddress]);

        return Result.Ok(
            Create(rom, cgbSupport, romSizeBytes, romBankCount, ramSizeBytes, ramBankCount)
        );
    }

    private static Result ValidateHeaderRangeAndChecksum(ReadOnlySpan<byte> rom)
    {
        if (rom.Length <= HeaderEndAddress)
        {
            return Result.Fail(
                new CartridgeLoadError(
                    CartridgeLoadErrorCode.RomTooSmall,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ROM must contain at least {0} bytes to include the cartridge header.",
                        HeaderEndAddress + 1
                    )
                )
            );
        }

        var expectedChecksum = CalculateHeaderChecksum(rom);
        var actualChecksum = rom[HeaderChecksumAddress];

        if (actualChecksum == expectedChecksum)
        {
            return Result.Ok();
        }

        return Result.Fail(
            new CartridgeLoadError(
                CartridgeLoadErrorCode.InvalidHeaderChecksum,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Header checksum is 0x{0:X2}, expected 0x{1:X2}.",
                    actualChecksum,
                    expectedChecksum
                )
            )
        );
    }

    private static CartridgeHeader Create(
        ReadOnlySpan<byte> rom,
        CgbSupport cgbSupport,
        int romSizeBytes,
        int romBankCount,
        int ramSizeBytes,
        int ramBankCount
    ) =>
        new(
            ReadTitle(rom, cgbSupport),
            cgbSupport,
            DecodeHardwareKind(cgbSupport, rom[SgbFlagAddress], rom[OldLicenseeCodeAddress]),
            (CartridgeType)rom[CartridgeTypeAddress],
            romSizeBytes,
            romBankCount,
            ramSizeBytes,
            ramBankCount,
            rom[HeaderChecksumAddress]
        );

    /// <summary>
    /// Calculates the checksum over header bytes 0134-014C.
    /// </summary>
    /// <returns>
    /// The checksum byte expected at ROM address 014D.
    /// </returns>
    internal static byte CalculateHeaderChecksum(ReadOnlySpan<byte> rom)
    {
        if (rom.Length <= HeaderChecksumAddress)
        {
            throw new ArgumentException(
                "ROM must contain the cartridge header checksum byte.",
                nameof(rom)
            );
        }

        byte checksum = 0;
        for (var offset = TitleStartAddress; offset < HeaderChecksumAddress; offset++)
        {
            checksum = unchecked((byte)(checksum - rom[offset] - 1));
        }

        return checksum;
    }

    private static string ReadTitle(ReadOnlySpan<byte> rom, CgbSupport cgbSupport)
    {
        var titleEndAddress = GetTitleEndAddress(rom, cgbSupport);
        var titleBytes = rom[TitleStartAddress..(titleEndAddress + 1)];
        var terminatorIndex = titleBytes.IndexOf((byte)0);
        if (terminatorIndex >= 0)
        {
            titleBytes = titleBytes[..terminatorIndex];
        }

        return Encoding.ASCII.GetString(titleBytes);
    }

    private static CgbSupport DecodeCgbSupport(byte flag) =>
        flag switch
        {
            0x80 => CgbSupport.Enhanced,
            0xC0 => CgbSupport.Required,
            _ => CgbSupport.None,
        };

    private static CartridgeHardwareKind DecodeHardwareKind(
        CgbSupport cgbSupport,
        byte sgbFlag,
        byte oldLicenseeCode
    )
    {
        if (cgbSupport is CgbSupport.Enhanced or CgbSupport.Required)
        {
            return CartridgeHardwareKind.GBC;
        }

        return sgbFlag == SgbEnhancedFlag && oldLicenseeCode == SgbLicenseeCode
            ? CartridgeHardwareKind.SGB
            : CartridgeHardwareKind.GB;
    }

    private static int GetTitleEndAddress(ReadOnlySpan<byte> rom, CgbSupport cgbSupport)
    {
        if (rom[OldLicenseeCodeAddress] == 0x33)
        {
            return NewHeaderTitleEndAddress;
        }

        return cgbSupport == CgbSupport.None ? TitleEndAddress : CgbTitleEndAddress;
    }

    private static Result<(int SizeBytes, int BankCount)> DecodeRomSize(byte code) =>
        code switch
        {
            <= 0x08 => Result.Ok((32 * 1024 * (1 << code), 2 * (1 << code))),
            0x52 => Result.Ok((1_152 * 1024, 72)),
            0x53 => Result.Ok((1_280 * 1024, 80)),
            0x54 => Result.Ok((1_536 * 1024, 96)),
            _ => Result.Fail<(int SizeBytes, int BankCount)>(
                new CartridgeLoadError(
                    CartridgeLoadErrorCode.UnsupportedRomSize,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ROM size code 0x{0:X2} is not supported.",
                        code
                    )
                )
            ),
        };

    private static Result<(int SizeBytes, int BankCount)> DecodeRamSize(byte code) =>
        code switch
        {
            0x00 => Result.Ok((0, 0)),
            0x02 => Result.Ok((8 * 1024, 1)),
            0x03 => Result.Ok((32 * 1024, 4)),
            0x04 => Result.Ok((128 * 1024, 16)),
            0x05 => Result.Ok((64 * 1024, 8)),
            _ => Result.Fail<(int SizeBytes, int BankCount)>(
                new CartridgeLoadError(
                    CartridgeLoadErrorCode.UnsupportedRamSize,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "RAM size code 0x{0:X2} is not supported.",
                        code
                    )
                )
            ),
        };
}
