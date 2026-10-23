# Supported devices

[← Wiki home](README.md)

- [What each controller reports](#what-each-controller-reports)
- [Bluetooth mice, keyboards and headsets](#bluetooth-mice-keyboards-and-headsets)
- [Logitech mice, keyboards and headsets](#logitech-mice-keyboards-and-headsets)
- [Known limitations](#known-limitations)
- [How batteries are read](#how-batteries-are-read)
- [What isn't supported yet](#what-isnt-supported-yet)

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

## Bluetooth mice, keyboards and headsets

Any Bluetooth device works **if Windows shows its battery**: open Settings → Bluetooth & devices, and look for a battery level beside it. ControllerHP reads that same level, so a device Windows shows a battery for appears in ControllerHP too, and one it doesn't, doesn't.

| Device | Connection | Battery | Charging shown | Tested on hardware |
|---|---|---|---|---|
| Keyboards | Bluetooth Low Energy | Exact % | Worked out from the % rising | **Tested** |
| Mice | Bluetooth Low Energy | Exact % | As above | Likely |
| Headsets, earbuds | Bluetooth (classic) | Exact %, or steps on some models | As above | Untested |
| Keyboards, mice | Bluetooth (classic) | Exact % | As above | Untested |

Each device is drawn as what it is (a keyboard, mouse, headset or controller), and **Show** in Settings turns whole kinds on or off. ControllerHP works out the kind from:
1. what the device says it is over Bluetooth;
2. failing that, the parts Windows installed for it: a keyboard part makes it a keyboard, even when it also has a mouse part for media keys or a touchpad;
3. failing that, whether it plays audio, which makes it a headset.

Anything else shows as **Other**, with the Bluetooth sign.

## Logitech mice, keyboards and headsets

Logitech devices on a **Unifying, Bolt or Lightspeed receiver**, or plugged in by their USB cable, are read the way Logitech's own software reads them: ControllerHP asks each device for its battery, name and type through the receiver, every 30 seconds. It only ever reads; it never changes a setting, and it works alongside G HUB and Logitech Options.

| Device | Connection | Battery | Charging shown | Tested on hardware |
|---|---|---|---|---|
| G Pro Wireless | Lightspeed receiver | Estimated from its battery voltage | Yes | **Tested** |
| Other Logitech mice and keyboards | Unifying, Bolt or Lightspeed receiver | Exact %, or estimated from voltage on older models | Yes | Likely |
| Logitech headsets | Their USB receiver | Varies | Varies | Untested |

The panel shows them as connected by **USB receiver**. A device that's asleep keeps its last reading rather than coming and going, since it doesn't answer until it wakes; one that's switched off or out of range shows as disconnected.

**Other makers' receivers** (dongles) aren't supported yet. Their battery isn't something Windows records, and each maker reads it differently, usually without publishing how.

## Known limitations

- **PlayStation controllers over Bluetooth:** they only report their battery after being switched to their full input report. ControllerHP does this when one connects, as Steam does. While it's in that mode, games that read it through the older DirectInput interface (some emulators and older games) don't see its input. Steam Input, and games with built-in DualSense or DualShock 4 support, are unaffected. See [a game doesn't see my PlayStation controller](troubleshooting.md#a-game-doesnt-see-my-playstation-controller).
- **PlayStation controllers held by another tool:** if a tool like DS4Windows with HidHide takes exclusive control of a controller, ControllerHP can't read it, and it shows as *battery unknown*.
- **Xbox controllers over Bluetooth:** Windows gives no charging signal, so ControllerHP works it out. The bolt appears at the first rise in percentage, and clears at 100% or after 5 minutes without a rise.
- **Two identical Xbox controllers**, one on Bluetooth and one not, may have their readings swapped.
- **Bluetooth mice, keyboards and headsets:** Windows doesn't say whether they're charging, so ControllerHP works it out from the level rising, as for Xbox controllers. Windows also only updates their level when the device reports it, which some do rarely.
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

**Logitech devices** are read through Logitech's HID++ protocol, on the receiver's vendor interface: the device's battery feature (unified battery, battery status, or battery voltage, converted to a percentage along a typical lithium curve), its name and type, and its unit id, which keeps its history together whichever receiver or port it's on.

**Other Bluetooth devices** are read from the battery level Windows records for each paired device. Whether one is connected comes from Windows' Bluetooth stack, the same as Settings shows, rather than the flag Windows records beside the level, which can read as disconnected for a headset playing audio.

Connecting and disconnecting are picked up as they happen. A check every 30 seconds catches battery changes on connections that don't announce them.

## What isn't supported yet

- **Devices on other makers' USB receivers** (dongles): Logitech's are supported; others are planned, one maker at a time, where the maker's protocol can be read safely.
- **Nintendo Switch Pro controllers, Joy-Cons and third-party controllers.**
- **Bluetooth devices Windows shows no battery for.**
- **Other systems:** ControllerHP is Windows-only for now.

Several of these are listed in [Help wanted](../dev/help-wanted.md), with where a contributor could start.

If you'd like a device added, [open a feature request](https://github.com/veloxity343/ControllerHP/issues/new/choose). If you can help test, say so: support for a new device needs someone with the hardware.

---

[← Devices](devices.md) · Next: [Troubleshooting →](troubleshooting.md)
