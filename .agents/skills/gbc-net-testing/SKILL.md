---
name: gbc-net-testing
description: Apply GBC.Net-specific test boundaries and hardware regression rules. Use for unit, integration, ROM, emulator, hardware, persistence, or application tests.
---

# GBC.Net Testing

Test behavior at the smallest tier that proves it. Unit tests cover Core
hardware, emulator state, ROM compatibility, and isolated App services.
Integration tests cover filesystem, SQLite, configuration, saves, library, and
application workflows.

Reuse helpers from `GbcNet.Tests.Shared` before adding private copies.

## Assertions

Use AwesomeAssertions for assertions and xUnit for discovery and data. Keep `AwesomeAssertions` as a global using. Use `Be` for scalars, `Equal` for ordered collections, and `BeEquivalentTo` only for deliberate structural comparisons. Assert exceptions through `FluentActions`. Use `AssertionScope` only for independent failures.

## Hardware and ROM tests

Before asserting Game Boy behavior, load the matching Pan Docs chapter through
`gameboy-development`. State the target hardware mode: DMG, CGB mode, CGB DMG
compatibility mode, or SGB.

* Test at the closest hardware boundary: instruction, controller, bus register,
  DMA transfer, PPU frame, APU sample, or saved state.
* Cover timing with exact M-cycles, T-cycles, dots, or frame boundaries. Do not
  replace hardware timing with arbitrary delays.
* Test reset and `CaptureState`/`RestoreState` round trips where state behavior
  changes. Include malformed or incompatible state rejection when applicable.
* Keep ROM tests deterministic. Assert ROM status and exact visual or audio
  output where resource baselines exist.

## Integration tests

Use real temporary directories and SQLite files for persistence, migrations,
locking, configuration, and save-file behavior. Do not mock EF Core,
DbContext, or entities. Do not use EF Core InMemory.

Use `TimeProvider` for deterministic time. Do not synchronize with
`Thread.Sleep` or arbitrary `Task.Delay`; use observable state,
`TaskCompletionSource`, and a bounded timeout. Do not capture `using` resources
in delayed lambdas; capture the needed service, memory, path, or token first.

## Microsoft.Testing.Platform

This repository uses Microsoft.Testing.Platform, not VSTest. Do not pass
`dotnet test --filter`; filter through `TEST_ARGS` in Makefile targets instead.
