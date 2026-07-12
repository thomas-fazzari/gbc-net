# AGENTS.md

## Defaults

- Use `gameboy-development` before changing Game Boy hardware behavior, emulator timing, CPU, PPU, APU, timers, interrupts, DMA, MBC, CGB, or SGB logic.
- Respond tersely in the user's language. Drop filler, pleasantries, hedging, and unnecessary words; fragments are fine. Preserve technical precision, code, commands, exact errors, and required detail.
- Do not narrate tool calls, use decorative tables, or dump long logs unless asked. Use full sentences only when compression could make security warnings, destructive actions, or ordered steps ambiguous.

## Code style

- Avoid unnecessary abstractions, indirections, wrappers/helper methods and intermediate variables: inline simple expressions and one-off construction unless naming significantly improves correctness, readability, reuse, or test diagnostics.
- Avoid unnecessary fallbacks and backward compatibility layers: write code for the current, explicit requirements. Do not support legacy behaviors, deprecated APIs, or "just in case" edge cases unless explicitly requested.

## Architecture decision records

- Versioned ADRs under `docs/adr/` are the source of truth. Codebase Memory is a discovery cache; keep its ADR content synchronized when a versioned ADR changes.
- Before changing an area listed below, read its referenced ADR:
  - SGB behavior or scope: `docs/adr/0001-super-game-boy-hle.md`

## Tests

- With xUnit v3/Microsoft Testing Platform, filter a specific test class through the test executable:
  `dotnet run --project tests/GbcNet.Tests/GbcNet.Tests.csproj -- --filter-class Fully.Qualified.TestClassName`.
- Do not use `dotnet test --filter`; this project uses the MTP runner and that filter option is not supported here.

## Git

- Do not stage, unstage, commit, reset, restore, checkout, branch, push, or otherwise mutate git state unless the user explicitly asks for that exact git action.
- For git output requests, run only the requested read-only command and report the important lines.
