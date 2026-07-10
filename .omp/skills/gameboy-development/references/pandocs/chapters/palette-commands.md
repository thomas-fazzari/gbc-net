# Palette Commands

## SGB Command $00 — PAL01

Transmit color data for SGB palette 0, color 0-3, and for SGB palette 1,
color 1-3 (without separate color 0).

| 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Header | Pals 0 & 1 color #0 | | Pal 0 color #1 | | Pal 0 color #2 | | Pal 0 color #3 | | Pal 1 color #1 | | Pal 1 color #2 | | Pal 1 color #3 | |  |

The **header** byte is `$00 << 3 | $01` = $01.

## SGB Command $01 — PAL23

Same as `PAL01` above, but for Palettes 2 and 3 respectively.
The **header** byte is thus $09.

## SGB Command $02 — PAL03

Same as `PAL01` above, but for Palettes 0 and 3 respectively.
The **header** byte is thus $11.

## SGB Command $03 — PAL12

Same as `PAL01` above, but for Palettes 1 and 2 respectively.
The **header** byte is thus $19.

## SGB Command $0A — PAL\_SET

Used to copy pre-defined palette data from SGB system color palettes to
actual SNES palettes.

Before using this feature, System Palette data should be initialized by
[`PAL_TRN`](#sgb-command-0b--pal_trn) command, and (when used) Attribute File (ATF) data should be
initialized by [`ATTR_TRN`](#sgb-command-15--attr_trn).

| 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Header | Palette #0's ID | | Palette #1's ID | | Palette #2's ID | | Palette #3's ID | | Flags |  | | | | | |

The **header** byte is `$0A << 3 | $01` = $51.
All **palette ID**s are little-endian.

|  | 7 | 6 | 5 | 4 | 3 | 2 | 1 | 0 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| **Flags** | Apply ATF | Cancel MASK\_EN | ATF number | | | | | |

- **Apply ATF**: If and only if this is set, then the ATF whose ID is specified by bits 0–5 is applied as if by [`ATTR_SET`](#sgb-command-16--attr_set).
- **Cancel `MASK_EN`**: If this bit is set, then any current [`MASK_EN`](#sgb-command-17--mask_en) “screen freeze” is cancelled.
- **ATF number**: Index of the ATF to transfer. Values greater than $2C are invalid.

## SGB Command $0B — PAL\_TRN

Used to initialize SGB system color palettes in SNES RAM. System color
palette memory contains 512 pre-defined palettes, these palettes do not
directly affect the display, however, the `PAL_SET` command may be later
used to transfer four of these “logical” palettes to actual visible
“physical” SGB palettes. Also, the `OBJ_TRN` feature will use groups
of 4 System Color Palettes (4\*4 colors) for SNES OBJ palettes (16
colors).

| 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Header |  | | | | | | | | | | | | | | |

The **header** byte must be $59.

The palette data is sent by [VRAM Transfer](#vram-transfers).

|  | …0 | …1 | …2 | …3 | …4 | …5 | …6 | …7 | …8 | …9 | …A | …B | …C | …D | …E | …F |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| $800… | Pal #0 color #0 | | Pal #0 color #1 | | Pal #0 color #2 | | Pal #0 color #3 | | Pal #1 color #0 | | Pal #1 color #1 | | Pal #1 color #2 | | Pal #1 color #3 | |
| $801… | Pal #2 color #0 | | Pal #2 color #1 | | Pal #2 color #2 | | Pal #2 color #3 | | Pal #3 color #0 | | Pal #3 color #1 | | Pal #3 color #2 | | Pal #3 color #3 | |
| … | … | | | | | | | | | | | | | | | |
| $8FF… | Pal #510 color #0 | | Pal #510 color #1 | | Pal #510 color #2 | | Pal #510 color #3 | | Pal #511 color #0 | | Pal #511 color #1 | | Pal #511 color #2 | | Pal #511 color #3 | |

The data is stored at 3000-3FFF in SNES memory.

## SGB Command $19 — PAL\_PRI

If the player overrides the active palette set (a pre-defined or the custom one), it stays in effect until the smiley face is selected again, or the player presses the X button on their SNES controller.

However, if `PAL_PRI` is enabled, then changing the palette set (via any `PAL_*` command besides `PAL_TRN`) will switch back to the game’s newly-modified palette set, if it wasn’t already active.

*Donkey Kong* (1994) is one known game that appears to use this.

| 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Header | Priority |  | | | | | | | | | | | | | |

The **header** must be $C9.

Bit 0 of the **priority** byte enables (`1`) or disables (`0`) `PAL_PRI`.
