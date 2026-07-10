# Window behavior

## FF4A–FF4B — WY, WX: Window Y position, X position plus 7

These two registers specify the on-screen coordinates of [the Window](#window-1)’s top-left pixel.

The Window is visible (if enabled) when `WX` and `WY` are in the range [0; 166] and [0; 143] respectively.
Values `WX`=7, `WY`=0 place the Window at the top left of the screen, completely covering the background.

## Window mid-frame behavior

While the Window should work as just mentioned, writing to `WX`, `WY` etc. mid-frame displays more articulated behavior.
There are several aspects of the window that respond differently to various mid-frame interactions; the **tl;dr** is this:

- For the least glitchy results, only write to `WX`, `WY`, and `LCDC` during VBlank (possibly in your [VBlank interrupt handler](#int-40--vblank-interrupt)); if mid-frame writes are required, prefer writing during HBlank.
- If intending to hide the Window for part of the screen (e.g. to have a status bar at the *top* of the screen instead of the bottom), hide it by setting `WX` to a high value rather than writing to `LCDC`.

### Window rendering criteria

The PPU keeps track of a “**Y condition**” throughout a frame.

- On each VBlank, the *Y condition* is cleared (becomes false).
- At the beginning of each scanline, if the value of `WY` is equal to [`LY`](#ff44--ly-lcd-y-coordinate-read-only), the *Y condition* becomes true (and remains so for subsequent scanlines).

Note

On GBC, clearing the [Window enable bit](#lcdc5--window-enable) in `LCDC` resets the *Y condition*; `WY` must be set to `LY` or greater for the Window to display again in the current frame.

Additionally, the PPU maintains a counter, initialized to 0 at the beginning of each scanline.
The counter is incremented for each pixel rendered; however, it also increments 7 times before the first pixel is actually rendered (this covers pixels discarded during the initial “fine scroll” adjustment).

When this counter is equal to `WX`, if the *Y condition* is true and the [Window enable bit](#lcdc5--window-enable) is set in `LCDC`, background rendering is reset, beginning anew from the active row of the Window’s tilemap.
The coordinate of the active Window row is then incremented.

- This process can happen more than once per scanline, making the Window’s “tilemap Y coordinate” increase more than once in the scanline.
  (This is demonstrated by the TODO test ROM.)

  However, this requires “disabling” the Window by briefly clearing its enable bit from `LCDC` first.
- If this process doesn’t happen, the Window’s “tilemap Y coordinate” does not increase; so, if the Window is hidden (by any means) on a given scanline, the row of pixels rendered the next time it’s shown will be the same as if it had not been hidden in the first place, producing a sort of vertical striped stretching:
- If `WX` is equal to 0, the Window is switched to before the initial “fine scroll” adjustment, causing it to be shifted left by SCX % 8 pixels.
- On monochrome systems, `WX` = 166 (which would normally show a single Window pixel, along the right edge of the screen) exhibits a bug: the Window spans the entire screen, but offset vertically by one scanline.
- On monochrome systems, if the Window is disabled via `LCDC`, but the other conditions are met *and* it would have started rendering exactly on a BG tile boundary, then where it would have started rendering, a single pixel with ID 0 (i.e. drawn as the first entry in [the BG palette](#ff47--bgp-non-cgb-mode-only-bg-palette-data)) is inserted; this offsets the remainder of the scanline.[1](#footnote-star_trek)

---

1. This was discovered as affecting the game *Star Trek 25th anniversary*; more information and a test ROM are available [in this thread](https://github.com/LIJI32/SameBoy/issues/278#issuecomment-1189712129). [↩](#fr-star_trek-1)
