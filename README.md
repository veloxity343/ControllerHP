<p align="center">
  <img src="docs/images/app-icon.png" width="96" alt="">
</p>

<h1 align="center">WattsLeft</h1>

<p align="center">Battery levels for game controllers, mice, keyboards and headsets, in the Windows taskbar.</p>

<p align="center">
  <a href="https://github.com/veloxity343/WattsLeft/releases/latest"><img src="https://img.shields.io/github/v/release/veloxity343/WattsLeft" alt="Latest release"></a>
  <a href="https://github.com/veloxity343/WattsLeft/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/veloxity343/WattsLeft/ci.yml?branch=main" alt="Build status"></a>
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4" alt="Windows 10 and 11">
  <a href="LICENSE"><img src="https://img.shields.io/github/license/veloxity343/WattsLeft" alt="MIT licence"></a>
</p>

<p align="center">
  <a href="#features">Features</a> · <a href="#install">Install</a> · <a href="docs/wiki/README.md">Wiki</a> · <a href="#help">Help</a> · <a href="#contributing">Contributing</a> · <a href="#license">License</a>
</p>

<p align="center">
  <img src="docs/images/device-icons.png" width="620" alt="Every kind of device, a controller, mouse, keyboard, headset and other, at 100% down to 15%, charging, wired and unknown, in dark and light mode">
</p>

## Features

- **An icon in the taskbar for each kind of device**, a controller, mouse, keyboard or headset, with a battery bar that turns from green to red, and a bolt while charging. Hover for the exact percentage.
- **Pop-ups** when a device connects or disconnects, and when its battery runs low (20% and 10% by default, or levels of its own), with a sound if you like.
- **One icon for several devices**, showing whichever is lowest, or one icon each if you prefer.
- **A panel on left-click** listing every device with its charge and connection.
- **A Devices page** with each device's battery history, average drain, time left, play time and a battery health estimate, its own warning levels, and a way to rename devices or remove ones you no longer use.
- **Choose what to watch:** turn off whole kinds of device, such as keyboards, in Settings.
- **Shares battery levels with other apps** if you turn it on, such as Rainmeter skins or scripts, through a file on your PC.
- **Follows Windows' light or dark mode**, and runs quietly: a check every 30 seconds, nothing sent anywhere.
- **Tells you when there's a new version,** if you let it (it asks once, on its first start), and updates itself when you say so.

## Documentation

This page covers the basics. The **[wiki](docs/wiki/README.md)** has the rest:
- [setting up](docs/wiki/getting-started.md) and what [each setting](docs/wiki/using-wattsleft.md) does;
- how the [battery stats and health](docs/wiki/devices.md) are worked out, and what's stored on your PC;
- what each [device and connection](docs/wiki/supported-devices.md) can report;
- [fixes for common problems](docs/wiki/troubleshooting.md).

If something isn't working, Troubleshooting is the place to start.

## Install

1. Download `WattsLeft-x.y.z-win-x64.zip` from [Releases](https://github.com/veloxity343/WattsLeft/releases/latest), about 16 MB. It needs nothing else installed: .NET is built in.
2. Extract the whole zip somewhere permanent, such as `%LOCALAPPDATA%\Programs\WattsLeft`, and run `WattsLeft.exe`. Keep the files together: the exe needs the DLLs beside it.
3. If Windows shows **"Windows protected your PC"**, choose **More info → Run anyway**. WattsLeft isn't code-signed, so Windows warns about it until it's widely downloaded.

Requires Windows 10 version 1809 or later. [Getting started](docs/wiki/getting-started.md) covers keeping the icon visible, starting with Windows, updating and uninstalling. See [CHANGELOG.md](CHANGELOG.md) for what's new in each version.

## Supported devices

| Controller | Bluetooth | USB | Xbox Wireless Adapter |
|---|---|---|---|
| Xbox One / Series | **Tested** | Untested | Untested |
| DualSense | **Tested** | Untested | — |
| DualSense Edge | Likely | Untested | — |
| DualShock 4 | Untested | Untested | — |
| Switch Pro Controller, Joy-Con | Untested | — | — |
| 8BitDo (in its own mode) | Untested | Untested | — |

**Tested** means seen working on real hardware. **Likely** means not tried yet, but it uses the same code as a tested one. **Untested** means written from the device's documented format but not yet tried. Have one that's Likely or Untested? [Tell us how it goes](https://github.com/veloxity343/WattsLeft/discussions).

PlayStation controllers report battery in 10% steps.

**Bluetooth mice, keyboards and headsets** work when Windows itself shows their battery, under Settings → Bluetooth & devices. Tested: a Bluetooth keyboard.

**Devices on their makers' USB receivers** (dongles) are read through each maker's own protocol, read-only:
- **Logitech** mice, keyboards and headsets on Unifying, Bolt and Lightspeed receivers. Tested: a G Pro Wireless.
- **Razer, SteelSeries, Corsair, HyperX, Sony INZONE, Lenovo, ASUS, Keychron, Pulsar and its sister brands, WLmouse, LAMZU, G-Wolves, MCHOSE, Lofree, Astro, Audeze, JBL, NZXT and Angry Miao** devices, and the Steam Controller's puck. All untested so far.

See [Supported devices](docs/wiki/supported-devices.md) for what each device and connection can report, and the known limitations.

## Help

- **Something not working?** Start with [Troubleshooting](docs/wiki/troubleshooting.md).
- **Questions** go in [Discussions](https://github.com/veloxity343/WattsLeft/discussions).
- **Bugs and ideas** go in [Issues](https://github.com/veloxity343/WattsLeft/issues/new/choose), where a short form asks for what's needed to help.
- **Security problems:** please report them privately, as described in [SECURITY.md](SECURITY.md).

## Contributing

Bug reports, results from devices nobody has tested yet, and pull requests are all welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) and the [developer docs](docs/dev/README.md). Everyone taking part is expected to follow the [Code of Conduct](CODE_OF_CONDUCT.md).

Looking for something to work on? [Help wanted](docs/dev/help-wanted.md) lists open ideas: more controllers (Switch Pro, Joy-Con, 8BitDo), devices on USB receivers, Linux and macOS versions, new features and automated tests.

## License

[MIT](LICENSE)
