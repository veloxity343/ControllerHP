# Changelog

Notable changes to ControllerHP, newest first. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [semantic versioning](https://semver.org/).

<!--
Add each change under Unreleased as it's made. When releasing, rename Unreleased to the version and date,
for example "## [0.4.0] - 2026-11-02", and start a new empty Unreleased section above it. Each release
uses its version's section as its release notes, and the release build stops if a version has none.
Pre-releases (tags like v0.4.0-beta.1) use the Unreleased section.
-->

## [Unreleased]

### Added

- **Pop-ups during fullscreen games:** in exclusive fullscreen, where pop-ups can't appear, they're now held and shown once the game closes, instead of being lost. A new **Hold while a game is fullscreen** setting does the same for borderless games. At most one shows per controller, and a held battery warning is dropped if the controller was charged meanwhile.

### Changed

- The supported controllers tables say which controllers and connections have been **tested** on real hardware, which are **likely** to work, and which are still **untested**.

## [0.3.0] - 2026-10-21

### Added

- **Controller panel:** left-click the tray icon to see every connected controller, with its charge and connection.
- **Settings window:** tray icons, pop-up position, connection pop-ups, battery warnings (each with its own on/off and level) and Start with Windows. Open it from the panel's gear or **Settings…** in the right-click menu.
- **Controllers page:** a chart of each controller's battery history (2 hours to 7 days), with time until low or full, average drain, play time, charge cycles and a battery health estimate. Controllers you no longer use can be removed, and restored any time.
- **Light and dark mode:** the right-click menu, panel and settings window follow Windows, with Windows 11's rounded corners.
- **New app icon,** also shown in the tray when no controllers are connected.
- **Battery warnings** toggle in the right-click menu, to turn both warnings on or off at once.
- A wiki covering getting started, every setting, the Controllers page, supported controllers and troubleshooting.

### Changed

- **Tray icons** now offers 1, 2 or one per controller.
- `history.log` is kept for 30 days and `error.log` to its newest 1 MB, instead of growing forever.
- Reconnecting a controller causes fewer background refreshes.

## [0.2.0] - 2026-10-21

### Added

- Support for **DualSense, DualSense Edge and DualShock 4** controllers over USB and Bluetooth.
- **One tray icon for several controllers,** showing the one with the lowest battery, with the rest listed when you hover. A **Tray icons** option shows more.
- **Pop-up position** and **Connection pop-ups** options in the right-click menu.

### Changed

- Pop-ups are slimmer and appear in the top-right corner by default, clear of Steam's notifications.

### Fixed

- A Bluetooth Xbox controller no longer disappears from the tray when a DualSense disconnects.

### Security

- Added a security policy for reporting vulnerabilities privately.

## [0.1.1] - 2026-10-21

### Added

- An application icon.

## [0.1.0] - 2026-10-21

### Added

- A battery meter in the notification area for Xbox controllers over USB, Bluetooth and the Xbox Wireless Adapter: the percentage, a meter that turns from green to red, and a bolt while charging.
- Pop-ups when a controller connects or disconnects, and when its battery reaches 20% and 10%.
- **Start with Windows**.

[Unreleased]: https://github.com/veloxity343/ControllerHP/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/veloxity343/ControllerHP/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/veloxity343/ControllerHP/compare/v0.1.1...v0.2.0
[0.1.1]: https://github.com/veloxity343/ControllerHP/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/veloxity343/ControllerHP/releases/tag/v0.1.0
