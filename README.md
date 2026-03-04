# BossKey

English | [简体中文](README.zh-CN.md) | [日本語](README.ja-JP.md)

<sub>A more considered Boss Key for Windows, with a cleaner and more polished interface than this category usually gets.</sub>

BossKey is a Windows desktop utility for quickly hiding, restoring, and organizing selected application windows. It is built for fast "boss key" workflows, but it is also useful as a lightweight window control tool for daily work setups.

![BossKey main window overview](docs/images/main-window-overview.png)

## Core Workflow

- Hide or restore all selected targets instantly.
- Add targets from the running window list or by using the window picker.
- Organize targets into groups, rename groups, collapse groups, and drag targets between groups.
- Configure global hotkeys and per-group hotkeys.
- Use keyboard-only hotkeys or keyboard + mouse button combinations such as `Ctrl + Alt + Mouse Middle`.
- Allow the same hide/show hotkey to act as a toggle.

## Per-Target Controls

Each target can be configured from its context menu. This lets you tune behavior per app instead of applying the same rule to every window.

- enable or disable a target
- mute on hide
- freeze on hide
- topmost on show
- center on cursor on show

![BossKey target context menu](docs/images/target-context-menu.png)

## Settings

The settings window brings hotkeys, group behavior, language, update checks, and theme access into one place.

- global hotkeys
- per-group hotkeys
- startup and tray behavior
- update checks
- language selection
- theme entry point

![BossKey settings window](docs/images/settings-window.png)

## Themes

BossKey includes a built-in theme system instead of relying on system defaults.

- default light and dark themes
- live preview while editing
- customizable theme colors
- themed message boxes and dialogs

![BossKey theme settings window](docs/images/theme-settings-window.png)

![BossKey color picker](docs/images/color-picker-window.png)

## Language Packs

- Built-in language: English
- Remote language packs currently included in this repository: Simplified Chinese and Japanese
- Installed language packs are stored in:
  - `%APPDATA%\BossKey\Languages`
- Main settings are stored in:
  - `%APPDATA%\BossKey\settings.json`
- When the app checks for updates, it can also refresh installed language packs if newer versions are available.

## Download

Get the latest release from GitHub Releases:

- Installer build: recommended for normal use
- Single-file build: recommended for portable use

Release page:

- <https://github.com/MayFlyOvO/BossKey/releases>

## Requirements

- Windows 10 / 11
- x64
- .NET 8 SDK to build from source

## Build From Source

```powershell
dotnet build BossKey.sln
dotnet run --project .\BossKey.App\BossKey.App.csproj
```

## Release Build

```bat
Build-Release.bat
```

The GitHub Actions workflow builds:

- a self-contained installer package
- a self-contained single-file package

## Notes

- Some elevated or protected processes may require BossKey itself to run as administrator for freeze behavior to work reliably.
- Behavior for multi-process applications depends on which process owns the selected window.
- Some system windows, UWP container windows, or special-rendered windows may not restore exactly like standard desktop apps.

## License

BossKey is licensed under the [MIT License](LICENSE).

This repository also bundles Google Material icon font assets that are distributed under Apache 2.0.
