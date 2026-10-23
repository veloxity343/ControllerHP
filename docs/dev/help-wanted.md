# Help wanted

[← Developer docs](README.md)

Ideas that would make ControllerHP better, which the maintainer can't take on right now, often for lack of the hardware or platform. **These are ideas, not promises:** nothing here is scheduled.

**Before you start,** comment on the matching issue, or [open one](https://github.com/veloxity343/ControllerHP/issues/new/choose) if there isn't one. That way, two people don't build the same thing, and we can agree an approach before you spend time on it.

Sizes are rough: **small** is an evening or two, **medium** a few weekends, **large** a project in its own right.

- [More controllers](#more-controllers)
- [Other operating systems](#other-operating-systems)
- [Features](#features)
- [Quality and reach](#quality-and-reach)

## More controllers

Each needs someone who owns the controller, both to work out its format and to test it. Start with [Adding a controller](adding-a-controller.md) and `--hid-probe`.

| Controller | Size | Notes |
|---|---|---|
| **Nintendo Switch Pro Controller** | Medium | Vendor id `057E`. Battery level and a charging flag are in the standard full input report, but over Bluetooth the controller must first be switched to that report with a subcommand, much like the DualSense. Linux's `hid-nintendo` driver documents the format. |
| **Joy-Con** (left and right) | Medium | Same protocol as the Pro Controller, but each half is its own device: decide whether a pair shows as one controller or two. |
| **Xbox Elite Series 2 and Adaptive Controller** | Small | Probably work already through the Xbox sources, but untested. Confirm, and fix whatever doesn't. The Bluetooth match looks for "Xbox" in the device name. |
| **8BitDo and other third-party pads** | Small to medium | Many pretend to be an Xbox or Switch controller, depending on their mode. Find out which modes already work and what each reports, and add notes to the wiki. |
| **Devices on other makers' USB receivers (dongles)** | Medium per maker | Logitech's receivers (`LogitechSource.cs`) and around twenty other makers (`Battery/Vendor/`) are supported, all but Logitech untested: if you have one, please report how it goes. A new maker is a `VendorProtocol` subclass in its own file, listed in `VendorProtocols.cs`. Other makers' wireless mice, keyboards and headsets don't report battery to Windows, and each has its own protocol over the receiver's HID interface: Razer, SteelSeries, Corsair, HyperX and so on. Each needs someone with the hardware, and a source per maker. Only ever read: never send a guessed command to a device. NuPhy's 2.4 GHz dongle is a known gap: its keyboards' battery isn't exposed over the dongle's raw HID channel in the open-source firmware. |
| **Steam Controller / Steam Deck controls** | Medium | Not standard HID gamepads. Check whether any battery data is reachable without Steam. |

## Other operating systems

ControllerHP is Windows-only today. **This is the biggest opportunity, and also the biggest job.**

The core logic is portable already: the data model (`ControllerBattery`), battery history (`BatteryHistory`) and the stats maths (`BatteryStats`) have no Windows dependencies. So does the logic deciding when to warn, though it's wired to the Windows pop-up today. Everything else is Windows-specific:
- the battery sources use Windows.Gaming.Input, XInput, Windows' Bluetooth device properties and the Win32 HID API;
- the interface is WinForms.

So a port would go:

1. **Split the project** (medium). Move the portable core into a `net8.0` class library: the model, history, stats, the warning logic, and the PlayStation report parsing, which is pure code. Keep the Windows sources and interface in the existing app. This step is useful on its own, because it makes the core testable (see [automated tests](#quality-and-reach)).
2. **Add a platform's battery sources** (medium), behind the existing `IBatterySource` interface.
3. **Add a platform's interface** (large): a tray icon, pop-ups and settings. A cross-platform UI toolkit such as Avalonia could share one interface between platforms. Native toolkits fit each desktop better.

| Platform | Where battery data comes from |
|---|---|
| **Linux** | Kernel drivers already report controller batteries through the `power_supply` class in `/sys/class/power_supply`. That includes `hid-playstation` for DualSense and DualShock 4, `hid-nintendo` for Switch controllers, and `xpadneo` for Xbox controllers over Bluetooth. They're also available through UPower over D-Bus, which reports changes as they happen. Tray icons use the StatusNotifierItem protocol on most desktops. Supporting this well across distributions and desktops (GNOME needs an extension for tray icons) is a real part of the work. |
| **macOS** | Apple's GameController framework gives each connected controller's battery level and charging state (`GCController.battery`, macOS 11 and later). The tray equivalent is a menu bar item. |

If you're interested, open an issue to discuss it first: a port shapes the project's structure, so it's worth agreeing the approach early.

## Features

| Idea | Size | Notes |
|---|---|---|
| **A "charge soon" warning** | Small | A pop-up when the predicted time to the low level drops below a set time (say 30 minutes), as well as at fixed percentages. |
| **Find my controller** | Medium | Make a controller rumble, or flash its light bar, to tell identical ones apart. PlayStation controllers accept this through an output report. Xbox rumble through Windows.Gaming.Input may only work for the app in focus, so test that first. |
| **Turn off PlayStation controllers over Bluetooth** | Medium | Disconnecting a PlayStation controller over Bluetooth turns it off, as DS4Windows does. This uses a long-standing but undocumented Windows call, so it needs careful handling and testing. |
| **Translations** | Medium | All text is in English, written directly in the code. Move it into resource files, then add languages. |
| **Screen reader support** | Medium | The settings window's custom controls draw themselves, so screen readers can't see them. Each needs accessibility information (UI Automation) so tools like Narrator can read and use it. |
| **High contrast mode** | Small to medium | The custom drawing uses fixed colours. Follow Windows' high contrast colours when they're on. |

## Quality and reach

| Idea | Size | Notes |
|---|---|---|
| **Automated tests** | Small to medium | No hardware needed. `BatteryStats` and `BatteryHistory` work on readings, so they can be tested with sample `battery.csv` data: drain rates, crash recovery, trimming, ordering. The PlayStation report parser can be tested with captured reports. A test project would also run in CI. |
| **Package managers** | Small | Publish to winget, Scoop or Chocolatey, so people can install and update with one command. Releases already produce the zips they need. |
| **Code signing** | Small, but costs money | Signing the exe would remove the "Windows protected your PC" warning. It needs a code-signing certificate, which costs money every year, or a free scheme for open-source projects. Suggestions welcome. |
| **Testing on more setups** | Small | Different Windows versions, display scaling, multiple monitors, and taskbars on other edges. Report what works and what doesn't. |

---

[← Adding a controller](adding-a-controller.md) · [Developer docs](README.md)
