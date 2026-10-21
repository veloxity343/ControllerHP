# Adding a controller

[← Developer docs](README.md)

Each controller family is an `IBatterySource` in `src/ControllerHP/Battery`. A new family, such as the Nintendo Switch Pro Controller (vendor id `057E`), would usually follow `PlayStationSource`:

1. **Find the device.** Watch for its HID game-controller interface (usage page `0x01`, usage `0x05`) and filter by vendor and product id.
2. **Read the battery.** Open the device briefly, read an input report, and parse the battery from it. Don't hold the device open: tools that want exclusive access would be blocked.
3. **Note your source.** Say in a comment where the report format came from, such as a Linux driver or a reverse-engineering write-up, since nobody can check it without the hardware.
4. **Return readings** with a stable id, the controller's name, a percentage or level, the charge state and the connection.
5. **Register the source** in `Program.cs`. If it sees the same controllers as an existing source, group the two in a `FirstAvailableSource`; otherwise, add it alongside.

`--hid-probe` dumps raw reports, which helps work out where the battery lives. Watch how the bytes change as the controller charges and drains.

Then update the user docs:
- [Supported controllers](../wiki/supported-controllers.md): the table, and any limitations.
- The table in the main [README](../../README.md).
- If the model has a commonly reported runtime, `BatteryStats.Typical`.

---

[← Diagnostics](diagnostics.md) · Next: [Help wanted →](help-wanted.md)
