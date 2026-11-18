# wslUI

A Windows desktop app (WPF, .NET 11) for managing WSL without the command line.

## Features

| Page | What it covers |
|---|---|
| **Distributions** | Live list with state, version, VHD size, disk used, processes and uptime. Start (kept running), stop, restart, terminal (user or root), Explorer, set default, convert WSL 1 ⇄ 2, set default user, create user, export, clone, move, resize, sparse, unregister. |
| **Install** | Online catalog (plus the legacy catalog), with `--name`, `--location`, `--version`, `--vhd-size`, `--fixed-vhd` and `--web-download`. Can create the default user right after install. Install from a `.wsl` or `.tar` file. |
| **Backup & Import** | `--export` (tar, tar.gz, tar.xz, vhd), `--import` (tar or `--vhd`), `--import-in-place`. |
| **Disks** | Lists physical disks; `--mount` with `--vhd`, `--bare`, `--name`, `--type`, `--options`, `--partition`; `--unmount`; `lsblk` view. |
| **Global config** | Form editor and raw editor for `%UserProfile%\.wslconfig` (`[wsl2]`, `[experimental]`, `[general]`). Keeps comments and unknown keys, and saves a `.bak` copy before writing. |
| **Distro config** | Form editor and raw editor for each distro's `/etc/wsl.conf`. |
| **Console** | Runs commands inside a distro with streaming output and stdin, or passes **raw `wsl.exe` arguments** for anything else. |
| **System** | `--version`, `--status`, `--update` (including `--pre-release`), `--shutdown` (including `--force`), `--set-default-version`, `--install --no-distribution` (`--enable-wsl1`), `--uninstall`, `--debug-shell`, `--system`. |

Every command wslUI runs is shown, with its output, in the **command log** at the bottom of the window. The tray icon offers quick start, stop and terminal actions for each distro.

## Build

```powershell
# Dev build that runs without a UAC prompt
dotnet build src/wslUI/wslUI.csproj -p:NoElevate=true

# Release: one self-contained exe (runs as admin), written to publish\wslUI.exe
dotnet publish src/wslUI/wslUI.csproj -c Release -o publish
```

To regenerate the app icon, run `tools/make-icon.ps1`.

## Notes

- **Start** holds an idle `wsl.exe` session open so the distro stays up until you stop it. Without that session, WSL would shut the distro down after a few seconds.
- **Live stats** run a tiny command in each running distro while the window is visible. That counts as activity, so distros won't idle-stop while you watch them. You can turn this off under System.
- Settings are stored in `%AppData%\wslUI\settings.json`.
