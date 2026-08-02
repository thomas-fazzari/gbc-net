---
name: gbc-net-testing
description: >
  Apply GBC.Net test strategy with xUnit v3, Microsoft.Testing.Platform, and
  AwesomeAssertions. Use for any unit, integration, ROM compatibility, emulator
  state, hardware regression, persistence, or application test.
---

# GBC.Net Testing

Test behavior at the smallest tier that proves it.

## Organize Tests

* `GbcNet.Tests.Unit`: Core hardware behavior, emulator state, ROM compatibility,
  and isolated App services.
* `GbcNet.Tests.Integration`: filesystem, SQLite, configuration, saves, library,
  and application workflows.
* `GbcNet.Tests.Shared`: helpers and fixtures used by both test projects.

Use xUnit v3 through Microsoft.Testing.Platform. Name tests
`Method_Condition_Expected`.

Prefer observable behavior, register values, memory
contents, frames, audio samples, saved bytes, and user-visible application state
instead of implementation details.

Use AwesomeAssertions for assertions and xUnit for discovery and data. Keep
`AwesomeAssertions` as a global using. Write subject-first assertions:

* Use `Be` for scalar equality.
* Use `Equal` when collection order and item equality are the contract.
* Use `BeEquivalentTo` only for deliberate structural comparisons.
* Use `ContainSingle(predicate).Which` when the matched value is needed.
* Assert exceptions through `FluentActions` and verify meaningful messages where
  the message is part of the contract.
* Use `AssertionScope` only when independent failures should be reported together.

## Hardware and ROM Tests

Before asserting Game Boy behavior, load the matching Pan Docs chapter through
`gameboy-development`. State the target hardware mode: DMG, CGB mode, CGB DMG
compatibility mode, or SGB.

* Test at the closest hardware boundary: instruction, controller, bus register,
  DMA transfer, PPU frame, APU sample, or saved state.
* Cover timing with exact M-cycles, T-cycles, dots, or frame boundaries. Do not
  replace hardware timing with arbitrary delays.
* Test reset and `CaptureState`/`RestoreState` round trips where state behavior is
  changed. Include malformed or incompatible state rejection when applicable.
* Keep ROM tests deterministic. Assert ROM status and exact visual/audio output
  where resource baselines exist.

## Integration Tests

Use real temporary directories and SQLite files for persistence, migrations,
locking, configuration, and save-file behavior. Do not mock EF Core, DbContext,
or entities. Do not use EF Core InMemory.

Use `TimeProvider` for deterministic time. Do not synchronize with `Thread.Sleep`
or arbitrary `Task.Delay`.

Use observable state, `TaskCompletionSource`, and a
bounded timeout.

Do not capture `using` resources in delayed lambdas: capture the
needed service, memory, path, or token first.

## Commands

When `rtk` is available, .NET unit tests must run from `emulator/` through:

```text
rtk test dotnet test --project tests/GbcNet.Tests.Unit/GbcNet.Tests.Unit.csproj
```

Never invoke `rtk dotnet test`. If `rtk` is unavailable, run:

```text
make unit CONFIGURATION=Release
```

Run integration and lint from the repository root through Makefile:

```text
make integration-c CONFIGURATION=Release
make integration-c CONFIGURATION=Release TEST_ARGS="--filter-class Fully.Qualified.TestClassName"
make lint CONFIGURATION=Release
```

Never run `make tests` or `make integration` on the host. Integration tests
require Podman or Docker through `make integration-c`. Do not use `dotnet test
--filter`; this repository uses Microsoft.Testing.Platform.

For a bug fix, first reproduce the defect at its smallest tier, then verify the
same scenario no longer fails. For a permanent behavior change, run the affected
unit or integration tests and `make lint CONFIGURATION=Release`.
