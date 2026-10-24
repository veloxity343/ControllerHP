# Devices

[← Wiki home](README.md)

- [The chart](#the-chart)
- [The stats](#the-stats)
- [Battery health](#battery-health)
- [Renaming a device](#renaming-a-device)
- [A device's own warning levels](#a-devices-own-warning-levels)
- [Removing a device](#removing-a-device)
- [What's stored on your PC](#whats-stored-on-your-pc)

The Devices page shows each device's battery history and stats, and lets you rename devices, give them their own warning levels, or remove ones you no longer use. Open **Settings** from the right-click menu (or the gear in the panel) and choose **Devices**.

## The chart

One line per device, over the last **2 hours**, **6 hours** (the default), **24 hours** or **7 days**.

- While a device is on and WattsLeft is running, its line carries on to *now*, even when the level hasn't changed.
- A **gap** means the device was off, or WattsLeft wasn't running.
- A connected device's line ends in a dot with its current level.
- A very short connection that's too brief to draw as a line shows as a small dot.
- **Hover** over the chart to see every device's level at that moment, and whether it was charging.
- **Click a name** in the legend above the chart to leave that device's line out, for example a keyboard you don't need to watch. Its name stays in the legend, dimmed, so you can click it back. This only changes the chart: its history is still recorded, and its card and stats still show.

Each device keeps its colour whichever range you pick. Every line is also named in the legend, so you never need to tell devices apart by colour alone.

History builds up from when you install WattsLeft, so the chart starts out short.

## The stats

Below the chart is a card of figures. By default it's for the device with the **lowest battery**, and it keeps following whichever is lowest until you pick a device from the dropdown, or **All devices** for one card each.

| Figure | What it means | How it's worked out |
|---|---|---|
| **Until 20%** | How long until the device reaches your low warning level (or empty, if that warning is off). | Current level ÷ its average drain over the last week. Shown while it's on battery. |
| **Until full** | How long until it's fully charged. | The room left ÷ how fast it has charged before. Shown while charging. Charging slows near full, so treat it as rough. |
| **Battery health** | See [below](#battery-health). | |
| **Average drain** | How quickly it uses its battery, in % per hour. | Over the range shown in the chart, while on battery. |
| **Play time** | Time spent on battery in the range, and today. | Any time it was on and not charging, including when it was on but idle. |
| **Charge cycles** | Battery used in the range, as whole charges: using 50% twice is one cycle. | Total % used ÷ 100. This is *not* a measure of wear by itself, and health doesn't use it. |
| **Last charged** | When it last finished charging, and to what level. | |
| **Drops to 20%** | How many times it ran down to your low warning level in the range. | |

**"Needs more data"** means there isn't enough history yet to give an honest figure. A rate needs at least 45 minutes of the device on battery, with its level falling at least 5%. PlayStation controllers report in 10% steps, so they need a step or two before anything shows.

Drain and charge rates are measured between the moments the level actually *changes*, not from when a device connects. That way, a device that connects partway through a 10% step, or one sitting full on a cable, doesn't distort them.

## Battery health

Battery health compares **how long a charge lasts on battery now** with a baseline.

- **For the first three weeks**, there isn't enough history for a baseline, so it shows the estimated runtime on a full charge. For these controllers, it also shows the range commonly reported for the model:

  | Model | Commonly reported runtime |
  |---|---|
  | Xbox Wireless Controller | 30–40 hours (AA batteries and rechargeable packs differ) |
  | DualSense | 6–12 hours |
  | DualShock 4 | 4–8 hours |
  | DualSense Edge | No widely reported figure, so none is shown |

  These are commonly reported figures, not official ones.

- **After three weeks**, it shows a percentage: the device's runtime now, compared with its own runtime in its first week of tracking. For example, *90%: 8 h per charge, was 9 h*.

**Treat it as a rough guide, not a diagnosis.**
- How you use it changes how long a charge lasts, so health moves around: on a controller, vibration, the light bar, headset audio and the games you play; on a keyboard, its backlight.
- Time spent charging, including playing with the cable plugged in, is left out.
- Charge cycles are left out too, so a device that's simply used a lot isn't marked as worn.

Hover the **?** beside it in the app for a short version of this.

## Renaming a device

Two devices of the same model, such as two Xbox controllers, share a name. The Devices page numbers them, *Xbox Wireless Controller (1)* and *(2)*, but in the tray and pop-ups they look the same. To tell them apart everywhere, give each a name of your own, such as *Black pad* or *Player 2*.

Choose the device in the dropdown, click **Rename** on its card, type a name and press Enter. Names can be up to 40 characters.

The new name shows everywhere WattsLeft shows that device: the tray tooltip, the panel, pop-ups, the chart and the [status file](using-wattsleft.md#sharing-battery-levels-with-other-apps). It doesn't change the name Windows uses. To go back to the device's own name, rename it again and leave the box empty.

## A device's own warning levels

Every device follows the warning levels in Settings (20% and 10% by default). To warn earlier for one that runs down fast, or not at all for one that's always on a cable, give it levels of its own.

Each device's card has a **Warnings** section, beside **Drops to**, with its **Low** and **Critical** levels. Change either one, and that device's levels override Settings: the line underneath changes from *Inherited from Settings* to *Overridden*. Step a level below its lowest value to turn that warning off, or use the switch beside the **Warnings** label to turn both off for that device. As in Settings, the critical level always stays below the low one.

Overridden levels are used for its warning pop-ups, the time left in the tooltip and panel, and its **Until** and **Drops to** figures on this page. Changing the levels in Settings, or turning them off from the right-click menu, doesn't change them. To have it follow Settings again, click **Reset**.

## Removing a device

If you no longer use a device, such as one you've sold or a friend's, you can remove it from the Devices page: choose it in the dropdown, click **Remove** on its card, and confirm. **Cancel** is selected by default, so pressing Enter or Esc by accident won't remove anything.

It disappears from the chart, the dropdown and the stats. For a few seconds an **Undo** bar appears at the top of the page. If it's connected, it also leaves the tray, the panel and the menu, and gets no pop-ups, until it next connects; its history keeps recording in the meantime, so restoring it leaves no gap.

Removing **hides** a device; it doesn't delete anything:
- Its history stays on your PC until it expires after 90 days.
- **Restore removed**, beside the dropdown on the Devices page, brings it back. The button shows how many are removed, and opens a list to pick from, with **Restore all** when there's more than one.
- The next time it connects, it comes back by itself. That includes WattsLeft starting while it's connected.

## What's stored on your PC

Nothing leaves your PC: WattsLeft makes no network connections at all.

| What | Where | Kept for | Typical size |
|---|---|---|---|
| Battery history | `%LOCALAPPDATA%\WattsLeft\battery.csv` | 90 days | Up to about 2 MB per heavily used Xbox controller, much less for others |
| "Still running" time, used to close history neatly after a crash | `%LOCALAPPDATA%\WattsLeft\battery.alive` | Overwritten every 30 seconds | A few bytes |
| Readable change log, for bug reports | `%LOCALAPPDATA%\WattsLeft\history.log` | 30 days | Under 1 MB |
| Errors, if any | `%LOCALAPPDATA%\WattsLeft\error.log` | Newest 1 MB | Usually a few KB |
| Battery levels for other apps, if **Share battery levels** is on | `%LOCALAPPDATA%\WattsLeft\status.json` | Deleted when you turn it off | A few hundred bytes |
| The last diagnostics report, if you've made one | `%LOCALAPPDATA%\WattsLeft\diagnostics.txt` | Replaced each time | A few KB |
| Settings | Registry: `HKEY_CURRENT_USER\Software\WattsLeft` | Until you change them | Tiny |

Older entries are trimmed each time WattsLeft starts. A row is written only when a reading changes, so a device sitting at the same level writes nothing.

`battery.csv` is plain text, and you can open it in Excel or any spreadsheet. To clear your history, exit WattsLeft and delete it.

---

[← Using WattsLeft](using-wattsleft.md) · Next: [Supported devices →](supported-devices.md)
