# Controllers

[← Wiki home](README.md)

- [The chart](#the-chart)
- [The stats](#the-stats)
- [Battery health](#battery-health)
- [Removing a controller](#removing-a-controller)
- [What's stored on your PC](#whats-stored-on-your-pc)

The Controllers page shows each controller's battery history and stats, and lets you remove controllers you no longer use. Open **Settings** from the right-click menu (or the gear in the panel) and choose **Controllers**.

## The chart

One line per controller, over the last **2 hours**, **6 hours** (the default), **24 hours** or **7 days**.

- While a controller is on and ControllerHP is running, its line carries on to *now*, even when the level hasn't changed.
- A **gap** means the controller was off, or ControllerHP wasn't running.
- A connected controller's line ends in a dot with its current level.
- A very short connection that's too brief to draw as a line shows as a small dot.
- **Hover** over the chart to see every controller's level at that moment, and whether it was charging.

Each controller keeps its colour whichever range you pick. Every line is also named in the legend, so you never need to tell controllers apart by colour alone.

History builds up from when you install ControllerHP, so the chart starts out short.

## The stats

Below the chart is a card of figures. By default it's for the controller with the **lowest battery**, and it keeps following whichever is lowest until you pick a controller from the dropdown, or **All controllers** for one card each.

| Figure | What it means | How it's worked out |
|---|---|---|
| **Until 20%** | How long until the controller reaches your low warning level (or empty, if that warning is off). | Current level ÷ its average drain over the last week. Shown while it's on battery. |
| **Until full** | How long until it's fully charged. | The room left ÷ how fast it has charged before. Shown while charging. Charging slows near full, so treat it as rough. |
| **Battery health** | See [below](#battery-health). | |
| **Average drain** | How quickly it uses its battery, in % per hour. | Over the range shown in the chart, while on battery. |
| **Play time** | Time spent on battery in the range, and today. | Any time it was on and not charging, including when it was on but idle. |
| **Charge cycles** | Battery used in the range, as whole charges: using 50% twice is one cycle. | Total % used ÷ 100. This is *not* a measure of wear by itself, and health doesn't use it. |
| **Last charged** | When it last finished charging, and to what level. | |
| **Drops to 20%** | How many times it ran down to your low warning level in the range. | |

**"Needs more data"** means there isn't enough history yet to give an honest figure. A rate needs at least 45 minutes of the controller on battery, with its level falling at least 5%. PlayStation controllers report in 10% steps, so they need a step or two before anything shows.

Drain and charge rates are measured between the moments the level actually *changes*, not from when a controller connects. That way, a controller that connects partway through a 10% step, or one sitting full on a cable, doesn't distort them.

## Battery health

Battery health compares **how long a charge lasts on battery now** with a baseline.

- **For the first three weeks**, there isn't enough history for a baseline, so it shows the estimated runtime on a full charge, next to the range commonly reported for that model:

  | Model | Commonly reported runtime |
  |---|---|
  | Xbox Wireless Controller | 30–40 hours (AA batteries and rechargeable packs differ) |
  | DualSense | 6–12 hours |
  | DualShock 4 | 4–8 hours |
  | DualSense Edge | No widely reported figure, so none is shown |

  These are commonly reported figures, not official ones.

- **After three weeks**, it shows a percentage: the controller's runtime now, compared with its own runtime in its first week of tracking. For example, *90%: 8 h per charge, was 9 h*.

**Treat it as a rough guide, not a diagnosis.**
- Vibration, the light bar, headset audio and the games you play all change how long a charge lasts, so health moves around.
- Time spent charging, including playing with the cable plugged in, is left out.
- Charge cycles are left out too, so a controller that's simply used a lot isn't marked as worn.

Hover the **?** beside it in the app for a short version of this.

## Removing a controller

If you no longer use a controller, such as one you've sold or a friend's, you can remove it from the Controllers page: choose it in the dropdown, click **Remove** on its card, and confirm. **Cancel** is selected by default, so pressing Enter or Esc by accident won't remove anything.

It disappears from the chart, the dropdown and the stats. For a few seconds an **Undo** bar appears at the top of the page. If it's connected, it stays in the tray while it's on, because it's still in use.

Removing **hides** a controller; it doesn't delete anything:
- Its history stays on your PC until it expires after 90 days.
- **Restore removed**, beside the dropdown on the Controllers page, brings it back. The button shows how many are removed, and opens a list to pick from, with **Restore all** when there's more than one.
- The next time it connects, it comes back by itself. That includes ControllerHP starting while it's connected.

## What's stored on your PC

Nothing leaves your PC: ControllerHP makes no network connections at all.

| What | Where | Kept for | Typical size |
|---|---|---|---|
| Battery history | `%LOCALAPPDATA%\ControllerHP\battery.csv` | 90 days | Up to about 2 MB per heavily used Xbox controller, much less for others |
| "Still running" time, used to close history neatly after a crash | `%LOCALAPPDATA%\ControllerHP\battery.alive` | Overwritten every 30 seconds | A few bytes |
| Readable change log, for bug reports | `%LOCALAPPDATA%\ControllerHP\history.log` | 30 days | Under 1 MB |
| Errors, if any | `%LOCALAPPDATA%\ControllerHP\error.log` | Newest 1 MB | Usually a few KB |
| Battery levels for other apps, if **Share battery levels** is on | `%LOCALAPPDATA%\ControllerHP\status.json` | Deleted when you turn it off | A few hundred bytes |
| The last diagnostics report, if you've made one | `%LOCALAPPDATA%\ControllerHP\diagnostics.txt` | Replaced each time | A few KB |
| Settings | Registry: `HKEY_CURRENT_USER\Software\ControllerHP` | Until you change them | Tiny |

Older entries are trimmed each time ControllerHP starts. A row is written only when a reading changes, so a controller sitting at the same level writes nothing.

`battery.csv` is plain text, and you can open it in Excel or any spreadsheet. To clear your history, exit ControllerHP and delete it.

---

[← Using ControllerHP](using-controllerhp.md) · Next: [Supported controllers →](supported-controllers.md)
