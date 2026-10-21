# Architecture

[← Developer docs](README.md)

ControllerHP is a single WinForms app with no window of its own: it lives in the notification area. The code is split in two:

- `src/ControllerHP/Battery`: finding controllers and reading their batteries.
- `src/ControllerHP/Tray`: everything you see, plus settings and battery history.

## From controller to tray

```
 Battery sources ──readings──▶ TrayApp ──▶ tray icons (TrayEntry slots)
  (IBatterySource)    events      │      ├─▶ pop-ups (ControllerNotifier → ToastManager)
                                  │      ├─▶ battery.csv (BatteryHistory)
                                  │      └─▶ flyout / settings window, while open
                                  └── Settings (registry), applied on change
```

1. **Sources** implement `IBatterySource`: a name, a `ControllersChanged` event, and `Read()`, which returns one `ControllerBattery` per controller. A reading carries a stable id, a name, a percentage or coarse level, a charge state, hardware ids and the connection.
2. **`TrayApp`** refreshes on three triggers: a 30-second poll, a source's event, and a follow-up shortly after a controller connects. Events arrive in bursts, so they wait for a 250 ms quiet spell (at most a second) and a burst costs one refresh.
3. **A refresh** reads every source and then:
   - updates the tray icon slots;
   - passes the readings to `ControllerNotifier` for pop-ups;
   - records any changes in `battery.csv` and `history.log`.

## Sources

| Source | File | Notes |
|---|---|---|
| Xbox, via Windows.Gaming.Input | `GamingInputSource.cs` | Pairs Bluetooth pads with their Bluetooth LE battery (`BluetoothLeBattery.cs`), since Windows.Gaming.Input's own figure is wrong over Bluetooth. It also falls back to the Bluetooth LE reading when Windows.Gaming.Input's gamepad list goes stale in a long-running process. |
| Xbox, via XInput | `XInputSource.cs` | Fallback with four levels. |
| PlayStation | `PlayStationSource.cs` | Opens each controller briefly every 5 seconds and reads one HID input report (`Hid.cs`). It never holds the device open, so tools that want exclusive access aren't blocked. |

The two Xbox sources see the same controllers, so `FirstAvailableSource` uses whichever finds any first. Sources for *different* families are independent, and their readings are combined.

## Tray and UI

| Piece | File | Notes |
|---|---|---|
| Tray icons | `TrayApp.cs` (`TrayEntry`) | Reused slots, so icons keep their taskbar position. Which controllers get one: lowest battery first. |
| Icon drawing | `BatteryIconRenderer.cs` | Drawn on a 16×16 grid and scaled. The "no controllers" icon is the embedded `app.ico`. |
| Pop-ups | `ControllerNotifier.cs`, `Toast.cs` | The notifier decides when, and the toast window draws and stacks them. |
| Panel | `Flyout.cs` | Opened by left-click. It closes on losing focus, and also on any click outside it via a mouse hook, because rapid tray clicks can leave it without focus. |
| Settings window | `SettingsWindow.cs`, `Controls.cs` | Custom-drawn controls, because WinForms' own don't follow dark mode. |
| Battery history | `BatteryHistory.cs`, `BatteryStats.cs`, `HistoryChart.cs`, `StatsCard.cs` | Recording and loading, figures, chart, and stat cards. |
| Theming | `Theme.cs`, `MenuRenderer.cs` | Palettes and Windows 11 styling (rounded corners, dark title bar). The tray menu and panel follow the taskbar's mode; the settings window follows the app mode. |
| Settings | `Settings.cs` | `HKCU\Software\ControllerHP`. Raises `Changed` on every write, so the menu and the window stay in step. |

## Design decisions worth knowing

- **Readings are only written when they change.** That keeps `battery.csv` small, and is why the chart holds a level flat until the next row. A heartbeat file (`battery.alive`) lets sessions left open by a crash close at the time the app stopped.
- **Rates are measured between level changes,** not from connection time. A PlayStation controller can be anywhere inside a 10% step when it connects.
- **Health excludes charging time and charge cycles.** It compares runtime per charge with the controller's own first week, and needs three weeks of history.
- **Parsed history is cached,** and later loads read only the rows appended since. The file only grows between startups.
- **Logs are capped at startup:** `battery.csv` at 90 days, `history.log` at 30 days, `error.log` at 1 MB.
- **No network access**, and nothing runs that isn't needed: idle cost is one ~15 ms check every 30 seconds.

---

[← Developer docs](README.md) · Next: [Diagnostics →](diagnostics.md)
