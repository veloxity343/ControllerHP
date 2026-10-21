# Contributing

Thanks for helping out. ControllerHP is small and Windows-only, so most contributions are one of three things: a bug report, results from a controller nobody has tested yet, or a pull request.

## Reporting a bug

Open an issue with:

- Your controller and how it's connected (USB, Bluetooth, Xbox Wireless Adapter).
- What the tray icon or pop-up showed, and what you expected.
- The output of `ControllerHP.exe --list`, run from a terminal in the install folder.
- Anything relevant from `%LOCALAPPDATA%\ControllerHP\error.log` and `history.log`. `history.log` records every reading change with a timestamp, which usually shows exactly when something went wrong.

For security problems, follow [SECURITY.md](SECURITY.md) instead of opening an issue.

## Building

You need the .NET 8 SDK (`global.json` pins the feature band). Open `ControllerHP.sln` in Visual Studio or Rider, or:

```bash
dotnet build ControllerHP.sln
```

The project builds on Linux too (`EnableWindowsTargeting`), which is how CI runs, but it only runs on Windows.

## Testing your change

There are no automated tests; changes are checked against real controllers. The diagnostics help:

| Flag | Use it to |
|---|---|
| `--list` | See what every battery source reports, raw values included |
| `--preview <file.png>` | Check icon and pop-up rendering without a controller |
| `--demo` | Watch the sample pop-ups on screen |
| `--hid-probe` | Dump raw HID reports, when working out a new controller's format |

In your pull request, say which controllers and connections you tried. If you changed how something looks, attach a `--preview` image.

## Adding a controller

Each controller family is an `IBatterySource` in `src/ControllerHP/Battery`:

- **Xbox:** `GamingInputSource` (Windows.Gaming.Input, plus Bluetooth LE for the real percentage) and `XInputSource` as a fallback. These see the same controllers, so they're grouped in a `FirstAvailableSource`.
- **PlayStation:** `PlayStationSource` reads the battery byte from the DualSense and DualShock 4 HID input reports.

A new family, such as the Switch Pro Controller (VID `057E`), would follow `PlayStationSource`: watch for the device, open it briefly, parse its input report, then add the source in `Program.cs`. Note where the report format came from in a comment, since it can't be checked without the hardware.

## Pull requests

- Branch from `main`, and keep each pull request to one change.
- Match the surrounding code: its naming, its layout, and comments that explain *why* rather than *what*.
- The CI build must pass before merging.
- Update the README if you change what users see or which controllers are supported.

## License

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE).
