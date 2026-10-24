# Supported devices

[← Wiki home](README.md)

- [What each controller reports](#what-each-controller-reports)
- [Bluetooth mice, keyboards and headsets](#bluetooth-mice-keyboards-and-headsets)
- [Logitech mice, keyboards and headsets](#logitech-mice-keyboards-and-headsets)
- [Devices on their makers' receivers](#devices-on-their-makers-receivers)
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
| Nintendo Switch Pro Controller, Joy-Con (L and R) | Bluetooth | 5 steps | Yes | Untested |
| Steam Controller (2025) | Its puck | Exact % | Yes | Untested |
| 8BitDo SF30 Pro, SN30 Pro, Pro 2, Pro 3 (in their own mode) | USB or Bluetooth | Exact %, only while Steam or a game is using it | Yes | Untested |

8BitDo controllers in XInput mode read as Xbox controllers. In their own mode, WattsLeft only listens: switching on the report that carries their battery would stop DirectInput games seeing them, so the level shows only once Steam or a game has done that. A Switch controller is asked once for its device information, which carries the battery, without changing its mode; over USB it isn't read. The Steam Controller's puck is only listened to.

**Have one marked Likely or Untested?** Please help: connect it, run `WattsLeft.exe --list` (see [Reporting a bug](troubleshooting.md#reporting-a-bug)), and post what you see in [Discussions](https://github.com/veloxity343/WattsLeft/discussions), whether it works or not.

The panel and Settings show each controller's connection: USB, Bluetooth or Xbox Wireless Adapter.

## Bluetooth mice, keyboards and headsets

Any Bluetooth device works **if Windows shows its battery**: open Settings → Bluetooth & devices, and look for a battery level beside it. WattsLeft reads that same level, so a device Windows shows a battery for appears in WattsLeft too, and one it doesn't, doesn't.

| Device | Connection | Battery | Charging shown | Tested on hardware |
|---|---|---|---|---|
| Keyboards | Bluetooth Low Energy | Exact % | Worked out from the % rising | **Tested** |
| Mice | Bluetooth Low Energy | Exact % | As above | Likely |
| Headsets, earbuds | Bluetooth (classic) | Exact %, or steps on some models | As above | Untested |
| Keyboards, mice | Bluetooth (classic) | Exact % | As above | Untested |

Each device is drawn as what it is (a keyboard, mouse, headset or controller), and **Show** in Settings turns whole kinds on or off. WattsLeft works out the kind from:
1. what the device says it is over Bluetooth;
2. failing that, the parts Windows installed for it: a keyboard part makes it a keyboard, even when it also has a mouse part for media keys or a touchpad;
3. failing that, whether it plays audio, which makes it a headset.

Anything else shows as **Other**, with the Bluetooth sign.

## Logitech mice, keyboards and headsets

Logitech devices on a **Unifying, Bolt or Lightspeed receiver**, or plugged in by their USB cable, are read the way Logitech's own software reads them: WattsLeft asks each device for its battery, name and type through the receiver, every 30 seconds. It only ever reads; it never changes a setting, and it works alongside G HUB and Logitech Options.

| Device | Connection | Battery | Charging shown | Tested on hardware |
|---|---|---|---|---|
| G Pro Wireless | Lightspeed receiver | Estimated from its battery voltage | Yes | **Tested** |
| Other Logitech mice and keyboards | Unifying, Bolt or Lightspeed receiver | Exact %, or estimated from voltage on older models | Yes | Likely |
| Logitech G533, G535, G633, G635, G733, G933, G935, G PRO, G PRO X, G PRO X 2 headsets | Their USB receiver | Estimated from battery voltage | Yes | Untested |
| Logitech G PRO X 2 LIGHTSPEED headset | Its receiver | Exact % | Yes | Untested |
| Logitech G930 headset | Its USB receiver | Estimated from the raw level | No | Untested |
| Astro A50 Gen 5 | Its base station | Exact % | Yes, docked | Untested |

The panel shows them as connected by **USB receiver**. A device that's asleep keeps its last reading rather than coming and going, since it doesn't answer until it wakes; one that's switched off or out of range shows as disconnected.

## Devices on their makers' receivers

Many wireless mice, keyboards and headsets connect through their maker's own USB receiver (a dongle), and Windows doesn't record their battery. WattsLeft reads these through each maker's own protocol, the way their software does, every 30 seconds. For each one:
- it only ever sends **read** requests, never anything that changes a setting or the device's mode;
- it only talks to devices it recognises by their maker and product id;
- it works alongside the maker's own software.

Devices that need a mode switched before they'll answer (Razer's BlackShark V2 Pro 2023 and Barracuda Pro headsets) are left out for that reason.

| Maker | Devices | Battery | Charging shown | Tested on hardware |
|---|---|---|---|---|
| Razer | Wireless mice (DeathAdder, Viper, Basilisk, Naga, Cobra, Pro Click, Orochi and more), keyboards (BlackWidow, DeathStalker HyperSpeed and V2/V3/V4 Pro), the Mouse Dock Pro | Exact % | Yes | Untested |
| Razer | BlackShark V2 Pro (2020) headset | Exact %, or charging only while charging | Yes | Untested |
| SteelSeries | Arctis Nova 7, 7X, 7P, Nova 5, Nova 3, Arctis 7+, GameBuds, older Arctis 1, 7, 9 and Pro Wireless headsets, the Nova Pro Wireless and Nova Elite base stations | Exact %, or steps on some | Most | Untested |
| SteelSeries | Rival 3 Wireless, Aerox 3, 5 and 9 Wireless mice | Exact % | Yes | Untested |
| Corsair | Void v2 Wireless, HS80 Max Wireless, Virtuoso Max Wireless, Virtuoso RGB Wireless SE and XT headsets; Dark Core RGB Pro SE mouse | Exact % | Virtuoso SE and XT only | Untested |
| Corsair | Older wireless headsets: Void Wireless, Void Pro and Void Elite Wireless, HS70 and HS70 Pro Wireless, and their kin | Exact % | Yes | Untested |
| HyperX | Cloud II Wireless (HP and Kingston models), Cloud Alpha Wireless, Cloud Alpha 2, Cloud III Wireless, Cloud II Core Wireless, Cloud III S Wireless | Exact % | Yes | Untested |
| HyperX | Cloud Flight Wireless | Estimated from battery voltage | Yes, without a level | Untested |
| ASUS | ROG Gladius III, Chakram, Keris, Spatha X, Harpe Ace, Pugio II, Strix Impact II Wireless, TUF Gaming M4 Wireless and others | Exact %, or 5 steps on older models | Yes | Untested |
| Keychron | Ultra-Link 8K receiver, M5 | Exact % | No | Untested |
| Pulsar, ATK, VXE, Hitscan | Pulsar X2 V2 Mini and X2 CrazyLight Mini, ATK VXE R1 SE+, VXE R1 Pro Max, Hitscan Hyperlight | Exact % | Yes, on the cable | Untested |
| WLmouse, LAMZU, G-Wolves | WLmouse Beast X family, LAMZU Maya X, G-Wolves mice (HTX, HSK, Fenrir, HTS and more) | Exact % | Yes, on the cable | Untested |
| MCHOSE | L7, M7, A7, K7 families, G7, A5 Pro Max | Exact % | Yes | Untested |
| MCHOSE | X9 Wireless headset | Exact % | No | Untested |
| Lofree | Hyzen keyboard on its dongle | Exact % | No | Untested |
| Audeze | Maxwell, Maxwell 2 | Exact % | Yes, on its cable | Untested |
| JBL | Quantum 910 Wireless | Exact %, when it reports it | No | Untested |
| NZXT | Lift Elite Wireless | Exact % | Yes | Untested |
| Angry Miao | AM Infinity 8K mouse | Exact % | No | Untested |
| Sony | INZONE H5 and H9 II headsets | Exact % | Yes | Untested |
| Sony | INZONE Buds, the lower earbud | Exact %, when the dongle reports it | No | Untested |
| Lenovo | Wireless VoIP Headset | Exact % | No | Untested |

**A few behave differently:**
- **Sleeping devices.** Many receivers can't tell a sleeping mouse from one that's switched off. A mouse or keyboard that stops answering keeps its last level for 30 minutes, or for as long as its receiver is plugged in for some models, so a mouse that dozes between games doesn't come and go. A headset that stops answering goes after 2 minutes, and any device that says it's switched off goes at once.
- **One device on two connections.** A mouse on its receiver and on its cable is one device; the cable says when it's charging.
- **Unnamed devices.** Some shared receivers (G-Wolves', MCHOSE's) don't say which mouse is paired to them, so it shows with the maker's name: rename it on the Devices page.
- **Headsets that only speak on events.** The JBL Quantum 910 reports its battery only now and then, for example when it's plugged in to charge, so its level can take a while to appear.

**Other makers' receivers** aren't supported yet, nor is a NuPhy keyboard on its dongle: its battery isn't something the dongle makes available.

## Known limitations

- **PlayStation controllers over Bluetooth:** they only report their battery after being switched to their full input report. WattsLeft does this when one connects, as Steam does. While it's in that mode, games that read it through the older DirectInput interface (some emulators and older games) don't see its input. Steam Input, and games with built-in DualSense or DualShock 4 support, are unaffected. See [a game doesn't see my PlayStation controller](troubleshooting.md#a-game-doesnt-see-my-playstation-controller).
- **PlayStation controllers held by another tool:** if a tool like DS4Windows with HidHide takes exclusive control of a controller, WattsLeft can't read it, and it shows as *battery unknown*.
- **Xbox controllers over Bluetooth:** Windows gives no charging signal, so WattsLeft works it out. The bolt appears at the first rise in percentage, and clears at 100% or after 5 minutes without a rise.
- **Two identical Xbox controllers**, one on Bluetooth and one not, may have their readings swapped.
- **Bluetooth mice, keyboards and headsets:** Windows doesn't say whether they're charging, so WattsLeft works it out from the level rising, as for Xbox controllers. Windows also only updates their level when the device reports it, which some do rarely.
- **Exclusive fullscreen:** pop-ups can't appear over games running in exclusive fullscreen, so they're held and shown once the game closes. Borderless and windowed modes are fine.

## How batteries are read

You don't need this to use WattsLeft, but it explains the differences above.

**Xbox controllers.** Several sources see the same controllers, so the first one that finds any is used:

| Source | Covers | Detail |
|---|---|---|
| Windows.Gaming.Input | USB, Xbox Wireless Adapter, Bluetooth | Percentage and charging state; finds controllers as they connect and disconnect. |
| ↳ Bluetooth LE battery | Bluetooth | Replaces the percentage above, which is wrong over Bluetooth. It's the same value Windows' Bluetooth settings show, and it updates as soon as Windows does. |
| XInput | USB, Xbox Wireless Adapter | A fallback: four levels, no charging state. |

**PlayStation controllers** are read from the battery status in their input reports, sampled every 5 seconds. They show alongside Xbox controllers.

**Devices on their makers' receivers** are read through each maker's protocol, on the receiver's vendor collection: a read request, as a feature or output report, and an answer that has to carry the request's echo or a documented reply id before it's believed, so other traffic on the collection is never read as a level. Where a maker's protocol is shared across brands, one protocol covers them all (the WLmouse, LAMZU and G-Wolves mice, for example, run the same firmware).

**Logitech devices** are read through Logitech's HID++ protocol, on the receiver's vendor interface: the device's battery feature (unified battery, battery status, or battery voltage, converted to a percentage along a typical lithium curve), its name and type, and its unit id, which keeps its history together whichever receiver or port it's on.

**Other Bluetooth devices** are read from the battery level Windows records for each paired device. Whether one is connected comes from Windows' Bluetooth stack, the same as Settings shows, rather than the flag Windows records beside the level, which can read as disconnected for a headset playing audio.

Connecting and disconnecting are picked up as they happen. A check every 30 seconds catches battery changes on connections that don't announce them.

## What isn't supported yet

- **Devices on other makers' USB receivers** (dongles): the makers above are supported; others can be added where the maker's protocol is known and can be read without changing anything on the device.
- **Razer's BlackShark V2 Pro (2023) and Barracuda Pro headsets,** which need a mode switched before they'll answer.
- **Nintendo Switch controllers over USB, and other third-party controllers.**
- **Bluetooth devices Windows shows no battery for.**
- **Other systems:** WattsLeft is Windows-only for now.

Several of these are listed in [Help wanted](../dev/help-wanted.md), with where a contributor could start.

If you'd like a device added, [open a feature request](https://github.com/veloxity343/WattsLeft/issues/new/choose). If you can help test, say so: support for a new device needs someone with the hardware.

---

[← Devices](devices.md) · Next: [Troubleshooting →](troubleshooting.md)
