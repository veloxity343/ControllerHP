# Using WattsLeft

[← Wiki home](README.md)

- [The tray icon](#the-tray-icon)
- [Several devices](#several-devices)
- [The device panel](#the-device-panel)
- [Pop-ups](#pop-ups)
- [The right-click menu](#the-right-click-menu)
- [Settings](#settings)
- [Sharing battery levels with other apps](#sharing-battery-levels-with-other-apps)

## The tray icon

| The icon shows | Meaning |
|---|---|
| A device with a thin bar beneath it | The battery level. The device is drawn as what it is: a controller, mouse, keyboard or headset, or the Bluetooth sign for anything else. Mice and the Bluetooth sign have the bar up the right instead. The bar fills from the left, and its colour shifts from green (75% and up) through yellow (50%) to red (20% and below). |
| A bolt at the bottom right | Charging. |
| A full, uncoloured bar with a bolt | Plugged in, with no battery reported (a wired-only controller). |
| **?** at the bottom right | Connected, but the battery level isn't known yet. |
| The WattsLeft gamepad | No devices connected. |

Hover over the icon for the device's name, its exact percentage and, once there's enough history, how long it has left. For example: *Xbox Wireless Controller: 62%, about 3 h to 20%*, or *DualSense Wireless Controller: 85%, charging, about 1 h 30 min to full*.

Time left counts down to your low warning level (or to empty if that warning is off), or up to full while charging. It's the same figure as the [Devices page](devices.md#the-stats) shows, and it appears after about an hour on battery.

The icon follows your taskbar's light or dark mode.

## Several devices

By default there's **one** tray icon, showing the device that most needs attention:

1. Devices on battery, lowest first.
2. Then any whose level isn't known yet.
3. Then devices that are charging or wired, since they're fine.

Hovering the icon adds a line listing the rest, such as *+1 more: Xbox Wireless Controller: 62%*.

To stop two similar batteries swapping places with every 1% change, a device only takes over the icon once it's 3 points lower than the one shown.

To see more icons, change **Tray icons** to **2** or **One per device** in the right-click menu or in Settings. The icons stay in the same place in the taskbar as devices come and go.

## The device panel

Left-click the tray icon to open a panel listing **every** connected device, including any that don't have a tray icon of their own. Each shows its battery, how long it has left (once known), and how it's connected:

> **Xbox Wireless Controller**
> 62% · 3 h to 20% · Bluetooth

The panel lists devices lowest battery first. The gear at the top right opens [Settings](#settings). Click anywhere else, press Esc or click the tray icon again to close it.

## Pop-ups

Slim pop-ups appear in the top-right corner of the screen:

| Pop-up | When |
|---|---|
| **Connected** | A device connects. Devices already on when WattsLeft starts don't get one. |
| **Disconnected** | A device turns off or disconnects. |
| **Battery low** | A device on battery drops to the low level (20% by default). |
| **Battery critical** | It drops to the critical level (10% by default). |
| **Fully charged** | A device that's been charging reaches 100%. Once per charge. |

Each warning shows once per discharge. It can show again after the device charges, or after its level recovers by 5 points or more.

The levels are set in [Settings](#settings), and a device can have [its own](devices.md#a-devices-own-warning-levels).

Pop-ups disappear after a few seconds; click one to dismiss it sooner. You can move them to another corner, turn off the connection ones, or change or turn off the warnings: see [Settings](#settings).

**While you're playing fullscreen:**
- **Exclusive fullscreen:** pop-ups can't appear over the game, so they're held and shown once it closes.
- **Borderless fullscreen:** pop-ups show over the game as normal. If you'd rather not be interrupted, turn on **Hold while a game is fullscreen** in Settings.

Held pop-ups show once the game closes, at most one per device, the latest for each. A held battery warning is dropped if the device was charged in the meantime.

**Sounds:** turn on **Play a sound** in Settings to hear battery warnings too: a soft two-note chime for low, and for critical, a quicker one that carries on to a lower third note. It plays the moment the warning happens, even while a fullscreen game holds the pop-up. Connection and fully charged pop-ups stay silent. To change how loud it is, set WattsLeft's volume in Windows' volume mixer.

## The right-click menu

| Item | What it does |
|---|---|
| Device lines | Each connected device and its reading. |
| **Refresh now** | Reads every device straight away, instead of waiting for the next check. |
| **Tray icons** | 1, 2, or one per device. |
| **Pop-up position** | Which corner pop-ups appear in. Choosing one shows a sample there. |
| **Connection pop-ups** | Turns the Connected and Disconnected pop-ups on or off. |
| **Battery warnings** | Turns both the low and critical warnings in Settings on or off together. Your warning levels are kept. Devices with [their own levels](devices.md#a-devices-own-warning-levels) keep them. |
| **Start with Windows** | Runs WattsLeft when you sign in. |
| **Settings** | Opens the settings window. |
| **Diagnostics** | Writes a report of what WattsLeft sees, for bug reports, and opens it. See [Reporting a bug](troubleshooting.md#reporting-a-bug). |
| **Exit** | Closes WattsLeft. |

The menu follows your taskbar's light or dark mode.

## Settings

Open it from the panel's gear or **Settings** in the right-click menu. It follows Windows' app mode, light or dark, and changes take effect straight away. The window has two pages: **Settings**, and **[Devices](devices.md)**.

| Setting | Default | Options |
|---|---|---|
| **Show** | All on | Controllers, mice, keyboards, headsets and other devices, each on or off. Kinds turned off get no icon, pop-ups or history |
| **Tray icons** | 1 | 1, 2, or one per device |
| **Position** | Top right | Any corner of the screen |
| **Connection pop-ups** | On | On or off |
| **Fully charged** | On | On or off |
| **Hold while a game is fullscreen** | Off | On or off. Pop-ups are always held in exclusive fullscreen, where they can't show |
| **Low battery** | On, 20% | On or off; 10% to 50%, in 5% steps |
| **Critical battery** | On, 10% | On or off; 5% to 45%, always below the low level |
| **Play a sound** | Off | On or off. Plays a chime with each battery warning |
| **Share battery levels** | Off | On or off. See [below](#sharing-battery-levels-with-other-apps) |
| **Start with Windows** | Off | On or off |

Switching a warning off greys out its level until you switch it back on. Lowering the low level below the critical one pushes the critical level down with it.

## Sharing battery levels with other apps

Turn on **Share battery levels** in Settings, and WattsLeft keeps `%LOCALAPPDATA%\WattsLeft\status.json` up to date with every connected device's battery. Other apps can read it to show your battery levels, such as a Rainmeter skin, a Stream Deck plugin or a script. It's a file on your PC, so nothing is sent anywhere.

```json
{
  "app": "WattsLeft",
  "format": 1,
  "version": "0.4.0",
  "running": true,
  "pid": 9696,
  "updated": "2026-10-22T12:41:09.5679406+11:00",
  "devices": [
    {
      "id": "ble:70f8ae8f7583",
      "name": "Xbox Wireless Controller",
      "deviceName": "Xbox Wireless Controller",
      "type": "controller",
      "percent": 62,
      "exact": true,
      "power": "battery",
      "connection": "bluetooth",
      "timeLeft": "3 h to 20%"
    }
  ]
}
```

| Field | Meaning |
|---|---|
| `format` | Goes up only if a field changes meaning or is removed. New fields can appear without it changing. |
| `running` | `false` once WattsLeft exits. If it crashed, this stays `true`: check that a process with the given `pid` is still running. |
| `updated` | When the file was last written. It's only rewritten when something changes, so it can be hours old while nothing does. |
| `devices` | Every connected device. Empty when none are. |
| `id` | Stays the same for each device, across connections and restarts. |
| `name` | The name WattsLeft shows: yours, if you've [renamed it](devices.md#renaming-a-device). |
| `deviceName` | The name the device gives itself. |
| `type` | `controller`, `mouse`, `keyboard`, `headset` or `other`. |
| `percent` | The battery level, or `null` if it isn't known yet. |
| `exact` | `false` when the device only reports a rough level (empty, low, medium or full), so `percent` is an estimate. |
| `power` | `battery`, `charging`, `wired` (plugged in with no battery reported) or `unknown`. |
| `connection` | `bluetooth`, `usb`, `wireless-adapter` or `unknown`. |
| `timeLeft` | The same estimate as the tooltip, such as `3 h to 20%` or `1 h 30 min to full`, or `null` until there's enough history. |

The file is replaced in one step, so an app never reads half of it. Turning the setting off deletes it.

---

[← Getting started](getting-started.md) · Next: [Devices →](devices.md)
