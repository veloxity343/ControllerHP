# ControllerHP

Your controller's hit points, in the Windows taskbar.

ControllerHP puts a battery meter in the notification area for each connected game controller: the percentage on top, a meter underneath whose fill and colour track the charge (green → yellow → red), and a bolt through it while charging. Small pop-ups in the bottom-right corner tell you when a controller connects or disconnects, and when its battery drops to 20% and 10%.

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
| DualSense / DualShock 4 | — | Planned | Planned |

### Known limitations

- Pop-ups can't appear over games running in exclusive fullscreen (borderless and windowed are fine).
- Over Bluetooth, Windows exposes no charging flag for Xbox controllers, so charging is inferred: the bolt appears at the first percentage rise and clears at 100% or after 5 minutes without one.
- Two identical controllers, one on Bluetooth and one not, may have their readings swapped.

## How batteries are read

Sources are tried in order, and the first one that finds any controller is used:

| Source | Covers | Detail |
|---|---|---|
| `Windows.Gaming.Input` | Xbox controllers over USB, Xbox Wireless Adapter, Bluetooth | Percentage + charging state; finds controllers and raises connect/disconnect events |
| ↳ Bluetooth LE battery | Xbox controllers over Bluetooth | Overrides the percentage above, which is wrong over Bluetooth (always 100/1000 mWh). Read from the same devnode property the Settings app shows, watched for instant updates. |
| `XInput` | Xbox controllers via USB / Wireless Adapter (not Bluetooth) | 4 coarse levels, no charging state |

Everything is event-driven; a 30-second poll only catches battery drift on sources without change events.

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
| `--hid-probe` | Dumps the Xbox controller's raw HID reports and GATT services |

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
