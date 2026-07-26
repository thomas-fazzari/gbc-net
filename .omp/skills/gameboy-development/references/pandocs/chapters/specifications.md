# Specifications

| | Game Boy (DMG) | Game Boy Pocket (MGB) | Super Game Boy (SGB) | Game Boy Color (CGB) |
| --- | --- | --- | --- | --- |
| CPU | 8-bit 8080-like Sharp CPU (speculated to be a SM83 core) | | | |
| Master Clock | 4.194304 MHz[^dmg_clk] | | Depends on revision[^sgb_clk] | Up to 8.388608 MHz |
| System Clock | 1/4 the frequency of Master Clock | | | |
| Work RAM | 8 KiB | | | 32 KiB[^compat] (4 + 7 × 4 KiB) |
| Video RAM | 8 KiB | | | 16 KiB[^compat] (2 × 8 KiB) |
| Screen | LCD 4.7 × 4.3 cm | LCD 4.8 × 4.4 cm | CRT TV | TFT 4.4 × 4 cm |
| Resolution | 160 × 144 | | 160 × 144 within 256 × 224 border | 160 × 144 |
| OBJ ("sprites") | 8 × 8 or 8 × 16 ; max 40 per screen, 10 per line | | | |
| Palettes | BG: 1 × 4, OBJ: 2 × 3 | | BG/OBJ: 1 + 4 × 3, border: 4 × 15 | BG: 8 × 4, OBJ: 8 × 3[^compat] |
| Colors | 4 shades of green | 4 shades of gray | 32768 colors (15-bit RGB) | |
| Horizontal sync | 9.198 KHz | | Complicated[^sgb_vid] | 9.198 KHz |
| Vertical sync | 59.73 Hz | | Complicated[^sgb_vid] | 59.73 Hz |
| Sound | 4 channels with stereo output | | 4 GB channels + SNES audio | 4 channels with stereo output |
| Power | DC 6V, 0.7 W | DC 3V, 0.7 W | Powered by SNES | DC 3V, 0.6 W |

---

[^dmg_clk]: Real DMG units tend to run about 50-70 PPM slow. Accuracy of other models is unknown. [See this page](https://github.com/jkotlinski/gbchrono) for more details.

[^sgb_clk]: SGB1 cartridges derive the GB CPU clock from the SNES’ clock, [yielding a clock speed a bit higher](sgb-description.md#sgb-clock-speed), which differs slightly between NTSC and PAL systems.
    SGB2 instead uses a clock internal to the cartridge, and so has the same speed as the handhelds.

[^compat]: The same value as on DMG is used in compatibility mode.

[^sgb_vid]: The SGB runs two consoles: a Game Boy within the SGB cartridge, and the SNES itself.
    The GB LCD output is captured and displayed by the SNES, but the two consoles’ frame rates don’t quite sync up, leading to duplicated and/or dropped frames.
    The GB side of the vertical sync depends on the CPU clock[^sgb_clk], with the same ratio as the handhelds.
