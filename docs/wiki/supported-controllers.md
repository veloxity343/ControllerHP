# Supported controllers

[← Wiki home](README.md)

- [What each controller reports](#what-each-controller-reports)
- [Known limitations](#known-limitations)
- [How batteries are read](#how-batteries-are-read)
- [Controllers that aren't supported yet](#controllers-that-arent-supported-yet)

## What each controller reports

The last column says whether each one has been tried on real hardware:

- **Tested:** seen working on a real controller.
- **Likely:** not tried yet, but it uses exactly the same code as a tested combination, so it should work.
- **Untested:** written from the controller's documented format, but nobody has tried it on real hardware yet.

| Controller | Connection | Battery | Charging shown | Tested on hardware |
|---|---|---|---|---|
| Xbox Wireless Controller (One / Series) | Bluetooth | Exact % | Worked out from the % rising (a minute or two after plugging in) | **Tested** (Series controller) |
| Xbox Elite Series 2, Xbox Adaptive Controller | Bluetooth | Exact % | As above | Likely |
| Xbox Wireless Controller (One / Series) | USB | Exact %, or 4 levels on some setups | Yes | Untested |
| Xbox Wireless Controller (One / Series) | Xbox Wireless Adapter | Exact %, or 4 levels on some setups | Yes | Untested |
| DualSense | Bluetooth | 10% steps | Yes | **Tested** |
| DualSense Edge | Bluetooth | 10% steps | Yes | Likely |
| DualSense, DualSense Edge | USB | 10% steps | Yes | Untested |
| DualShock 4 | USB or Bluetooth | 10% steps | Yes | Untested |

**Have one marked Likely or Untested?** Please help: connect it, run `ControllerHP.exe --list` (see [Reporting a bug](troubleshooting.md#reporting-a-bug)), and post what you see in [Discussions](https://github.com/veloxity343/ControllerHP/discussions), whether it works or not.

The panel and Settings show each controller's connection: USB, Bluetooth or Xbox Wireless Adapter.

## Known limitations

- **PlayStation controllers over Bluetooth:** they only report their battery after being switched to their full input report. ControllerHP does this when one connects, as Steam does. While it's in that mode, games that read it through the older DirectInput interface (some emulators and older games) don't see its input. Steam Input, and games with built-in DualSense or DualShock 4 support, are unaffected. See [a game doesn't see my PlayStation controller](troubleshooting.md#a-game-doesnt-see-my-playstation-controller).
- **PlayStation controllers held by another tool:** if a tool like DS4Windows with HidHide takes exclusive control of a controller, ControllerHP can't read it, and it shows as *battery unknown*.
- **Xbox controllers over Bluetooth:** Windows gives no charging signal, so ControllerHP works it out. The bolt appears at the first rise in percentage, and clears at 100% or after 5 minutes without a rise.
- **Two identical Xbox controllers**, one on Bluetooth and one not, may have their readings swapped.
- **Exclusive fullscreen:** pop-ups can't appear over games running in exclusive fullscreen, so they're held and shown once the game closes. Borderless and windowed modes are fine.

## How batteries are read

You don't need this to use ControllerHP, but it explains the differences above.

**Xbox controllers.** Several sources see the same controllers, so the first one that finds any is used:

| Source | Covers | Detail |
|---|---|---|
| Windows.Gaming.Input | USB, Xbox Wireless Adapter, Bluetooth | Percentage and charging state; finds controllers as they connect and disconnect. |
| ↳ Bluetooth LE battery | Bluetooth | Replaces the percentage above, which is wrong over Bluetooth. It's the same value Windows' Bluetooth settings show, and it updates as soon as Windows does. |
| XInput | USB, Xbox Wireless Adapter | A fallback: four levels, no charging state. |

**PlayStation controllers** are read from the battery status in their input reports, sampled every 5 seconds. They show alongside Xbox controllers.

Connecting and disconnecting are picked up as they happen. A check every 30 seconds catches battery changes on connections that don't announce them.

## Controllers that aren't supported yet

Nintendo Switch Pro controllers, Joy-Cons and third-party controllers aren't supported yet. ControllerHP is also Windows-only for now. Both are listed in [Help wanted](../dev/help-wanted.md), with where a contributor could start.

If you'd like a controller added, [open a feature request](https://github.com/veloxity343/ControllerHP/issues/new/choose). If you can help test, say so: support for a new controller needs someone with the hardware.

---

[← Controllers](controllers.md) · Next: [Troubleshooting →](troubleshooting.md)
