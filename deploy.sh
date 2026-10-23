#!/usr/bin/env bash
# Build in WSL and install to %LOCALAPPDATA%\Programs\ControllerHP, then (re)start the tray app.
# Running straight from \\wsl.localhost is unreliable: Windows caches stale DLLs, and the
# "Start with Windows" entry would point at a path that doesn't exist until WSL starts.
set -euo pipefail
cd "$(dirname "$0")"

WIN=/mnt/c/Windows/System32
DOTNET="${DOTNET:-$HOME/.dotnet/dotnet}"
LOCALAPPDATA_WIN=$("$WIN/cmd.exe" /c "echo %LOCALAPPDATA%" 2>/dev/null | tr -d '\r')
INSTALL_DIR="$(wslpath -u "$LOCALAPPDATA_WIN")/Programs/ControllerHP"

# A fresh publish folder, so files the app no longer has aren't copied again.
rm -rf publish
DOTNET_CLI_TELEMETRY_OPTOUT=1 "$DOTNET" publish src/ControllerHP -c Release -o publish --nologo

"$WIN/taskkill.exe" /IM ControllerHP.exe /F >/dev/null 2>&1 && sleep 1 || true
mkdir -p "$INSTALL_DIR"
cp -f publish/* "$INSTALL_DIR/"

if [[ "${1:-}" != "--no-start" ]]; then
  # Launch from a Windows working directory so the process isn't tied to this shell.
  (cd "$INSTALL_DIR" && "$WIN/cmd.exe" /c start "" ControllerHP.exe "${@:2}" </dev/null >/dev/null 2>&1)
fi
echo "Installed to $(wslpath -w "$INSTALL_DIR")"
