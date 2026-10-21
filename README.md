# ControllerHP

Your controller's hit points, in the Windows taskbar.

ControllerHP puts a battery meter in the notification area for each connected game controller: the percentage on top, a meter underneath whose fill and colour track the charge (green → yellow → red), and a bolt through it while charging. Slim pop-ups in the top-right corner tell you when a controller connects or disconnects, and when its battery drops to 20% and 10%. Right-click the tray icon to move them to another corner or turn off the connect/disconnect ones.

![Icon states and pop-ups](docs/icon-preview.png)

## Install

Download the latest zip from [Releases](../../releases), extract `ControllerHP.exe` anywhere (e.g. `%LOCALAPPDATA%\Programs\ControllerHP`), and run it. Right-click the tray icon and tick **Start with Windows** to keep it running.

| Download | Size | Needs |
|---|---|---|
| `ControllerHP-x.y.z-win-x64.zip` | ~6 MB | [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (Windows offers the download link if it's missing) |
| `ControllerHP-x.y.z-win-x64-standalone.zip` | ~72 MB | Nothing |

Requires Windows 10 1809 or later.

## Supported controllers

| Controller | Connection | Battery | Charging |
|---|---|---|---|
| Xbox One / Series | Bluetooth | Exact % | Inferred from the % rising (a minute or two after plugging in) |
| Xbox One / Series | USB, Xbox Wireless Adapter | Exact % or 4 levels | Yes |
| DualSense / DualSense Edge | USB, Bluetooth | 10% steps | Yes |
| DualShock 4 | USB, Bluetooth | 10% steps | Yes |

### Known limitations

- Pop-ups can't appear over games running in exclusive fullscreen (borderless and windowed are fine).
- Over Bluetooth, Windows exposes no charging flag for Xbox controllers, so charging is inferred: the bolt appears at the first percentage rise and clears at 100% or after 5 minutes without one.
- Two identical Xbox controllers, one on Bluetooth and one not, may have their readings swapped.
- Over Bluetooth, PlayStation controllers only report their battery after being switched to their full input report, which ControllerHP does when one connects (as Steam does). Until the controller reconnects, games that read it through DirectInput (some emulators and older games) don't see its input. Steam Input and games with native DualSense / DualShock 4 support are unaffected.
- PlayStation controllers held exclusively by another tool (e.g. DS4Windows with HidHide) show as "battery unknown".

## How batteries are read

For Xbox controllers, sources are tried in order, and the first one that finds any controller is used:

| Source | Covers | Detail |
|---|---|---|
| `Windows.Gaming.Input` | Xbox controllers over USB, Xbox Wireless Adapter, Bluetooth | Percentage + charging state; finds controllers and raises connect/disconnect events |
| ↳ Bluetooth LE battery | Xbox controllers over Bluetooth | Overrides the percentage above, which is wrong over Bluetooth (always 100/1000 mWh). Read from the same devnode property the Settings app shows, watched for instant updates. |
| `XInput` | Xbox controllers via USB / Wireless Adapter (not Bluetooth) | 4 coarse levels, no charging state |

PlayStation controllers are read separately, so they show alongside Xbox ones:

| Source | Covers | Detail |
|---|---|---|
| Raw HID input reports | DualSense, DualSense Edge, DualShock 4 over USB and Bluetooth | Battery level and charging state from the controller's status byte, sampled every 5 seconds |

Connects and disconnects are event-driven; a 30-second poll only catches battery drift on Xbox sources without change events.

## Development

Requires the .NET 8 SDK (`global.json` pins the feature band). Open `ControllerHP.sln` in Visual Studio or Rider, or build from the command line:

```bash
dotnet build ControllerHP.sln
```

### From WSL

```bash
./deploy.sh
```

This publishes the app, installs it to `%LOCALAPPDATA%\Programs\ControllerHP` and restarts it. Don't run it straight out of `publish/` over `\\wsl.localhost`. Windows caches stale DLLs from that path, and "Start with Windows" would point at a location that doesn't exist until WSL starts.

### Diagnostics

| Flag | What it does |
|---|---|
| `--list` | Prints what every battery source reports for each controller, raw values included |
| `--preview <file.png>` | Renders every icon state and pop-up to an image |
| `--demo` | Plays the sample pop-ups on screen |
| `--hid-probe` | Dumps the raw HID reports of connected Xbox and PlayStation controllers, and Xbox GATT services |

Unhandled errors are logged to `%LOCALAPPDATA%\ControllerHP\error.log`, and every reading change is logged with a timestamp to `history.log` in the same folder.

### Releasing

Push a version tag and GitHub Actions builds both zips and publishes a release with generated notes. The version number comes from the tag, so nothing needs editing first.

```bash
git tag v0.1.0
git push origin v0.1.0
```

Tags with a suffix (e.g. `v0.2.0-beta.1`) are published as pre-releases. If a release build fails, fix it, then move the tag and push again:

```bash
git tag -f v0.1.0
git push -f origin v0.1.0
```

### Adding other controllers

PlayStation and Switch controllers aren't XInput devices, and Windows doesn't expose their battery through a standard API. Each needs its own `IBatterySource` that opens the HID device and parses the vendor's input report:

- **DualShock 4 / DualSense** (VID `054C`): battery level and charging flag are packed into a byte of the input report, at a different offset for USB vs Bluetooth.
- **Switch Pro Controller** (VID `057E`): battery nibble in the standard input report, after a handshake.

## License

[MIT](LICENSE)
