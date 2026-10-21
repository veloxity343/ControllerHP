<p align="center">
  <img src="docs/images/app-icon.png" width="96" alt="">
</p>

<h1 align="center">ControllerHP</h1>

<p align="center">Battery levels for Xbox and PlayStation controllers, in the Windows taskbar.</p>

<p align="center">
  <a href="https://github.com/veloxity343/ControllerHP/releases/latest"><img src="https://img.shields.io/github/v/release/veloxity343/ControllerHP" alt="Latest release"></a>
  <a href="https://github.com/veloxity343/ControllerHP/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/veloxity343/ControllerHP/ci.yml?branch=main" alt="Build status"></a>
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4" alt="Windows 10 and 11">
  <a href="LICENSE"><img src="https://img.shields.io/github/license/veloxity343/ControllerHP" alt="MIT licence"></a>
</p>

<p align="center">
  <a href="#features">Features</a> · <a href="#install">Install</a> · <a href="docs/wiki/README.md">Wiki</a> · <a href="#help">Help</a> · <a href="#contributing">Contributing</a> · <a href="#license">License</a>
</p>

![Tray icons at each battery level, pop-ups, and the controller panel in dark and light mode](docs/images/icon-preview.png)

## Features

- **A battery icon in the taskbar** showing the percentage and a meter that turns from green to red, with a bolt while charging.
- **Pop-ups** when a controller connects or disconnects, and when its battery runs low (20% and 10% by default).
- **One icon for several controllers**, showing whichever is lowest, or one icon each if you prefer.
- **A panel on left-click** listing every controller with its charge and connection.
- **Battery history**: a chart of each controller's charge, with average drain, time left, play time and a battery health estimate.
- **Follows Windows' light or dark mode**, and runs quietly: a check every 30 seconds, nothing sent anywhere.

## Install

1. Download a zip from [Releases](https://github.com/veloxity343/ControllerHP/releases/latest):

   | Download | Size | Needs |
   |---|---|---|
   | `ControllerHP-x.y.z-win-x64.zip` | ~6 MB | [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (Windows offers it if it's missing) |
   | `ControllerHP-x.y.z-win-x64-standalone.zip` | ~72 MB | Nothing |

2. Extract `ControllerHP.exe` somewhere permanent, such as `%LOCALAPPDATA%\Programs\ControllerHP`, and run it.
3. If Windows shows **"Windows protected your PC"**, choose **More info → Run anyway**. ControllerHP isn't code-signed, so Windows warns about it until it's widely downloaded.

Requires Windows 10 version 1809 or later. [Getting started](docs/wiki/getting-started.md) covers keeping the icon visible, starting with Windows, updating and uninstalling.

## Supported controllers

| Controller | USB | Bluetooth | Xbox Wireless Adapter |
|---|---|---|---|
| Xbox One / Series | ✓ | ✓ | ✓ |
| DualSense / DualSense Edge | ✓ | ✓ | — |
| DualShock 4 | ✓ | ✓ | — |

PlayStation controllers report battery in 10% steps. See [Supported controllers](docs/wiki/supported-controllers.md) for what each connection can report and the known limitations.

## Help

- **Something not working?** Start with [Troubleshooting](docs/wiki/troubleshooting.md).
- **Questions** go in [Discussions](https://github.com/veloxity343/ControllerHP/discussions).
- **Bugs and ideas** go in [Issues](https://github.com/veloxity343/ControllerHP/issues/new/choose), where a short form asks for what's needed to help.
- **Security problems:** please report them privately, as described in [SECURITY.md](SECURITY.md).

## Contributing

Bug reports, results from controllers nobody has tested yet, and pull requests are all welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) and the [developer docs](docs/dev/README.md). Everyone taking part is expected to follow the [Code of Conduct](CODE_OF_CONDUCT.md).

Looking for something to work on? [Help wanted](docs/dev/help-wanted.md) lists open ideas: more controllers (Switch Pro, Joy-Con, 8BitDo), Linux and macOS versions, new features and automated tests.

## License

[MIT](LICENSE)
