# Troubleshooting

[← Wiki home](README.md)

Each problem below has its likely cause and what to do. If yours isn't here, see [Reporting a bug](#reporting-a-bug).

- [I can't see the tray icon](#i-cant-see-the-tray-icon)
- [Windows says "Windows protected your PC"](#windows-says-windows-protected-your-pc)
- [It asks me to install .NET](#it-asks-me-to-install-net)
- [My controller doesn't appear](#my-controller-doesnt-appear)
- [It says "battery unknown" or shows "?"](#it-says-battery-unknown-or-shows-)
- [A game doesn't see my PlayStation controller](#a-game-doesnt-see-my-playstation-controller)
- [The charging bolt is late or missing on an Xbox controller](#the-charging-bolt-is-late-or-missing-on-an-xbox-controller)
- [The percentage only moves in 10% steps](#the-percentage-only-moves-in-10-steps)
- [Pop-ups don't appear while I'm playing](#pop-ups-dont-appear-while-im-playing)
- [Battery warnings don't make a sound](#battery-warnings-dont-make-a-sound)
- [The Controllers page says "Needs more data"](#the-controllers-page-says-needs-more-data)
- [There are gaps in the battery history](#there-are-gaps-in-the-battery-history)
- [Resetting everything](#resetting-everything)
- [Reporting a bug](#reporting-a-bug)

## I can't see the tray icon

**Cause:** Windows hides new tray icons in the overflow area, behind the **^** arrow.
**Fix:** see [Keeping the icon visible](getting-started.md#keeping-the-icon-visible). If it isn't in the overflow area either, ControllerHP isn't running: start `ControllerHP.exe` again.

## Windows says "Windows protected your PC"

**Cause:** ControllerHP isn't code-signed, so Windows SmartScreen warns about it.
**Fix:** choose **More info → Run anyway**. See [why this happens](getting-started.md#the-windows-protected-your-pc-warning).

## It asks me to install .NET

**Cause:** you downloaded the smaller zip, which needs the .NET 8 Desktop Runtime.
**Fix:** follow Windows' link to install it, or download the `-standalone` zip instead, which needs nothing else.

## My controller doesn't appear

**Causes and fixes:**
- **It isn't connected to Windows.** Check that it shows as *Connected* under Settings → Bluetooth & devices (Windows 11) or Settings → Devices (Windows 10), and that its light is steady rather than flashing.
- **It isn't supported.** See [Supported controllers](supported-controllers.md).
- **It only just connected.** Give it a few seconds, or choose **Refresh now** from the right-click menu.

If it's still missing, run `ControllerHP.exe --list` (see [Reporting a bug](#reporting-a-bug)) to see what each source reports.

## It says "battery unknown" or shows "?"

**Causes and fixes:**
- **It's a PlayStation controller held by another tool,** such as DS4Windows with HidHide, which stops other apps reading it. Close the tool, or turn off its exclusive mode.
- **It only just connected.** The first reading usually takes under a second, but give it a few.
- **The controller reported an error,** for example while charging when too hot or too cold. It should clear on its own.

## A game doesn't see my PlayStation controller

**Cause:** over Bluetooth, ControllerHP switches PlayStation controllers to their full input report so it can read the battery. Steam does the same. Games that use the older DirectInput interface, such as some emulators and older games, don't understand that report.
**Fix:**
- Best: play through Steam with Steam Input on, or use the game's built-in DualSense or DualShock 4 support. Both read the full report.
- Or connect the controller by USB, where this doesn't happen.
- Or, for that game only: exit ControllerHP, then turn the controller off and on. It reconnects in its basic mode. Note that ControllerHP switches it again the next time it sees the controller connect.

## The charging bolt is late or missing on an Xbox controller

**Cause:** over Bluetooth, Windows doesn't say whether an Xbox controller is charging, so ControllerHP works it out from the percentage rising. That takes a minute or two after you plug it in, and it stops showing at 100% or after 5 minutes without a rise.
**Fix:** none needed. Over USB or the Xbox Wireless Adapter, charging shows straight away.

## The percentage only moves in 10% steps

**Cause:** that's how DualSense and DualShock 4 controllers report their battery, so "85%" means somewhere between 80% and 89%.
**Fix:** none needed. The Controllers page's stats account for it.

## Pop-ups don't appear while I'm playing

**Causes and fixes:**
- **The game is in exclusive fullscreen**, which nothing can draw over. ControllerHP holds the pop-ups and shows them once the game closes. To see them during play, switch the game to borderless or windowed mode.
- **Hold while a game is fullscreen is on** in Settings, which also holds them in borderless games until the game closes.
- **The pop-up is turned off.** Check **Connection pop-ups** and **Battery warnings** in the right-click menu, or the levels in Settings.
- **Each warning only shows once per discharge.** It shows again after the controller charges.

## Battery warnings don't make a sound

**Causes and fixes:**
- **Play a sound is off.** It's off by default: turn it on under Battery warnings in Settings. Turning it on plays the low battery chime once, so you can check it.
- **ControllerHP is muted or turned down.** Check its entry in the Windows volume mixer: right-click the speaker icon in the taskbar and choose **Volume mixer**. It only appears there once ControllerHP has played a sound.

## The Controllers page says "Needs more data"

**Cause:** an honest figure needs at least 45 minutes of the controller on battery, with its level falling at least 5%. Battery health needs three weeks of history for its percentage.
**Fix:** keep using it. See [The stats](controllers.md#the-stats).

## There are gaps in the battery history

**Cause:** a gap means the controller was off, or ControllerHP wasn't running, for example before you signed in or after you exited it.
**Fix:** to record continuously, turn on **Start with Windows**.

## Resetting everything

1. Right-click the tray icon and choose **Exit**.
2. Delete the registry key `HKEY_CURRENT_USER\Software\ControllerHP` to reset the settings.
3. Delete `%LOCALAPPDATA%\ControllerHP` to clear the battery history and logs.
4. Start ControllerHP again.

## Reporting a bug

[Open a bug report](https://github.com/veloxity343/ControllerHP/issues/new/choose). The form asks for what helps most:

1. **Your controller and connection,** such as *DualSense over Bluetooth*.
2. **A diagnostics report.** With the controller connected, right-click the tray icon and choose **Diagnostics**. ControllerHP writes a report and opens it. It includes:
   - your ControllerHP and Windows versions, and your settings;
   - what each battery source sees, raw values included;
   - the recent battery log and any errors.

   Bluetooth addresses in it are partly masked, so you can paste it as it is, but have a quick look through first. The report is saved as `%LOCALAPPDATA%\ControllerHP\diagnostics.txt`.

If ControllerHP won't start, you can still get the battery part of the report: open the folder containing `ControllerHP.exe`, type `cmd` in File Explorer's address bar, press Enter, and run `ControllerHP.exe --list`.

Security problems are different: please report them privately, as described in [SECURITY.md](../../SECURITY.md).

---

[← Supported controllers](supported-controllers.md) · [Wiki home](README.md)
