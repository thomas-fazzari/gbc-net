# Timer obscure behaviour

System counter

DIV is just the visible part of the **system counter**.

The **system counter** is constantly incrementing every M-cycle, unless the CPU is in [STOP mode](#using-the-stop-instruction).

## Timer Global Circuit

76
54
32
10


7


0
KEY1

Double
Speed
Mode
active?


DIV


Reset

Write
to DIV

Increment

M-cycle
clock


1
0


Falling
edge
detector


DIV-APU
event


See detailed
schematic


2
10
TAC

TAC.freq

TAC.enable


Timer
tick


See
detailed
schematic

TMA


TIMA

Load

Inc.

Overflow


4
32
10
IF

Interrupt request

## Relation between Timer and Divider register

This is a schematic of the circuit involving TAC and DIV:

On **DMG**:


76
54
32
10


DIV


Reset

Write
to DIV

Increment

M-cycle
clock


0
3
2
1


2
10
TAC

TAC.freq

TAC.enable


AND


Falling
edge
detector


Timer
tick


On **CGB**:


76
54
32
10


DIV


Reset

Write
to DIV

Increment

M-cycle
clock


0
3
2
1


2
10
TAC

TAC.freq

TAC.enable


Falling
edge
detector


AND


Timer
tick

Notice how the bits themselves are connected to the multiplexer and then to the falling-edge detector; this causes a few odd behaviors:

- Resetting the entire system counter (by writing to `DIV`) can reset the bit currently selected by the multiplexer, thus sending a “Timer tick” and/or “[DIV-APU event](#div-apu)” pulse early.
- Changing which bit of the system counter is selected (by changing the “Clock select” bits of [`TAC`](#ff07--tac-timer-control)) from a bit currently set to another that is currently unset, will send a “Timer tick” pulse.
  (For example: if the system counter is equal to $3FF0 and `TAC` to $FC, writing $05 or $06 to `TAC` will instantly send a “Timer tick”, but $04 or $07 won’t.)
- On monochrome consoles, disabling the timer if the currently selected bit is set, will send a “Timer tick” once.
  This does not happen on Color models.
- On Color models, a write to `TAC` that fulfills the previous bullet’s conditions *and* turns the timer on (it was disabled before) may or may not send a “Timer tick”.
  The exact behaviour varies between individual consoles.

## Timer overflow behavior

When `TIMA` overflows, the value from `TMA` is copied, and the timer flag is set in [`IF`](#ff0f--if-interrupt-flag), but **one M-cycle later**.
This means that `TIMA` is equal to $00 for the M-cycle after it overflows.

This only happens when `TIMA` overflows from incrementing, it cannot be made to happen by manually writing to `TIMA`.

Here is an example; `SYS` represents the lower 8 bits of the system counter, and `TAC` is $FD (timer enabled, bit 1 of `SYS` selected as source):

`TIMA` overflows on cycle A, but the interrupt is only requested on cycle B:

| M-cycle |  |  |  | A | B |  | ​ |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `SYS` | 2B | 2C | 2D | 2E | 2F | 30 | 31 |
| `TIMA` | FE | FF | FF | **00** | 23 | 24 | 24 |
| `TMA` | 23 | 23 | 23 | 23 | 23 | 23 | 23 |
| `IF` | E0 | E0 | E0 | **E0** | E4 | E4 | E4 |

Here are some unexpected behaviors:

1. Writing to `TIMA` during cycle A acts as if the overflow **didn’t happen**!
   `TMA` will not be copied to `TIMA` (the value written will therefore stay), and bit 2 of `IF` will not be set.
   Writing to `DIV`, `TAC`, or other registers won’t prevent the `IF` flag from being set or `TIMA` from being reloaded.
2. Writing to `TIMA` during cycle B will be ignored; `TIMA` will be equal to `TMA` at the end of the cycle anyway.
3. Writing to `TMA` during cycle B will have the same value copied to `TIMA` as well, on the same cycle.

Here is how `TIMA` and `TMA` interact:

76
54
32
10


TMA


Write
to TMA

Load

CPU data bus


0
1


TIMA

Timer
tick

Increment


Falling
edge
detector


Write to
TIMA


NOT


AND


Delay

Set


4
32
10
IF


OR

Load

Explanation of the above behaviors:

1. Writing to `TIMA` blocks the falling edge from the increment from being detected (see the `AND` gate)[1](#footnote-write_edge).
2. The “Load” signal stays enabled for the entirety of cycle B, and since `TIMA` is made of TAL cells, it’s constantly copying its input.
   However, the “Write to TIMA” signal gets reset in the middle of the cycle, thus the multiplexer emits `TMA`’s value again; in essence, the CPU’s write to `TIMA` *does* go through, but it’s overwritten right after.
3. As mentioned in the previous bullet point, `TIMA` constantly copies its input, so it updates together with `TMA`.
   This and the previous bullet point can be emulated as if `TMA` was copied to `TIMA` at the very end of the cycle, though this is not quite what’s happening in hardware.

---

1. This is necessary, because otherwise writing a number with bit 7 reset (either from the CPU or from `TMA`) when `TIMA`’s bit 7 is set, would trigger the bit 7 falling edge detector and thus schedule a spurious interrupt. [↩](#fr-write_edge-1)
