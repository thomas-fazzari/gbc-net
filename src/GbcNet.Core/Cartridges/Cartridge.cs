// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using GbcNet.Core.Cartridges.Memory;
using GbcNet.Core.Memory;

namespace GbcNet.Core.Cartridges;

/// <summary>
/// Loaded Game Boy cartridge image with mutable MBC/RAM state.
/// </summary>
public sealed class Cartridge
{
    public const int FixedRomBankSize = 16 * 1024;

    private const int AddressableRomSize = 2 * FixedRomBankSize;
    private readonly ICartridgeMemoryController _memoryController;

    private Cartridge(CartridgeHeader header, ICartridgeMemoryController memoryController)
    {
        Header = header;
        _memoryController = memoryController;
        RomLength = header.RomSizeBytes;
    }

    /// <summary>
    /// Parsed cartridge header metadata.
    /// </summary>
    public CartridgeHeader Header { get; }

    /// <summary>
    /// Full ROM payload length, in bytes.
    /// </summary>
    public int RomLength { get; }

    /// <summary>
    /// Indicates whether the cartridge has battery-backed save data.
    /// </summary>
    public bool HasBatteryBackedSave => _memoryController.SaveData.HasBatteryBackedSave;

    /// <summary>
    /// Battery-backed save payload size, in bytes.
    /// </summary>
    public int BatterySaveSize => _memoryController.SaveData.BatterySaveSize;

    /// <summary>
    /// Indicates whether battery-backed save data changed since load or the last clear.
    /// </summary>
    public bool IsBatterySaveDirty => _memoryController.SaveData.IsBatterySaveDirty;

    /// <summary>
    /// Parses and loads a cartridge image.
    /// </summary>
    /// <returns>
    /// A loaded cartridge, or a typed cartridge load error.
    /// </returns>
    public static CartridgeLoadResult Load(ReadOnlySpan<byte> rom) =>
        Load(rom, static () => DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    internal static CartridgeLoadResult Load(ReadOnlySpan<byte> rom, Func<long> getUnixTimeSeconds)
    {
        if (!CartridgeHeader.TryParse(rom, out var header, out var error))
        {
            return CartridgeLoadResult.Failure(error);
        }

        if (rom.Length != header.RomSizeBytes)
        {
            return CartridgeLoadResult.Failure(
                new CartridgeLoadError(
                    CartridgeLoadErrorCode.RomLengthMismatch,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "ROM length is {0} bytes, but header declares {1} bytes.",
                        rom.Length,
                        header.RomSizeBytes
                    )
                )
            );
        }

        var romBytes = rom.ToArray();
        return TryCreateMemoryController(
            romBytes,
            header,
            getUnixTimeSeconds,
            out var memoryController,
            out error
        )
            ? CartridgeLoadResult.Success(new Cartridge(header, memoryController))
            : CartridgeLoadResult.Failure(error);
    }

    /// <summary>
    /// Reads from the cartridge ROM address window at 0000-7FFF.
    /// </summary>
    /// <param name="address">
    /// CPU-visible cartridge ROM address.
    /// </param>
    /// <returns>
    /// The ROM byte at the requested address.
    /// </returns>
    public byte ReadRom(ushort address)
    {
        if (address >= AddressableRomSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(address),
                address,
                "Cartridge ROM address must be in the 0000-7FFF range."
            );
        }

        return _memoryController.ReadRom(address);
    }

    /// <summary>
    /// Writes to the cartridge ROM window, where MBC registers are mapped.
    /// </summary>
    public void WriteRom(ushort address, byte value)
    {
        if (address >= AddressableRomSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(address),
                address,
                "Cartridge ROM address must be in the 0000-7FFF range."
            );
        }

        _memoryController.WriteRom(address, value);
    }

    /// <summary>
    /// Reads from cartridge external RAM at A000-BFFF.
    /// </summary>
    public byte ReadRam(ushort address)
    {
        if (address is < AddressMap.ExternalRamStart or > AddressMap.ExternalRamEnd)
        {
            throw new ArgumentOutOfRangeException(
                nameof(address),
                address,
                "Cartridge RAM address must be in the A000-BFFF range."
            );
        }

        return ReadRamOffset(GetExternalRamOffset(address));
    }

    /// <summary>
    /// Writes to cartridge external RAM at A000-BFFF.
    /// </summary>
    public void WriteRam(ushort address, byte value)
    {
        if (address is < AddressMap.ExternalRamStart or > AddressMap.ExternalRamEnd)
        {
            throw new ArgumentOutOfRangeException(
                nameof(address),
                address,
                "Cartridge RAM address must be in the A000-BFFF range."
            );
        }

        _memoryController.WriteRamOffset(GetExternalRamOffset(address), value);
    }

    /// <summary>
    /// Reads from cartridge external RAM using a cartridge-local 8 KiB bank offset.
    /// </summary>
    private byte ReadRamOffset(ushort offset)
    {
        if (offset >= AddressMap.ExternalRamWindowSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                offset,
                "Cartridge RAM offset must be in the 0000-1FFF range."
            );
        }

        return _memoryController.ReadRamOffset(offset);
    }

    /// <summary>
    /// Copies battery-backed save data for persistence.
    /// </summary>
    public byte[] ExportBatterySave() => _memoryController.SaveData.ExportBatterySave();

    /// <summary>
    /// Loads persisted battery-backed save data into the cartridge.
    /// </summary>
    public bool TryImportBatterySave(
        ReadOnlySpan<byte> data,
        [NotNullWhen(false)] out string? errorMessage
    ) => _memoryController.SaveData.TryImportBatterySave(data, out errorMessage);

    /// <summary>
    /// Marks battery-backed save data as persisted.
    /// </summary>
    public void ClearBatterySaveDirty()
    {
        _memoryController.SaveData.ClearBatterySaveDirty();
    }

    private static bool TryCreateMemoryController(
        byte[] rom,
        CartridgeHeader header,
        Func<long> getUnixTimeSeconds,
        [NotNullWhen(true)] out ICartridgeMemoryController? memoryController,
        [NotNullWhen(false)] out CartridgeLoadError? error
    )
    {
        var cartridgeType = header.CartridgeType;
        memoryController = cartridgeType switch
        {
            _ when cartridgeType.IsNoMbc() => new NoMbcMemoryController(
                rom,
                header,
                cartridgeType.HasBatteryBackedExternalRam()
            ),
            _ when cartridgeType.IsMbc1() => new Mbc1MemoryController(
                rom,
                header,
                cartridgeType.HasBatteryBackedExternalRam()
            ),
            _ when cartridgeType.IsMbc2() => new Mbc2MemoryController(
                rom,
                header,
                cartridgeType is CartridgeType.Mbc2Battery
            ),
            _ when cartridgeType.IsMbc3() => new Mbc3MemoryController(
                rom,
                header,
                cartridgeType.HasBatteryBackedExternalRam(),
                cartridgeType.HasRtc(),
                getUnixTimeSeconds
            ),
            _ when cartridgeType.IsMbc5() => new Mbc5MemoryController(
                rom,
                header,
                cartridgeType.HasBatteryBackedExternalRam()
            ),
            _ => null,
        };

        if (memoryController is not null)
        {
            error = null;
            return true;
        }

        error = new CartridgeLoadError(
            CartridgeLoadErrorCode.UnsupportedCartridgeType,
            string.Format(
                CultureInfo.InvariantCulture,
                "Cartridge type 0x{0:X2} is not supported yet.",
                (int)cartridgeType
            )
        );
        return false;
    }

    private static ushort GetExternalRamOffset(ushort address) =>
        (ushort)(address - AddressMap.ExternalRamStart);
}
