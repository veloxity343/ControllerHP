# Using ControllerHP

[← Wiki home](README.md)

- [The tray icon](#the-tray-icon)
- [Several controllers](#several-controllers)
- [The controller panel](#the-controller-panel)
- [Pop-ups](#pop-ups)
- [The right-click menu](#the-right-click-menu)
- [Settings](#settings)

## The tray icon

| The icon shows | Meaning |
|---|---|
| A number above a meter | The battery percentage. The meter's colour shifts from green (75% and up) through yellow (50%) to red (20% and below). |
| A bolt through the meter | Charging. |
| **USB** with a bolt | Plugged in, with no battery reported (a wired-only controller). |
| **?** | Connected, but the battery level isn't known yet. |
| The ControllerHP gamepad | No controllers connected. |

Hover over the icon for the controller's name and its exact reading, for example *DualSense Wireless Controller: 85%, charging*.

The icon follows your taskbar's light or dark mode.

## Several controllers

By default there's **one** tray icon, showing the controller that most needs attention:

1. Controllers on battery, lowest first.
2. Then any whose level isn't known yet.
3. Then controllers that are charging or wired, since they're fine.

Hovering the icon adds a line listing the rest, such as *+1 more: Xbox Wireless Controller: 62%*.

To stop two similar batteries swapping places with every 1% change, a controller only takes over the icon once it's 3 points lower than the one shown.

To see more icons, change **Tray icons** to **2** or **One per controller** in the right-click menu or in Settings. The icons stay in the same place in the taskbar as controllers come and go.

## The controller panel

Left-click the tray icon to open a panel listing **every** connected controller, including any that don't have a tray icon of their own. Each shows its battery, whether it's charging, and how it's connected:

> **Xbox Wireless Controller**
> 62% · Bluetooth

The panel lists controllers lowest battery first. The gear at the top right opens [Settings](#settings). Click anywhere else, press Esc or click the tray icon again to close it.

## Pop-ups

Slim pop-ups appear in the top-right corner of the screen:

| Pop-up | When |
|---|---|
| **Connected** | A controller connects. Controllers already on when ControllerHP starts don't get one. |
| **Disconnected** | A controller turns off or disconnects. |
| **Battery low** | A controller on battery drops to the low level (20% by default). |
| **Battery critical** | It drops to the critical level (10% by default). |

Each warning shows once per discharge. It can show again after the controller charges, or after its level recovers by 5 points or more.

Pop-ups disappear after a few seconds; click one to dismiss it sooner. You can move them to another corner, turn off the connection ones, or change or turn off the warnings: see [Settings](#settings).

Pop-ups can't appear over games running in *exclusive* fullscreen. Borderless and windowed modes are fine.

## The right-click menu

| Item | What it does |
|---|---|
| Controller lines | Each connected controller and its reading. |
| **Refresh now** | Reads every controller straight away, instead of waiting for the next check. |
| **Tray icons** | 1, 2, or one per controller. |
| **Pop-up position** | Which corner pop-ups appear in. Choosing one shows a sample there. |
| **Connection pop-ups** | Turns the Connected and Disconnected pop-ups on or off. |
| **Battery warnings** | Turns both the low and critical warnings on or off together. Your warning levels are kept. |
| **Start with Windows** | Runs ControllerHP when you sign in. |
| **Settings…** | Opens the settings window. |
| **Exit** | Closes ControllerHP. |

The menu follows your taskbar's light or dark mode.

## Settings

Open it from the panel's gear or **Settings…** in the right-click menu. It follows Windows' app mode, light or dark, and changes take effect straight away. The window has two pages: **Settings**, and **[Controllers](controllers.md)**.

| Setting | Default | Options |
|---|---|---|
| **Tray icons** | 1 | 1, 2, or one per controller |
| **Position** | Top right | Any corner of the screen |
| **Connection pop-ups** | On | On or off |
| **Low battery** | On, 20% | On or off; 10% to 50%, in 5% steps |
| **Critical battery** | On, 10% | On or off; 5% to 45%, always below the low level |
| **Start with Windows** | Off | On or off |

Switching a warning off greys out its level until you switch it back on. Lowering the low level below the critical one pushes the critical level down with it.

---

[← Getting started](getting-started.md) · Next: [Controllers →](controllers.md)
