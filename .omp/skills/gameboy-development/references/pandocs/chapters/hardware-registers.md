# Hardware Registers

| Address | Name | Description | Readable / Writable | Models |
| --- | --- | --- | --- | --- |
| $FF00 | [P1/JOYP](#ff00--p1joyp-joypad) | Joypad | Mixed | All |
| $FF01 | [SB](#ff01--sb-serial-transfer-data) | Serial transfer data | R/W | All |
| $FF02 | [SC](#ff02--sc-serial-transfer-control) | Serial transfer control | R/W | Mixed |
| $FF04 | [DIV](#ff04--div-divider-register) | Divider register | R/W | All |
| $FF05 | [TIMA](#ff05--tima-timer-counter) | Timer counter | R/W | All |
| $FF06 | [TMA](#ff06--tma-timer-modulo) | Timer modulo | R/W | All |
| $FF07 | [TAC](#ff07--tac-timer-control) | Timer control | R/W | All |
| $FF0F | [IF](#ff0f--if-interrupt-flag) | Interrupt flag | R/W | All |
| $FF10 | [NR10](#ff10--nr10-channel-1-sweep) | Sound channel 1 sweep | R/W | All |
| $FF11 | [NR11](#ff11--nr11-channel-1-length-timer--duty-cycle) | Sound channel 1 length timer & duty cycle | Mixed | All |
| $FF12 | [NR12](#ff12--nr12-channel-1-volume--envelope) | Sound channel 1 volume & envelope | R/W | All |
| $FF13 | [NR13](#ff13--nr13-channel-1-period-low-write-only) | Sound channel 1 period low | W | All |
| $FF14 | [NR14](#ff14--nr14-channel-1-period-high--control) | Sound channel 1 period high & control | Mixed | All |
| $FF16 | [NR21](#sound-channel-2--pulse) | Sound channel 2 length timer & duty cycle | Mixed | All |
| $FF17 | [NR22](#sound-channel-2--pulse) | Sound channel 2 volume & envelope | R/W | All |
| $FF18 | [NR23](#sound-channel-2--pulse) | Sound channel 2 period low | W | All |
| $FF19 | [NR24](#sound-channel-2--pulse) | Sound channel 2 period high & control | Mixed | All |
| $FF1A | [NR30](#ff1a--nr30-channel-3-dac-enable) | Sound channel 3 DAC enable | R/W | All |
| $FF1B | [NR31](#ff1b--nr31-channel-3-length-timer-write-only) | Sound channel 3 length timer | W | All |
| $FF1C | [NR32](#ff1c--nr32-channel-3-output-level) | Sound channel 3 output level | R/W | All |
| $FF1D | [NR33](#ff1d--nr33-channel-3-period-low-write-only) | Sound channel 3 period low | W | All |
| $FF1E | [NR34](#ff1e--nr34-channel-3-period-high--control) | Sound channel 3 period high & control | Mixed | All |
| $FF20 | [NR41](#ff20--nr41-channel-4-length-timer-write-only) | Sound channel 4 length timer | W | All |
| $FF21 | [NR42](#ff21--nr42-channel-4-volume--envelope) | Sound channel 4 volume & envelope | R/W | All |
| $FF22 | [NR43](#ff22--nr43-channel-4-frequency--randomness) | Sound channel 4 frequency & randomness | R/W | All |
| $FF23 | [NR44](#ff23--nr44-channel-4-control) | Sound channel 4 control | Mixed | All |
| $FF24 | [NR50](#ff24--nr50-master-volume--vin-panning) | Master volume & VIN panning | R/W | All |
| $FF25 | [NR51](#ff25--nr51-sound-panning) | Sound panning | R/W | All |
| $FF26 | [NR52](#ff26--nr52-audio-master-control) | Sound on/off | Mixed | All |
| $FF30-FF3F | [Wave RAM](#ff30ff3f--wave-pattern-ram) | Storage for one of the sound channels’ waveform | R/W | All |
| $FF40 | [LCDC](#ff40--lcdc-lcd-control) | LCD control | R/W | All |
| $FF41 | [STAT](#ff41--stat-lcd-status) | LCD status | Mixed | All |
| $FF42 | [SCY](#ff42ff43--scy-scx-background-viewport-y-position-x-position) | Viewport Y position | R/W | All |
| $FF43 | [SCX](#ff42ff43--scy-scx-background-viewport-y-position-x-position) | Viewport X position | R/W | All |
| $FF44 | [LY](#ff44--ly-lcd-y-coordinate-read-only) | LCD Y coordinate | R | All |
| $FF45 | [LYC](#ff45--lyc-ly-compare) | LY compare | R/W | All |
| $FF46 | [DMA](#ff46--dma-oam-dma-source-address--start) | OAM DMA source address & start | R/W | All |
| $FF47 | [BGP](#ff47--bgp-non-cgb-mode-only-bg-palette-data) | BG palette data | R/W | DMG |
| $FF48 | [OBP0](#ff48ff49--obp0-obp1-non-cgb-mode-only-obj-palette-0-1-data) | OBJ palette 0 data | R/W | DMG |
| $FF49 | [OBP1](#ff48ff49--obp0-obp1-non-cgb-mode-only-obj-palette-0-1-data) | OBJ palette 1 data | R/W | DMG |
| $FF4A | [WY](#ff4aff4b--wy-wx-window-y-position-x-position-plus-7) | Window Y position | R/W | All |
| $FF4B | [WX](#ff4aff4b--wy-wx-window-y-position-x-position-plus-7) | Window X position plus 7 | R/W | All |
| $FF4C | [KEY0/SYS](#ff4c--key0sys-cgb-mode-only-cpu-mode-select) | CPU mode select | Mixed | CGB |
| $FF4D | [KEY1/SPD](#ff4d--key1spd-cgb-mode-only-prepare-speed-switch) | Prepare speed switch | Mixed | CGB |
| $FF4F | [VBK](#ff4f--vbk-cgb-mode-only-vram-bank) | VRAM bank | R/W | CGB |
| $FF50 | [BANK](#power-up-sequence) | Boot ROM mapping control | W | All |
| $FF51 | [HDMA1](#ff51ff52--hdma1-hdma2-cgb-mode-only-vram-dma-source-high-low-write-only) | VRAM DMA source high | W | CGB |
| $FF52 | [HDMA2](#ff51ff52--hdma1-hdma2-cgb-mode-only-vram-dma-source-high-low-write-only) | VRAM DMA source low | W | CGB |
| $FF53 | [HDMA3](#ff53ff54--hdma3-hdma4-cgb-mode-only-vram-dma-destination-high-low-write-only) | VRAM DMA destination high | W | CGB |
| $FF54 | [HDMA4](#ff53ff54--hdma3-hdma4-cgb-mode-only-vram-dma-destination-high-low-write-only) | VRAM DMA destination low | W | CGB |
| $FF55 | [HDMA5](#ff55--hdma5-cgb-mode-only-vram-dma-lengthmodestart) | VRAM DMA length/mode/start | R/W | CGB |
| $FF56 | [RP](#ff56--rp-cgb-mode-only-infrared-communications-port) | Infrared communications port | Mixed | CGB |
| $FF68 | [BCPS/BGPI](#ff68--bcpsbgpi-cgb-mode-only-background-color-palette-specification--background-palette-index) | Background color palette specification / Background palette index | R/W | CGB |
| $FF69 | [BCPD/BGPD](#ff69--bcpdbgpd-cgb-mode-only-background-color-palette-data--background-palette-data) | Background color palette data / Background palette data | R/W | CGB |
| $FF6A | [OCPS/OBPI](#ff6aff6b--ocpsobpi-ocpdobpd-cgb-mode-only-obj-color-palette-specification--obj-palette-index-obj-color-palette-data--obj-palette-data) | OBJ color palette specification / OBJ palette index | R/W | CGB |
| $FF6B | [OCPD/OBPD](#ff6aff6b--ocpsobpi-ocpdobpd-cgb-mode-only-obj-color-palette-specification--obj-palette-index-obj-color-palette-data--obj-palette-data) | OBJ color palette data / OBJ palette data | R/W | CGB |
| $FF6C | [OPRI](#ff6c--opri-cgb-mode-only-object-priority-mode) | Object priority mode | R/W | CGB |
| $FF70 | [SVBK/WBK](#ff70--svbkwbk-cgb-mode-only-wram-bank) | WRAM bank | R/W | CGB |
| $FF76 | [PCM12](#ff76--pcm12-cgb-mode-only-digital-outputs-1--2-read-only) | Audio digital outputs 1 & 2 | R | CGB |
| $FF77 | [PCM34](#ff77--pcm34-cgb-mode-only-digital-outputs-3--4-read-only) | Audio digital outputs 3 & 4 | R | CGB |
| $FFFF | [IE](#ffff--ie-interrupt-enable) | Interrupt enable | R/W | All |
