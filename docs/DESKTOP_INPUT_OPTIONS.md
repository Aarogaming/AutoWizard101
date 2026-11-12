# Desktop input options

Research date: November 9, 2026. Scope: official Microsoft documentation and
existing local evidence. No drivers, device registrations, OS changes, game
inputs, or new dependencies were installed or tested.

**Recommendation:** keep ordinary UI input as Maelstrom's default. Treat a virtual
keyboard/mouse as an optional compatibility experiment after reproducing a
delivery failure in a controlled game we own. It is not currently a prerequisite
for Maelstrom's player architecture.

## What Windows documents

| Option | Documented behavior | Implication for Maelstrom |
| --- | --- | --- |
| `SendInput` | Inserts keyboard/mouse events into the input stream and returns an insertion count. UIPI restricts injection to equal/lower integrity applications; failure does not identify UIPI as the cause. | Keep bounded pulses, release checks and target checks. An insertion receipt still needs an observed game response. Do not weaken OS protections to make it work. [Microsoft API reference](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput) |
| Virtual HID Framework | A HID source driver creates virtual devices that HID clients can consume. The current VHF architecture guide specifies a kernel-mode source driver; `VhfCreate` uses `VhfKm.lib`. | This is driver engineering and a device lifecycle, beyond registering an ordinary application as a keyboard. [VHF architecture](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/virtual-hid-framework--vhf-), [VhfCreate](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/vhf/nf-vhf-vhfcreate) |
| Raw Input | The receiving application registers to receive device reports through `WM_INPUT` and can distinguish devices. | Registering Maelstrom for Raw Input makes it a receiver, not a new keyboard or mouse. Device-specific delivery is a possible compatibility test, not proof that a particular game will accept it. [Raw Input overview](https://learn.microsoft.com/en-us/windows/win32/inputdev/about-raw-input) |

VHF's documented kernel-source requirement does **not** mean every possible HID
driver design is kernel-only. Microsoft's broader transport-minidriver guidance
also supports UMDF and recommends considering it first where functionality fits.
That does not establish a portable, driver-free VHF source implementation.
[Transport minidriver overview](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/transport-minidrivers)

The brief `vhf.h` index describes interfaces for both modes, while the architecture
guide and `VhfCreate` requirements give the narrower supported source path above.
Use those specific requirements and verify the chosen WDK/OS combination before
promising a user-mode VHF design.
[Header index](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/vhf/)

## Focus and simultaneous desktop use

Normal keyboard messages go to the foreground thread's focused window.
Creating another keyboard does not itself define a second focused application.
The inference for Maelstrom is that a virtual device alone provides no guarantee
of private routing or uninterrupted simultaneous desktop use.
[Keyboard focus and activation](https://learn.microsoft.com/en-us/windows/win32/inputdev/about-keyboard-input)

Background input is possible for an application that supports it: Raw Input's
`RIDEV_INPUTSINK` lets the registered receiving application obtain input while not
foreground. Default Raw Input routing follows focus. This is application behavior;
Maelstrom registering itself does not change another game's input registration.
We have not established that W101 supports the required background mode.
[RAWINPUTDEVICE flags](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawinputdevice)

For a game/test harness we control, a cooperative receiver or isolated game
environment is a separate architectural option. Do not assume a virtual device
automatically supplies isolation, independent focus, or a private cursor.

## Installation and maintenance cost

Virtual drivers have applicable driver-signing requirements just like physical
hardware drivers. Microsoft's production signing policy describes Dev Portal
signing and certification/testing requirements. A custom VHF component therefore
adds a release and OS-compatibility lifecycle beyond the portable application.
[Kernel-mode signing requirements](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/kernel-mode-code-signing-requirements--windows-vista-and-later-),
[Driver signing policy](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/kernel-mode-code-signing-policy--windows-vista-and-later-)

Windows manages installation/removal through driver packages. Microsoft's PnPUtil
workflow uses an elevated prompt. Plan installer-managed provisioning, updates,
removal and rollback if this option is ever selected; keep the portable app useful
without it. This review makes no system changes.
[PnPUtil](https://learn.microsoft.com/en-us/windows-hardware/drivers/devtest/pnputil)

## Evidence and next decision

The documented [Pet Dance trial](PET_DANCE_DELIVERY_TRACKER_20261108.md) stopped on
an unrecognized loading screen with **zero input receipts and zero outcomes**.
That observation identifies no rejected arrow-delivery attempt. A different input
transport would not correct the missing recognized gameplay state. Recognition,
decision, dispatch, accepted input and game outcome need separate measurements.

A useful future comparison would use the same bounded action in an owned test
game and record:

1. Target/focus state, requested action and dispatch time.
2. Transport insertion/submission receipt and independently received game input.
3. Visible response, key release, lost focus and stop behavior.
4. End-to-end latency, resource cost, device removal and recovery behavior.

First establish a reproducible failure in the current
[bounded input path](../ProjectMaelstrom/ProjectMaelstrom/Utilities/DodgeInputPulse.cs).
If device-origin reports demonstrably fix that failure, evaluate an optional
backend behind the existing action registry, shared input owner and policy.
No benchmark here establishes better latency, broader game compatibility, game
approval or undetectability. This is an exploratory design note, not authorization
to implement or install a driver.
