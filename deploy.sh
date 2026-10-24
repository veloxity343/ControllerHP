#!/usr/bin/env bash
# Build the Avalonia tray app in WSL and install to %LOCALAPPDATA%\Programs\WattsLeft, then (re)start it.
# Running straight from \\wsl.localhost is unreliable: Windows caches stale DLLs, and the
# "Start with Windows" entry would point at a path that doesn't exist until WSL starts.
set -euo pipefail
cd "$(dirname "$0")"

WIN=/mnt/c/Windows/System32
DOTNET="${DOTNET:-$HOME/.dotnet/dotnet}"
LOCALAPPDATA_WIN=$("$WIN/cmd.exe" /c "echo %LOCALAPPDATA%" 2>/dev/null | tr -d '\r')
INSTALL_DIR="$(wslpath -u "$LOCALAPPDATA_WIN")/Programs/WattsLeft"

# A fresh publish folder, so files the app no longer has aren't copied again.
rm -rf publish
DOTNET_CLI_TELEMETRY_OPTOUT=1 "$DOTNET" publish src/WattsLeft -c Release -o publish --nologo

# ControllerHP is WattsLeft's old name: stop it too, so the first start can move its settings and history.
for exe in WattsLeft.exe ControllerHP.exe; do
  "$WIN/taskkill.exe" /IM "$exe" /F >/dev/null 2>&1 && sleep 1 || true
done
# The install folder holds nothing but the app (settings live in the registry, logs and history in
# %LOCALAPPDATA%\WattsLeft), so it's replaced whole, leaving no files from an older build behind.
mkdir -p "$INSTALL_DIR"
find "${INSTALL_DIR:?}" -mindepth 1 -delete
cp -rf publish/. "$INSTALL_DIR/"

if [[ "${1:-}" != "--no-start" ]]; then
  # Launch from a Windows working directory so the process isn't tied to this shell.
  (cd "$INSTALL_DIR" && "$WIN/cmd.exe" /c start "" WattsLeft.exe "${@:2}" </dev/null >/dev/null 2>&1)
fi
echo "Installed to $(wslpath -w "$INSTALL_DIR")"
