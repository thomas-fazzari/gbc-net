# Lookup Map

Use this map before implementing hardware behavior. Load only relevant chapters unless broad context is needed.

## Core Orientation

- System specs: `pandocs/chapters/specifications.md`
- Memory map: `pandocs/chapters/memory-map.md`
- Hardware register summary: `pandocs/chapters/hardware-registers.md`
- Cartridge header: `pandocs/chapters/the-cartridge-header.md`

## CPU

- Registers and flags: `pandocs/chapters/cpu-registers-and-flags.md`
- Opcode grouping and invalid opcodes: `pandocs/chapters/cpu-instruction-set.md`
- Z80 differences: `pandocs/chapters/cpu-comparison-with-z80.md`
- Interrupts: `pandocs/chapters/interrupts.md`
- Interrupt sources: `pandocs/chapters/interrupt-sources.md`
- HALT and halt bug: `pandocs/chapters/halt.md`
- STOP and power: `pandocs/chapters/reducing-power-consumption.md`
- Power-up/boot state: `pandocs/chapters/power-up-sequence.md`

## Bus, Memory, DMA

- Address ranges, echo RAM, unusable memory: `pandocs/chapters/memory-map.md`
- VRAM/OAM blocking: `pandocs/chapters/accessing-vram-and-oam.md`
- OAM layout: `pandocs/chapters/object-attribute-memory-oam.md`
- OAM DMA: `pandocs/chapters/oam-dma-transfer.md`
- CGB HDMA: `pandocs/chapters/cgb-registers.md`
- External connectors: `pandocs/chapters/external-connectors.md`

## PPU / LCD

- Graphics overview: `pandocs/chapters/graphics-overview.md`
- Tile data: `pandocs/chapters/vram-tile-data.md`
- Tile maps and CGB attributes: `pandocs/chapters/vram-tile-maps.md`
- OAM/object attributes: `pandocs/chapters/object-attribute-memory-oam.md`
- LCDC: `pandocs/chapters/lcd-control.md`
- STAT, LY, LYC: `pandocs/chapters/lcd-status-registers.md`
- Scroll registers: `pandocs/chapters/viewport-position-scrolling.md`
- Window quirks: `pandocs/chapters/window-behavior.md`
- Palettes: `pandocs/chapters/palettes.md`
- Rendering timing and modes: `pandocs/chapters/rendering-overview.md`
- Pixel FIFO: `pandocs/chapters/pixel-fifo.md`
- OAM corruption bug: `pandocs/chapters/oam-corruption-bug.md`

## APU / Audio

- Audio concepts: `pandocs/chapters/audio-overview.md`
- Audio registers: `pandocs/chapters/audio-registers.md`
- Detailed channel/mixer quirks: `pandocs/chapters/audio-details.md`

## Timers, Input, Serial

- DIV/TIMA/TMA/TAC: `pandocs/chapters/timer-and-divider-registers.md`
- Timer edge/overflow quirks: `pandocs/chapters/timer-obscure-behaviour.md`
- Joypad register: `pandocs/chapters/joypad-input.md`
- Serial/link cable: `pandocs/chapters/serial-data-transfer-link-cable.md`

## CGB

- CGB register chapter: `pandocs/chapters/cgb-registers.md`
- CGB palettes: `pandocs/chapters/palettes.md`
- CGB tile attributes: `pandocs/chapters/vram-tile-maps.md`
- CGB speed switch: `pandocs/chapters/cgb-registers.md`
- CGB approval/compatibility: `pandocs/chapters/gbc-approval-process.md`
- IR port: `pandocs/chapters/gbc-infrared-communication.md`

## Cartridges / MBC

- MBC overview: `pandocs/chapters/mbcs.md`
- No MBC: `pandocs/chapters/no-mbc.md`
- MBC1: `pandocs/chapters/mbc1.md`
- MBC2: `pandocs/chapters/mbc2.md`
- MBC3 and RTC: `pandocs/chapters/mbc3.md`
- MBC5 and rumble: `pandocs/chapters/mbc5.md`
- MBC6, MBC7, MMM01, M161, HuC1, HuC-3, other: matching `pandocs/chapters/*.md`

## SGB / Accessories

- SGB overview: `pandocs/chapters/sgb-description.md`
- SGB unlock/detect: `pandocs/chapters/unlocking-and-detecting-sgb-functions.md`
- SGB command packets: `pandocs/chapters/command-packet-transfers.md`
- SGB VRAM transfers: `pandocs/chapters/vram-transfers.md`
- SGB command summary and command groups: matching `pandocs/chapters/*commands.md`
- Printer, Camera, 4-player adapter, cheats: matching accessory chapters.
