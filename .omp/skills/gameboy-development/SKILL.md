---
name: gameboy-development
description: Source-first Game Boy development encyclopedia based on bundled Pan Docs chapters. Use when designing, implementing, debugging, testing, or reviewing Game Boy / Game Boy Color / Super Game Boy emulators, ROM tooling, cartridge mappers, CPU SM83 execution, memory bus behavior, PPU/LCD rendering, APU audio, timers, interrupts, DMA/HDMA, joypad/serial/link cable, boot/power-up state, CGB compatibility, SGB commands, or hardware-accuracy decisions.
---

# Game Boy Development

Use this skill as a local hardware encyclopedia. It is not language-specific.

## Ground Rules

- Treat bundled Pan Docs as source of truth before writing hardware behavior from memory.
- Search or open the relevant chapter before implementing or correcting CPU, bus, PPU, APU, timer, interrupt, DMA, MBC, CGB, or SGB behavior.
- Prefer exact hardware terms: DMG, MGB, SGB, SGB2, CGB, AGB, SM83, M-cycle, T-cycle, dot, PPU mode, OAM, VRAM, WRAM, HRAM, IF, IE, IME.
- State model scope explicitly: DMG-only, CGB-capable, CGB mode, CGB in DMG compatibility mode, SGB behavior, or AGB behavior.
- Do not treat this `SKILL.md` as hardware truth; load the matching Pan Docs chapter for concrete behavior.
- If Pan Docs marks behavior as unknown, model-dependent, or not fully researched, keep that uncertainty visible in the implementation notes.

## Bundled Knowledge

Primary clean resource:

- `references/pandocs/CHAPTERS.md` - chapter index.
- `references/pandocs/chapters/*.md` - clean top-level chapters.

Navigation references:

- `references/lookup-map.md` - which chapters to read for each task.

Scripts:

- `scripts/search-pandocs.sh <query>` - ripgrep Pan Docs.
- `scripts/list-pandocs-chapters.sh` - list clean chapter files.
- `scripts/show-pandocs-chapter.sh <slug-or-path>` - print one chapter.

## Required Workflow

1. Classify the task:
   - CPU/instruction/flag behavior
   - memory bus/MMU/MBC/cartridge/header
   - PPU/LCD/VRAM/OAM/pixel FIFO/rendering
   - APU/audio/registers/mixer
   - timers/interrupts/HALT/STOP/DMA
   - CGB/SGB/accessory behavior
   - testing/debugging/architecture
2. Use `references/lookup-map.md` to choose chapters.
3. Load only the needed chapters, or search with `scripts/search-pandocs.sh`.
4. Implement against chapter text, including documented read/write masks, blocked memory access, side effects, timing, and model differences.
5. When adding tests, cite the chapter that defines the expected behavior.

## Search Patterns

Use these as first-pass queries:

```bash
scripts/search-pandocs.sh "FF40|LCDC|LCD enable"
scripts/search-pandocs.sh "TIMA|TMA|TAC|DIV"
scripts/search-pandocs.sh "IME|IE|IF|halt bug"
scripts/search-pandocs.sh "HDMA|VBK|SVBK|KEY1"
scripts/search-pandocs.sh "MBC1|RAM Enable|ROM Bank Number"
scripts/search-pandocs.sh "NR52|DIV-APU|DAC|wave RAM"
```
