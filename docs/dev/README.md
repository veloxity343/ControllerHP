# Developer docs

[← Back to the main page](../../README.md) · [User wiki](../wiki/README.md)

For working on ControllerHP itself. To contribute, start with [CONTRIBUTING.md](../../CONTRIBUTING.md).

1. **[Architecture](architecture.md):** how the code is laid out, how readings flow from a controller to the tray, and the design decisions behind it.
2. **[Diagnostics](diagnostics.md):** the command-line flags for checking controllers and rendering the UI without hardware.
3. **[Adding a controller](adding-a-controller.md):** how to support a new controller family.
4. **[Help wanted](help-wanted.md):** ideas open to contributors, including more controllers, Linux and macOS, features and tests, with where to start on each.

## Building

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). `global.json` pins the feature band. Open `ControllerHP.sln` in Visual Studio or Rider, or:

```bash
dotnet build ControllerHP.sln
```

The project also builds on Linux (`EnableWindowsTargeting`), which is how CI runs it, but the app itself only runs on Windows.
