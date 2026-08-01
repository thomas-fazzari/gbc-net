# GBC.Net

## Goal

Building an accurate Game Boy, Game Boy Color, and Super Game Boy emulator in C# on .NET 10.

## Repository

* `src/GbcNet.Core`: emulator core with no dependencies. Owns SM83 execution, memory and cartridges, clocks, interrupts, DMA, PPU, APU, joypad, serial, cheats, and SGB behavior.
* `src/GbcNet.App`: Avalonia desktop app and composition root. Owns UI, emulation sessions and pacing, SQLite persistence, configuration, files, input, audio and rendering adapters, and logging.
* `tests/GbcNet.Tests.Unit`: hardware behavior, emulator state, and ROM compatibility tests.
* `tests/GbcNet.Tests.Integration`: filesystem, database, and application integration tests.

## Architecture

* Dependency direction is `GbcNet.App` to `GbcNet.Core`; Core does not depend on application or infrastructure packages.
* Keep hardware state, timing, and emulation behavior in Core. Keep UI, operating-system I/O, persistence, and external adapters in App.
* Keep Core and hardware hot paths allocation-conscious and free of logging.
* Guard hardware fixes at the closest behavior or compatibility boundary, using existing unit and ROM-test patterns.

## Decisions

Versioned ADRs under `docs/adr/` are the source of truth.

* `0001-super-game-boy-hle.md`: emulate SGB1 hardware on the Game Boy side and SNES-side effects through HLE in `SgbController`; do not add a SNES core solely for SGB support.
* `0002-logging.md`: application code uses Microsoft logging with source-generated messages and a Serilog file sink; Core and hardware hot paths remain logging-free.

## Workflow

Use Makefile entry points instead of duplicating their underlying commands.

* `make` runs the desktop app.
* `make lint CONFIGURATION=Release` validates Markdown, formatting, analyzers, and the solution build.
* `make unit CONFIGURATION=Release` runs unit tests.
* `make integration-c CONFIGURATION=Release` runs integration tests in Podman or Docker.
