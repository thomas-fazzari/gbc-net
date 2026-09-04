# cgb-acid2

cgb-acid2 is a test for developers of Game Boy Color emulators to verify their
emulation of the Game Boy Color's Pixel Processing Unit (PPU).

Source: https://github.com/mattcurrie/cgb-acid2
ROM release: https://github.com/mattcurrie/cgb-acid2/releases/download/v1.1/cgb-acid2.gbc

## Golden frame

`cgb-acid2.rgb555le.bin` is a 160x144 raw frame, two bytes per pixel, little-endian RGB555.
It was generated from SameBoy 1.0.3 with color correction disabled, then converted from
SameBoy's 32-bit BMP output back to RGB555 using the cgb-acid2 reference-image formula:
`component8 = (component5 << 3) | (component5 >> 2)`.

The generated SameBoy BMP is kept as `reference-cgb.bmp` for visual inspection only.
