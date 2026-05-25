# Timer and Divider Registers

NOTE

The Timer described below is the built-in timer in the Game Boy. It has
nothing to do with the MBC3s battery buffered Real Time Clock - that’s
a completely different thing, described in
[Memory Bank Controllers](#mbcs).

## FF04 — DIV: Divider register

This register is incremented at a rate of 16384Hz (~16779Hz on SGB).
Writing any value to this register resets it to $00.
Additionally, this register is reset when executing the `stop` instruction, and
only begins ticking again once `stop` mode ends. This also occurs during a
[speed switch](#ff4d--key1spd-cgb-mode-only-prepare-speed-switch).
(TODO: how is it affected by the wait after a speed switch?)

Note: The divider is affected by CGB double speed mode, and will
increment at 32768Hz in double speed.

## FF05 — TIMA: Timer counter

This timer is incremented at the clock frequency specified by the TAC
register ($FF07). When the value overflows (exceeds $FF)
it is reset to the value specified in TMA (FF06) and [an interrupt](#int-50--timer-interrupt)
is requested, as described below.

## FF06 — TMA: Timer modulo

When TIMA overflows, it is reset to the value in this register and [an interrupt](#int-50--timer-interrupt) is requested.
Example of use: if TMA is set to $FF, an interrupt is requested at the clock frequency selected in
TAC (because every increment is an overflow). However, if TMA is set to $FE, an interrupt is
only requested every two increments, which effectively divides the selected clock by two. Setting
TMA to $FD would divide the clock by three, and so on.

If a TMA write is executed on the same M-cycle as the content of TMA is transferred to TIMA
due to a timer overflow, the old value is transferred to TIMA.

## FF07 — TAC: Timer control

|  | 7 | 6 | 5 | 4 | 3 | 2 | 1 | 0 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| **TAC** |  | | | | | Enable | Clock select | |

- **Enable**: Controls whether `TIMA` is incremented.
  Note that `DIV` is **always** counting, regardless of this bit.
- **Clock select**: Controls the frequency at which `TIMA` is incremented, as follows:

  | Clock select | Increment every | Frequency (Hz) | | |
  | --- | --- | --- | --- | --- |
  | DMG, SGB2, CGB in normal-speed mode | SGB1 | CGB in double-speed mode |
  | 00 | 256 M-cycles | 4096 | ~4194 | 8192 |
  | 01 | 4 M-cycles | 262144 | ~268400 | 524288 |
  | 10 | 16 M-cycles | 65536 | ~67110 | 131072 |
  | 11 | 64 M-cycles | 16384 | ~16780 | 32768 |

Note that writing to this register [may increase `TIMA` once](#relation-between-timer-and-divider-register)!
