# Hardware Registers

| Address | Name | Description | Readable / Writable | Models |
| --- | --- | --- | --- | --- |
| $FF00 | [P1/JOYP](joypad-input.md#ff00--p1joyp-joypad) | Joypad | Mixed | All |
| $FF01 | [SB](serial-data-transfer-link-cable.md#ff01--sb-serial-transfer-data) | Serial transfer data | R/W | All |
| $FF02 | [SC](serial-data-transfer-link-cable.md#ff02--sc-serial-transfer-control) | Serial transfer control | R/W | Mixed |
| $FF04 | [DIV](timer-and-divider-registers.md#ff04--div-divider-register) | Divider register | R/W | All |
| $FF05 | [TIMA](timer-and-divider-registers.md#ff05--tima-timer-counter) | Timer counter | R/W | All |
| $FF06 | [TMA](timer-and-divider-registers.md#ff06--tma-timer-modulo) | Timer modulo | R/W | All |
| $FF07 | [TAC](timer-and-divider-registers.md#ff07--tac-timer-control) | Timer control | R/W | All |
| $FF0F | [IF](interrupts.md#ff0f--if-interrupt-flag) | Interrupt flag | R/W | All |
| $FF10 | [NR10](audio-registers.md#ff10--nr10-channel-1-sweep) | Sound channel 1 sweep | R/W | All |
| $FF11 | [NR11](audio-registers.md#ff11--nr11-channel-1-length-timer--duty-cycle) | Sound channel 1 length timer & duty cycle | Mixed | All |
| $FF12 | [NR12](audio-registers.md#ff12--nr12-channel-1-volume--envelope) | Sound channel 1 volume & envelope | R/W | All |
| $FF13 | [NR13](audio-registers.md#ff13--nr13-channel-1-period-low-write-only) | Sound channel 1 period low | W | All |
| $FF14 | [NR14](audio-registers.md#ff14--nr14-channel-1-period-high--control) | Sound channel 1 period high & control | Mixed | All |
| $FF16 | [NR21](audio-registers.md#sound-channel-2--pulse) | Sound channel 2 length timer & duty cycle | Mixed | All |
| $FF17 | [NR22](audio-registers.md#sound-channel-2--pulse) | Sound channel 2 volume & envelope | R/W | All |
| $FF18 | [NR23](audio-registers.md#sound-channel-2--pulse) | Sound channel 2 period low | W | All |
| $FF19 | [NR24](audio-registers.md#sound-channel-2--pulse) | Sound channel 2 period high & control | Mixed | All |
| $FF1A | [NR30](audio-registers.md#ff1a--nr30-channel-3-dac-enable) | Sound channel 3 DAC enable | R/W | All |
| $FF1B | [NR31](audio-registers.md#ff1b--nr31-channel-3-length-timer-write-only) | Sound channel 3 length timer | W | All |
| $FF1C | [NR32](audio-registers.md#ff1c--nr32-channel-3-output-level) | Sound channel 3 output level | R/W | All |
| $FF1D | [NR33](audio-registers.md#ff1d--nr33-channel-3-period-low-write-only) | Sound channel 3 period low | W | All |
| $FF1E | [NR34](audio-registers.md#ff1e--nr34-channel-3-period-high--control) | Sound channel 3 period high & control | Mixed | All |
| $FF20 | [NR41](audio-registers.md#ff20--nr41-channel-4-length-timer-write-only) | Sound channel 4 length timer | W | All |
| $FF21 | [NR42](audio-registers.md#ff21--nr42-channel-4-volume--envelope) | Sound channel 4 volume & envelope | R/W | All |
| $FF22 | [NR43](audio-registers.md#ff22--nr43-channel-4-frequency--randomness) | Sound channel 4 frequency & randomness | R/W | All |
| $FF23 | [NR44](audio-registers.md#ff23--nr44-channel-4-control) | Sound channel 4 control | Mixed | All |
| $FF24 | [NR50](audio-registers.md#ff24--nr50-master-volume--vin-panning) | Master volume & VIN panning | R/W | All |
| $FF25 | [NR51](audio-registers.md#ff25--nr51-sound-panning) | Sound panning | R/W | All |
| $FF26 | [NR52](audio-registers.md#ff26--nr52-audio-master-control) | Sound on/off | Mixed | All |
| $FF30-FF3F | [Wave RAM](audio-registers.md#ff30ff3f--wave-pattern-ram) | Storage for one of the sound channels’ waveform | R/W | All |
| $FF40 | [LCDC](lcd-control.md#ff40--lcdc-lcd-control) | LCD control | R/W | All |
| $FF41 | [STAT](lcd-status-registers.md#ff41--stat-lcd-status) | LCD status | Mixed | All |
| $FF42 | [SCY](viewport-position-scrolling.md#ff42ff43--scy-scx-background-viewport-y-position-x-position) | Viewport Y position | R/W | All |
| $FF43 | [SCX](viewport-position-scrolling.md#ff42ff43--scy-scx-background-viewport-y-position-x-position) | Viewport X position | R/W | All |
| $FF44 | [LY](lcd-status-registers.md#ff44--ly-lcd-y-coordinate-read-only) | LCD Y coordinate | R | All |
| $FF45 | [LYC](lcd-status-registers.md#ff45--lyc-ly-compare) | LY compare | R/W | All |
| $FF46 | [DMA](oam-dma-transfer.md#ff46--dma-oam-dma-source-address--start) | OAM DMA source address & start | R/W | All |
| $FF47 | [BGP](palettes.md#ff47--bgp-non-cgb-mode-only-bg-palette-data) | BG palette data | R/W | DMG |
| $FF48 | [OBP0](palettes.md#ff48ff49--obp0-obp1-non-cgb-mode-only-obj-palette-0-1-data) | OBJ palette 0 data | R/W | DMG |
| $FF49 | [OBP1](palettes.md#ff48ff49--obp0-obp1-non-cgb-mode-only-obj-palette-0-1-data) | OBJ palette 1 data | R/W | DMG |
| $FF4A | [WY](window-behavior.md#ff4aff4b--wy-wx-window-y-position-x-position-plus-7) | Window Y position | R/W | All |
| $FF4B | [WX](window-behavior.md#ff4aff4b--wy-wx-window-y-position-x-position-plus-7) | Window X position plus 7 | R/W | All |
| $FF4C | [KEY0/SYS](cgb-registers.md#ff4c--key0sys-cgb-mode-only-cpu-mode-select) | CPU mode select | Mixed | CGB |
| $FF4D | [KEY1/SPD](cgb-registers.md#ff4d--key1spd-cgb-mode-only-prepare-speed-switch) | Prepare speed switch | Mixed | CGB |
| $FF4F | [VBK](cgb-registers.md#ff4f--vbk-cgb-mode-only-vram-bank) | VRAM bank | R/W | CGB |
| $FF50 | [BANK](power-up-sequence.md#power-up-sequence) | Boot ROM mapping control | W | All |
| $FF51 | [HDMA1](cgb-registers.md#ff51ff52--hdma1-hdma2-cgb-mode-only-vram-dma-source-high-low-write-only) | VRAM DMA source high | W | CGB |
| $FF52 | [HDMA2](cgb-registers.md#ff51ff52--hdma1-hdma2-cgb-mode-only-vram-dma-source-high-low-write-only) | VRAM DMA source low | W | CGB |
| $FF53 | [HDMA3](cgb-registers.md#ff53ff54--hdma3-hdma4-cgb-mode-only-vram-dma-destination-high-low-write-only) | VRAM DMA destination high | W | CGB |
| $FF54 | [HDMA4](cgb-registers.md#ff53ff54--hdma3-hdma4-cgb-mode-only-vram-dma-destination-high-low-write-only) | VRAM DMA destination low | W | CGB |
| $FF55 | [HDMA5](cgb-registers.md#ff55--hdma5-cgb-mode-only-vram-dma-lengthmodestart) | VRAM DMA length/mode/start | R/W | CGB |
| $FF56 | [RP](cgb-registers.md#ff56--rp-cgb-mode-only-infrared-communications-port) | Infrared communications port | Mixed | CGB |
| $FF68 | [BCPS/BGPI](palettes.md#ff68--bcpsbgpi-cgb-mode-only-background-color-palette-specification--background-palette-index) | Background color palette specification / Background palette index | R/W | CGB |
| $FF69 | [BCPD/BGPD](palettes.md#ff69--bcpdbgpd-cgb-mode-only-background-color-palette-data--background-palette-data) | Background color palette data / Background palette data | R/W | CGB |
| $FF6A | [OCPS/OBPI](palettes.md#ff6aff6b--ocpsobpi-ocpdobpd-cgb-mode-only-obj-color-palette-specification--obj-palette-index-obj-color-palette-data--obj-palette-data) | OBJ color palette specification / OBJ palette index | R/W | CGB |
| $FF6B | [OCPD/OBPD](palettes.md#ff6aff6b--ocpsobpi-ocpdobpd-cgb-mode-only-obj-color-palette-specification--obj-palette-index-obj-color-palette-data--obj-palette-data) | OBJ color palette data / OBJ palette data | R/W | CGB |
| $FF6C | [OPRI](cgb-registers.md#ff6c--opri-cgb-mode-only-object-priority-mode) | Object priority mode | R/W | CGB |
| $FF70 | [SVBK/WBK](cgb-registers.md#ff70--svbkwbk-cgb-mode-only-wram-bank) | WRAM bank | R/W | CGB |
| $FF76 | [PCM12](audio-details.md#ff76--pcm12-cgb-mode-only-digital-outputs-1--2-read-only) | Audio digital outputs 1 & 2 | R | CGB |
| $FF77 | [PCM34](audio-details.md#ff77--pcm34-cgb-mode-only-digital-outputs-3--4-read-only) | Audio digital outputs 3 & 4 | R | CGB |
| $FFFF | [IE](interrupts.md#ffff--ie-interrupt-enable) | Interrupt enable | R/W | All |
