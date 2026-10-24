# Getting started

[← Wiki home](README.md)

- [Requirements](#requirements)
- [Installing](#installing)
- [The "Windows protected your PC" warning](#the-windows-protected-your-pc-warning)
- [Keeping the icon visible](#keeping-the-icon-visible)
- [Starting with Windows](#starting-with-windows)
- [Updating](#updating)
- [Uninstalling](#uninstalling)

## Requirements

- Windows 10 version 1809 or later, or Windows 11, on a 64-bit (x64) PC.
- A supported controller, or a Bluetooth mouse, keyboard or headset: see [Supported devices](supported-devices.md).

## Installing

1. Open the [latest release](https://github.com/veloxity343/ControllerHP/releases/latest) and download `ControllerHP-x.y.z-win-x64.zip` (about 16 MB). Nothing else needs installing: .NET is built into it, so it keeps working whatever .NET versions come and go.
2. Extract the whole zip to a folder it can stay in. `%LOCALAPPDATA%\Programs\ControllerHP` is a good choice: paste that into File Explorer's address bar to create it. Keep the files together, as `ControllerHP.exe` needs the DLLs beside it, and don't run it from inside the zip.
3. Double-click `ControllerHP.exe`. There's no window: ControllerHP lives in the notification area, at the right-hand end of the taskbar.

ControllerHP doesn't need administrator rights and doesn't install anything.

## The "Windows protected your PC" warning

The first time you run it, Windows may show a blue **"Windows protected your PC"** box. Choose **More info**, then **Run anyway**.

This happens because ControllerHP isn't code-signed. Signing costs money every year, and Windows SmartScreen warns about any unsigned program until enough people have downloaded it. The warning doesn't mean anything was found wrong with it. If you'd like to check what you're running, the source is in this repository and every release is built from it by [GitHub Actions](https://github.com/veloxity343/ControllerHP/actions/workflows/ci.yml).

## Keeping the icon visible

Windows hides new tray icons in the overflow area (the **^** arrow) by default. To keep ControllerHP on the taskbar:

- **Windows 11:** Settings → Personalisation → Taskbar → **Other system tray icons**, and turn **ControllerHP** on.
- **Windows 10:** drag the icon from the overflow area onto the taskbar, or go to Settings → Personalisation → Taskbar → **Select which icons appear on the taskbar**.

## Starting with Windows

Right-click the tray icon and tick **Start with Windows**, or turn it on under **Settings** → Startup. It's off by default. It adds ControllerHP to your own startup apps only, not anyone else's on the PC.

If you move ControllerHP's folder later, turn this off and on again so Windows starts it from the new place.

## Updating

1. Right-click the tray icon and choose **Exit**.
2. Download the new release, delete the old files from ControllerHP's folder, and extract the new zip into the same folder. Keeping the same folder keeps the tray icon where you put it.
3. Run it.

Your settings and battery history are kept: they're stored separately from the program (see [what's stored](devices.md#whats-stored-on-your-pc)).

## Uninstalling

1. Right-click the tray icon, untick **Start with Windows**, then choose **Exit**.
2. Delete the folder you put `ControllerHP.exe` in.
3. Optionally, delete its data too:
   - The folder `%LOCALAPPDATA%\ControllerHP` (battery history and logs).
   - The registry key `HKEY_CURRENT_USER\Software\ControllerHP` (settings).

---

Next: [Using ControllerHP →](using-controllerhp.md)
