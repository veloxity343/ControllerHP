# Diagnostics

[← Developer docs](README.md)

`ControllerHP.exe` takes a few flags for checking controllers and rendering the UI. Run them from a terminal in the folder containing the exe. None of them start the tray app or change any settings.

| Flag | What it does |
|---|---|
| `--list` | Prints what every battery source reports for each connected controller, raw values included. The first thing to ask for in a bug report. |
| `--hid-probe` | Dumps the raw HID reports of connected Xbox and PlayStation controllers, and Xbox Bluetooth GATT services. For working out a new controller's report format. |
| `--preview <file.png>` | Renders every tray icon state, the pop-ups and the controller panel, in both modes, to an image. |
| `--preview-settings <file.png>` | Renders each page of the settings window, in dark and light mode, to an image. Uses sample battery history, so it works without controllers. |
| `--demo` | Shows the sample pop-ups on screen, one after another. |

## Logs

In `%LOCALAPPDATA%\ControllerHP`:

| File | Contents |
|---|---|
| `history.log` | Every reading change, with a timestamp. The quickest way to see when something went wrong. Kept 30 days. |
| `error.log` | Unhandled errors, and each distinct source failure, logged once. Kept to the newest 1 MB. |
| `battery.csv` | One row per change per controller: time, id, name, percent, charge state. Kept 90 days. |
| `battery.alive` | The last time the app was known to be running. |

## Checking a change

There are no automated tests; changes are checked against real controllers. In your pull request, say which controllers and connections you tried. If you changed how something looks, attach a `--preview` or `--preview-settings` image.

---

[← Architecture](architecture.md) · Next: [Adding a controller →](adding-a-controller.md)
