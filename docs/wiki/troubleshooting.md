# Troubleshooting

[← Wiki home](README.md)

Each problem below has its likely cause and what to do. If yours isn't here, see [Reporting a bug](#reporting-a-bug).

- [I can't see the tray icon](#i-cant-see-the-tray-icon)
- [Windows says "Windows protected your PC"](#windows-says-windows-protected-your-pc)
- [It asks me to install .NET](#it-asks-me-to-install-net)
- [My device doesn't appear](#my-device-doesnt-appear)
- [It says "battery unknown" or shows "?"](#it-says-battery-unknown-or-shows-)
- [A game doesn't see my PlayStation controller](#a-game-doesnt-see-my-playstation-controller)
- [The charging bolt is late, missing or wrong over Bluetooth](#the-charging-bolt-is-late-missing-or-wrong-over-bluetooth)
- [The percentage only moves in 10% steps](#the-percentage-only-moves-in-10-steps)
- [Pop-ups don't appear while I'm playing](#pop-ups-dont-appear-while-im-playing)
- [Battery warnings don't make a sound](#battery-warnings-dont-make-a-sound)
- [The Devices page says "Needs more data"](#the-devices-page-says-needs-more-data)
- [There are gaps in the battery history](#there-are-gaps-in-the-battery-history)
- [Resetting everything](#resetting-everything)
- [Reporting a bug](#reporting-a-bug)

## I can't see the tray icon

**Cause:** Windows hides new tray icons in the overflow area, behind the **^** arrow.
**Fix:** see [Keeping the icon visible](getting-started.md#keeping-the-icon-visible). If it isn't in the overflow area either, WattsLeft isn't running: start `WattsLeft.exe` again.

## Windows says "Windows protected your PC"

**Cause:** WattsLeft isn't code-signed, so Windows SmartScreen warns about it.
**Fix:** choose **More info → Run anyway**. See [why this happens](getting-started.md#the-windows-protected-your-pc-warning).

## It won't start, or says a DLL is missing

**Cause:** `WattsLeft.exe` was copied out of the zip on its own, or run from inside the zip. It needs the DLLs that come with it (they draw the panel and windows).
**Fix:** extract the whole zip to a folder, and run `WattsLeft.exe` from there.

## My device doesn't appear

**Causes and fixes:**
- **It isn't connected to Windows.** Check that it shows as *Connected* under Settings → Bluetooth & devices (Windows 11) or Settings → Devices (Windows 10). On a controller, check its light is steady rather than flashing.
- **Its kind is turned off.** Check **Show** in Settings: if, say, Keyboards is off, no keyboard appears.
- **Windows shows no battery for it.** For Bluetooth mice, keyboards and headsets, WattsLeft reads the level Windows records. If Settings → Bluetooth & devices shows no battery beside it, WattsLeft has none to show.
- **It uses a USB receiver (dongle) that isn't supported.** So far that's Logitech's, and WLmouse, LAMZU and G-Wolves mice. If the device also has Bluetooth, use that instead: its battery shows if Windows shows it.
- **It isn't supported.** See [Supported devices](supported-devices.md).
- **It only just connected.** Give it a few seconds, or choose **Refresh now** from the right-click menu.

If it's still missing, run `WattsLeft.exe --list` (see [Reporting a bug](#reporting-a-bug)) to see what each source reports.

## It says "battery unknown" or shows "?"

**Causes and fixes:**
- **It's a PlayStation controller held by another tool,** such as DS4Windows with HidHide, which stops other apps reading it. Close the tool, or turn off its exclusive mode.
- **It only just connected.** The first reading usually takes under a second, but give it a few.
- **The controller reported an error,** for example while charging when too hot or too cold. It should clear on its own.

## A game doesn't see my PlayStation controller

**Cause:** over Bluetooth, WattsLeft switches PlayStation controllers to their full input report so it can read the battery. Steam does the same. Games that use the older DirectInput interface, such as some emulators and older games, don't understand that report.
**Fix:**
- Best: play through Steam with Steam Input on, or use the game's built-in DualSense or DualShock 4 support. Both read the full report.
- Or connect the controller by USB, where this doesn't happen.
- Or, for that game only: exit WattsLeft, then turn the controller off and on. It reconnects in its basic mode. Note that WattsLeft switches it again the next time it sees the controller connect.

## The charging bolt is late, missing or wrong over Bluetooth

**Cause:** over Bluetooth, Windows doesn't say whether an Xbox controller, mouse, keyboard or headset is charging, so WattsLeft works it out from the level rising. It waits for a rise of 2 points, since a battery that's been resting can tick up a point on its own. So on a device that reports in 1% steps, the bolt shows a few minutes after you plug it in. It goes at 100%, when the level drops, or after 5 minutes without a rise. See [Charging over Bluetooth](supported-devices.md#charging-over-bluetooth) for the details.
**Fix:** none needed; it catches up. An Xbox controller on USB or the Xbox Wireless Adapter shows charging straight away. If the bolt shows while a device isn't charging, please [report it](#reporting-a-bug) with your `history.log`, which records each step.

## The percentage only moves in 10% steps

**Cause:** that's how DualSense and DualShock 4 controllers report their battery, so "85%" means somewhere between 80% and 89%.
**Fix:** none needed. The Devices page's stats account for it.

## Pop-ups don't appear while I'm playing

**Causes and fixes:**
- **The game is in exclusive fullscreen**, which nothing can draw over. WattsLeft holds the pop-ups and shows them once the game closes. To see them during play, switch the game to borderless or windowed mode.
- **Hold while a game is fullscreen is on** in Settings, which also holds them in borderless games until the game closes.
- **The pop-up is turned off.** Check **Connection pop-ups** and **Battery warnings** in the right-click menu, or the levels in Settings.
- **Each warning only shows once per discharge.** It shows again after the device charges.

## Battery warnings don't make a sound

**Causes and fixes:**
- **Play a sound is off.** It's off by default: turn it on under Battery warnings in Settings. Turning it on plays the low battery chime once, so you can check it.
- **WattsLeft is muted or turned down.** Check its entry in the Windows volume mixer: right-click the speaker icon in the taskbar and choose **Volume mixer**. It only appears there once WattsLeft has played a sound.

## The Devices page says "Needs more data"

**Cause:** an honest figure needs at least 45 minutes of the device on battery, with its level falling at least 5%. Battery health needs three weeks of history for its percentage.
**Fix:** keep using it. See [The stats](devices.md#the-stats).

## There are gaps in the battery history

**Cause:** a gap means the device was off, or WattsLeft wasn't running, for example before you signed in or after you exited it.
**Fix:** to record continuously, turn on **Start with Windows**.

## Resetting everything

1. Right-click the tray icon and choose **Exit**.
2. Delete the registry key `HKEY_CURRENT_USER\Software\WattsLeft` to reset the settings.
3. Delete `%LOCALAPPDATA%\WattsLeft` to clear the battery history and logs.
4. Start WattsLeft again.

## Reporting a bug

[Open a bug report](https://github.com/veloxity343/WattsLeft/issues/new/choose). The form asks for what helps most:

1. **Your device and connection,** such as *DualSense over Bluetooth* or *a Logitech mouse on its USB receiver*.
2. **A diagnostics report.** With the device connected, right-click the tray icon and choose **Diagnostics**. WattsLeft writes a report and opens it. It includes:
   - your WattsLeft and Windows versions, and your settings;
   - what each battery source sees, raw values included;
   - the recent battery log and any errors.

   Bluetooth addresses in it are partly masked, so you can paste it as it is, but have a quick look through first. The report is saved as `%LOCALAPPDATA%\WattsLeft\diagnostics.txt`.

If WattsLeft won't start, you can still get the battery part of the report: open the folder containing `WattsLeft.exe`, type `cmd` in File Explorer's address bar, press Enter, and run `WattsLeft.exe --list`.

Security problems are different: please report them privately, as described in [SECURITY.md](../../SECURITY.md).

---

[← Supported devices](supported-devices.md) · [Wiki home](README.md)
