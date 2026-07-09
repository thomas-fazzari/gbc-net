// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

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
    internal static bool TryParse(
        ReadOnlySpan<byte> rom,
        [NotNullWhen(true)] out CartridgeHeader? header,
        [NotNullWhen(false)] out CartridgeLoadError? error
    )
    {
        header = null;

        if (!ValidateHeaderRangeAndChecksum(rom, out error))
        {
            return false;
        }

        if (
            !TryDecodeRomSize(
                rom[RomSizeAddress],
                out var romSizeBytes,
                out var romBankCount,
                out error
            )
        )
        {
            return false;
        }

        if (
            !TryDecodeRamSize(
                rom[RamSizeAddress],
                out var ramSizeBytes,
                out var ramBankCount,
                out error
            )
        )
        {
            return false;
        }

        var cgbSupport = DecodeCgbSupport(rom[CgbFlagAddress]);
        header = Create(rom, cgbSupport, romSizeBytes, romBankCount, ramSizeBytes, ramBankCount);
        error = null;
        return true;
    }

    private static bool ValidateHeaderRangeAndChecksum(
        ReadOnlySpan<byte> rom,
        [NotNullWhen(false)] out CartridgeLoadError? error
    )
    {
        if (rom.Length <= HeaderEndAddress)
        {
            error = new CartridgeLoadError(
                CartridgeLoadErrorCode.RomTooSmall,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "ROM must contain at least {0} bytes to include the cartridge header.",
                    HeaderEndAddress + 1
                )
            );
            return false;
        }

        var expectedChecksum = CalculateHeaderChecksum(rom);
        var actualChecksum = rom[HeaderChecksumAddress];

        if (actualChecksum == expectedChecksum)
        {
            error = null;
            return true;
        }

        error = new CartridgeLoadError(
            CartridgeLoadErrorCode.InvalidHeaderChecksum,
            string.Format(
                CultureInfo.InvariantCulture,
                "Header checksum is 0x{0:X2}, expected 0x{1:X2}.",
                actualChecksum,
                expectedChecksum
            )
        );
        return false;
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

    private static bool TryDecodeRomSize(
        byte code,
        out int sizeBytes,
        out int bankCount,
        [NotNullWhen(false)] out CartridgeLoadError? error
    )
    {
        switch (code)
        {
            case <= 0x08:
                sizeBytes = 32 * 1024 * (1 << code);
                bankCount = 2 * (1 << code);
                error = null;
                return true;
            case 0x52:
                sizeBytes = 1_152 * 1024;
                bankCount = 72;
                error = null;
                return true;
            case 0x53:
                sizeBytes = 1_280 * 1024;
                bankCount = 80;
                error = null;
                return true;
            case 0x54:
                sizeBytes = 1_536 * 1024;
                bankCount = 96;
                error = null;
                return true;
            default:
                sizeBytes = 0;
                bankCount = 0;
                error = new CartridgeLoadError(
                    CartridgeLoadErrorCode.UnsupportedRomSize,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ROM size code 0x{0:X2} is not supported.",
                        code
                    )
                );
                return false;
        }
    }

    private static bool TryDecodeRamSize(
        byte code,
        out int sizeBytes,
        out int bankCount,
        [NotNullWhen(false)] out CartridgeLoadError? error
    )
    {
        switch (code)
        {
            case 0x00:
                sizeBytes = 0;
                bankCount = 0;
                error = null;
                return true;
            case 0x02:
                sizeBytes = 8 * 1024;
                bankCount = 1;
                error = null;
                return true;
            case 0x03:
                sizeBytes = 32 * 1024;
                bankCount = 4;
                error = null;
                return true;
            case 0x04:
                sizeBytes = 128 * 1024;
                bankCount = 16;
                error = null;
                return true;
            case 0x05:
                sizeBytes = 64 * 1024;
                bankCount = 8;
                error = null;
                return true;
            default:
                sizeBytes = 0;
                bankCount = 0;
                error = new CartridgeLoadError(
                    CartridgeLoadErrorCode.UnsupportedRamSize,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "RAM size code 0x{0:X2} is not supported.",
                        code
                    )
                );
                return false;
        }
    }
}
