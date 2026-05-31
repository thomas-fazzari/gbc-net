# AGENTS.md

## Defaults

- Use `$caveman` for every response. Stop only when the user says `normal mode` or `stop caveman`.
- Use `$grill-me` before non-trivial feature, refactor, or architecture work unless the user explicitly skips it.
- Use Context7 for current external library/framework/API/CLI docs before changing code that depends on them.
- Use `$gameboy-development` before changing Game Boy hardware behavior, emulator timing, CPU, PPU, APU, timers, interrupts, DMA, MBC, CGB, or SGB logic.

## Operating Rules

- Inspect existing code before choosing a pattern.
- Keep the smallest correct change and preserve current boundaries.
- Ask one focused question when requirements or architecture direction are ambiguous.
- Touch only files needed for the task; do not silently fix unrelated issues.
- Remove code made obsolete by the change.
- Do not add pass-through wrappers for methods, properties, or delegates when callers can use the underlying object directly. Keep a wrapper only when it enforces an invariant, protects an API boundary, names a real domain concept, or removes meaningful duplication.

## Commands

Use `make` as source of truth.

```bash
make install
make lint
make test
make fix
```

## Validation

Report exact commands and results. If validation cannot run, say why.

## Git

- Do not stage, unstage, commit, reset, restore, checkout, branch, push, or otherwise mutate git state unless the user explicitly asks for that exact git action.
- For git output requests, run only the requested read-only command and report the important lines.
