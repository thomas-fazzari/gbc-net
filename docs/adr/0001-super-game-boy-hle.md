# ADR-0001: Emulate the Super Game Boy SNES side with HLE

- Status: Accepted
- Date: 2026-07-11

## Context

The Super Game Boy combines Game Boy hardware with SNES-side firmware and hardware. SGB-enhanced games communicate through JOYP command packets to request palettes, attribute maps, VRAM transfers, borders, masking, multiplayer input multiplexing, SNES sound, firmware patching, and other SNES-side effects.

GBC.Net is a Game Boy emulator, not a SNES emulator. Implementing or embedding a complete SNES CPU, PPU, APU, memory map, and SGB firmware environment would substantially increase scope and maintenance cost. The visual and gameplay features used by ordinary SGB-enhanced games can instead be reproduced directly from their commands, as established by HLE implementations such as SameBoy.

## Decision

GBC.Net will emulate the Game Boy side as dedicated SGB1 hardware and emulate the SNES side through high-level emulation in `SgbController`.

The HLE path will:

- receive SGB command packets through JOYP;
- implement palette and attribute-map commands;
- capture and decode VRAM transfers;
- render custom 256×224 borders around the Game Boy image;
- implement masking and 2/4-player JOYP multiplexing;
- preserve SGB1 clock, APU, boot ROM, and post-boot behavior;
- ignore unsupported SNES-side commands without pretending their effects occurred.

GBC.Net will not add a SNES execution core solely for SGB support. SNES-side sound, firmware code execution, menu/test controls, OBJ overlays, palette-priority behavior, and SGB2 remain outside the current scope. `DATA_SND` is recognized as an intentional no-op because HLE does not execute SNES firmware patches.

The supported boundary is guarded by:

- `tests/GbcNet.Tests/Sgb/SgbControllerTests.cs`;
- `tests/GbcNet.Tests/GameBoyTests.cs`;
- `tests/GbcNet.Tests/RomTesting/Mooneye/MooneyeSgbRomTests.cs`.

## Consequences

### Positive

- SGB palettes, attribute maps, borders, masking, transfers, and multiplayer protocol work without a second console core.
- The implementation reuses the existing DMG CPU, PPU, APU, memory, and timing infrastructure.
- Scope, runtime cost, and maintenance remain appropriate for a Game Boy emulator.
- Unsupported commands degrade predictably instead of destabilizing emulation.

### Negative

- Games depending materially on SNES-side sound, custom SNES code, OBJ overlays, palette priority, or SGB2-specific behavior are incomplete.
- HLE can reproduce known command effects but not arbitrary behavior of the original SNES firmware.
- New compatibility findings may require additional command-level implementations.

## Alternatives considered

### Emulate a complete SNES and run the SGB firmware

Rejected because it would turn SGB support into a second emulator core with disproportionate complexity.

### Integrate an external SNES core

Rejected because the dependency, synchronization, firmware, audio/video composition, and maintenance costs are not justified by current compatibility needs.

### Treat SGB cartridges as ordinary Game Boy games

Rejected because palettes, borders, masking, transfers, and multiplayer identification are core advertised SGB behavior and are already supported effectively through HLE.

## Revisit criteria

Reconsider this decision only if compatibility evidence shows that important games require effects that cannot be maintained reasonably at the command level, or if full SNES integration becomes an explicit product goal.
