// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Tests.Fixtures;

/// <summary>
/// SM83 opcode constants used by several tests.
/// </summary>
internal static class Opcodes
{
    public const byte NopOpcode = 0x00;
    public const byte StopOpcode = 0x10;
    public const byte IncBOpcode = 0x04;
    public const byte HaltOpcode = 0x76;
}
