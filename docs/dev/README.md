# Developer docs

[← Back to the main page](../../README.md) · [User wiki](../wiki/README.md)

For working on WattsLeft itself. To contribute, start with [CONTRIBUTING.md](../../CONTRIBUTING.md).

1. **[Architecture](architecture.md):** how the code is laid out, how readings flow from a device to the tray, and the design decisions behind it.
2. **[Diagnostics](diagnostics.md):** the command-line flags for checking devices and rendering the UI without hardware.
3. **[Adding a controller](adding-a-controller.md):** how to support a new controller family.
4. **[Windows notes](windows-notes.md):** the undocumented Windows details WattsLeft relies on, the quirks it works around, and the lessons from building and releasing it. Read it before changing a battery source, the tray or the release workflow.
5. **[Help wanted](help-wanted.md):** ideas open to contributors, including more controllers, devices on USB receivers, Linux and macOS, features and tests, with where to start on each.

## Building

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). `global.json` pins the feature band. Open `WattsLeft.sln` in Visual Studio or Rider, or:

```bash
dotnet build WattsLeft.sln
```

The projects also build on Linux (`EnableWindowsTargeting`), but the app itself only runs on Windows. A normal build runs on the .NET 10 runtime; `deploy.sh` builds that from WSL and installs it.

Releases are compiled to native code with Native AOT (`-p:PublishAot=true`), so they need no .NET installed. That compile only works on Windows, with Visual Studio's C++ build tools, so CI does it on a Windows runner. The trimming and AOT analysers are on, so anything that wouldn't survive it shows as a build warning.
