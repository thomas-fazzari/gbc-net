# Specifications

|  | Game Boy (DMG) | Game Boy Pocket (MGB) | Super Game Boy (SGB) | Game Boy Color (CGB) |
| --- | --- | --- | --- | --- |
| CPU | 8-bit 8080-like Sharp CPU (speculated to be a SM83 core) | | | |
| Master Clock | 4.194304 MHz[1](#dmg_clk) | | Depends on revision[2](#sgb_clk) | Up to 8.388608 MHz |
| System Clock | 1/4 the frequency of Master Clock | | | |
| Work RAM | 8 KiB | | | 32 KiB[3](#compat) (4 + 7 × 4 KiB) |
| Video RAM | 8 KiB | | | 16 KiB[3](#compat) (2 × 8 KiB) |
| Screen | LCD 4.7 × 4.3 cm | LCD 4.8 × 4.4 cm | CRT TV | TFT 4.4 × 4 cm |
| Resolution | 160 × 144 | | 160 × 144 within 256 × 224 border | 160 × 144 |
| OBJ ("sprites") | 8 × 8 or 8 × 16 ; max 40 per screen, 10 per line | | | |
| Palettes | BG: 1 × 4, OBJ: 2 × 3 | | BG/OBJ: 1 + 4 × 3, border: 4 × 15 | BG: 8 × 4, OBJ: 8 × 3[3](#compat) |
| Colors | 4 shades of green | 4 shades of gray | 32768 colors (15-bit RGB) | |
| Horizontal sync | 9.198 KHz | | Complicated[4](#sgb_vid) | 9.198 KHz |
| Vertical sync | 59.73 Hz | | Complicated[4](#sgb_vid) | 59.73 Hz |
| Sound | 4 channels with stereo output | | 4 GB channels + SNES audio | 4 channels with stereo output |
| Power | DC 6V, 0.7 W | DC 3V, 0.7 W | Powered by SNES | DC 3V, 0.6 W |

---

1. SGB1 cartridges derive the GB CPU clock from the SNES’ clock, [yielding a clock speed a bit higher](#sgb-clock-speed), which differs slightly between NTSC and PAL systems.
   SGB2 instead uses a clock internal to the cartridge, and so has the same speed as the handhelds. [↩](#fr-sgb_clk-1)
