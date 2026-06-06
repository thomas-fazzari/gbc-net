# AGENTS.md

## Defaults

- Use `$caveman` for every response. Stop only when the user says `normal mode` or `stop caveman`.
- Use `$grill-me` before non-trivial feature, refactor, or architecture work unless the user explicitly skips it.
- Use `$gameboy-development` before changing Game Boy hardware behavior, emulator timing, CPU, PPU, APU, timers, interrupts, DMA, MBC, CGB, or SGB logic.

## Code style

- Avoid unnecessary abstractions, indirections, wrappers/helper methods and intermediate variables: inline simple expressions and one-off construction unless naming significantly improves correctness, readability, reuse, or test diagnostics.

## Git

- Do not stage, unstage, commit, reset, restore, checkout, branch, push, or otherwise mutate git state unless the user explicitly asks for that exact git action.
- For git output requests, run only the requested read-only command and report the important lines.
