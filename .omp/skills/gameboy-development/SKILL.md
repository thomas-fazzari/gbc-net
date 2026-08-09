---
name: gameboy-development
description: Source-first Game Boy development encyclopedia based on bundled Pan Docs chapters. Use when designing, implementing, debugging, testing, or reviewing Game Boy / Game Boy Color / Super Game Boy emulators, ROM tooling, cartridge mappers, CPU SM83 execution, memory bus behavior, PPU/LCD rendering, APU audio, timers, interrupts, DMA/HDMA, joypad/serial/link cable, boot/power-up state, CGB compatibility, SGB commands, or hardware-accuracy decisions.
---

# Game Boy Development

Local hardware encyclopedia. Concrete behavior lives in the bundled Pan Docs,
not this file.

## Ground Rules

* Read the matching Pan Docs chapter before changing hardware behavior.
* State model scope explicitly: DMG-only, CGB-capable, CGB mode, CGB in DMG compatibility mode, SGB, or AGB behavior.
* Preserve uncertainty that Pan Docs marks as unknown, model-dependent, or not fully researched.

## Bundled Knowledge

* `references/pandocs/CHAPTERS.md` - chapter index.
* `references/pandocs/chapters/*.md` - clean top-level chapters.
* `references/lookup-map.md` - which chapters to read for each task.

## Required Workflow

1. Use `references/lookup-map.md` to choose the smallest relevant chapter set.
2. Read known chapters directly; use `grep` over `references/pandocs` for ad-hoc queries.
3. Implement documented masks, blocked access, side effects, timing, and model differences.
4. When adding tests, cite the chapter that defines the expected behavior.
