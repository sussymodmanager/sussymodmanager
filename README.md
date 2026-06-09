<div align="center">

# SUSSYMODMANAGER

[Install](#download) • [Documentation (WIP)] • [FAQ (WIP)] • [Screenshots (WIP)] • [Contribute (WIP)](#contribute)

[![Among Us](https://badgen.net/static/AmongUs/2026.3.31/yellow)](https://store.steampowered.com/app/945360/Among_Us)
[![Release](https://img.shields.io/github/v/release/sussymodmanager/sussymodmanager)](https://github.com/sussymodmanager/sussymodmanager/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/sussymodmanager/sussymodmanager/total?label=downloads)](https://github.com/sussymodmanager/sussymodmanager/releases)

![SCREENSHOT](https://github.com/sussymodmanager/sussymodmanager/blob/main/assets/modstore.png)

</div>

---

### Table of Contents (WIP)
- [Introduction](#introduction)
- [Download](#download)

# Introduction

<a href="https://github.com/sussymodmanager/sussymodmanager/blob/main/assets/sus-logo-square.jpg">
  <img
    src="https://raw.githubusercontent.com/sussymodmanager/sussymodmanager/blob/main/assets/sus-logo-square.jpg"
    align="right"
    width="128"
  />
</a>

### Feature rich desktop mod manager for Among Us on Windows, macOS, and Linux.

Select the mods you want to use, or pick a preset, and hit play. It's just that simple.
BepInEx, Dependencies (Reactor, MiraAPI, etc) are handled for you. Steam, Epic, Xbox/MS Store install paths are automatically detected.
Designed to make Among Us Modding as accessible as possible.



## Download

Grab the latest build from [releases](https://github.com/sussymodmanager/sussymodmanager/releases/latest).

| Platform | What to grab |
|----------|----------------|
| Windows x64 | `SussyModManager-Setup-x64.exe` (installer) or `SussyModManager-win-x64.zip` (portable folder) |
| Windows ARM| `SussyModManager-win-arm64.zip` (portable folder) |
| macOS | `SussyModManager-osx-arm64.zip` (Apple Silicon) or `osx-x64` (Intel) |
| Linux | `SussyModManager-linux-x64.zip` unzip, then `./run.sh` |

## Quick start

1. Run the app, the Setup Wizard guides you through getting the app running.
2. Go to **Presets** and install **SUS AF**, **TOHE**, or **Vanilla+**, or browse **Mod Store** and install what you want.
3. On **Installed**, check **Launch** next to the mods you want active, then hit **PLAY**.

## Mod store

![MODSTORE](https://github.com/sussymodmanager/sussymodmanager/blob/main/assets/modstore.png)

**Fast Installs:** Mods come from directly from the source and are ready to update whenever new versions are released.

**Store filters:** narrow the grid to mods that are not installed, already installed, or have an update available.

**Just Works:** Dependencies install in the right order every time so you never need to worry about any issues. 

You can also drop in your own plugin DLLs from **Installed → Add custom .dll**.

## Presets

![PRESETS](https://github.com/sussymodmanager/sussymodmanager/blob/main/assets/presets.png)

Three built-in packs ship with the app:

**SUS AF:** Town of Us Mira, Roles Extension, Draft Mode, AleLuduMod, ChaosTokens, Divani Mods, Game Tweaks, AUnlocker, Vanilla Enhancements, Better CrewLink.

**TOHE:** Town of Host Enhanced, AUnlocker, Vanilla Enhancements, Better CrewLink.

**Vanilla+:** AUnlocker and Vanilla Enhancements only.

For your own setups: save the current selection as a preset, rename or delete it later, **Load selection** without reinstalling. Custom presets can be exported to `.json` and shared; import someone else's file from the Presets page. 

**Install Pack** refreshes the live preset from GitHub, then downloads any missing mods (no forced updates on already-installed ones). Does not select the pack.

**Select Pack** refreshes from GitHub, installs missing mods, and turns on **pack mode**: every Play button becomes **Play {pack name}**, launch selection is locked to the pack mods only, and each play refreshes again, installs newly listed mods, updates pack mods, then launches.

**Deselect Pack** (on Presets or Installed) returns to **custom mode**: Play is plain **PLAY** again and only your Launch checkboxes matter — install extra mods, mix loadouts, no auto pack sync.

When Better CrewLink is in the active selection, it starts automatically alongside the game.

## Installed mods

![INSTALLED](https://github.com/sussymodmanager/sussymodmanager/blob/main/assets/installed.png)

This is where you control what actually loads into the game.

- **Launch** checkbox per mod — only checked mods (plus their dependencies) get copied into `BepInEx/plugins` when you play.
- **PLAY** / **Play {pack}** — in custom mode, launches your checked mods. In pack mode, syncs the selected pack first, then launches. On Windows it goes through Steam so Among Us stays running if you close the manager.
- **Launch Vanilla** — same game folder, no mods injected. Useful for public lobbies or when Reactor is being annoying.
- **Check for updates / Update all** — refresh installed mods from the store. The Installed list updates in place after installs, updates, or pack play — you do not need to switch tabs.

BepInEx is **be.752**, the same build bundled with Town of Us Mira — not a random bleeding-edge beta. Install or repair it from **Settings** if something's off.

## Themes

![THEMES](https://github.com/sussymodmanager/sussymodmanager/blob/main/assets/themes.png)

Eight built-in profiles (Sus Default, Impostor Red, Crew Blue, Skeld Teal, Bubblegum Pink, Toxic Green, AMOLED Black, Light Mint) plus custom themes.

Custom themes start from four colors — the rest is filled in automatically. Open the color picker if you want to tweak further, or tick **Edit all colors** for full control. Export/import `.json` to share a theme.

## Settings worth knowing

- **Game channel** — Steam vs Epic/MS Store picks the right BepInEx build for your install.
- **Troubleshooting** — read-only Reactor/interop status, **Repair interop**, optional **interop reference** Among Us folder (backup install with working interop), open app data / BepInEx log, refresh store from GitHub.
- **Danger zone** — convert back to vanilla (keeps downloaded mods) or nuke everything including BepInEx. Both need the safety checkbox first.
- **Updates** — optional auto-download of new **app** releases. **Check for mod updates on startup** only fetches version info and shows update badges; it does not auto-install mods (pack **Play** still syncs/updates pack mods).

Failed mod installs show the mod name and a link to its GitHub or Thunderstore page.

Crash logs land in the app data folder (paths below).

## Linux and macOS

Among Us is a Windows binary everywhere. The manager installs the matching BepInEx and launches through Steam/Proton. On first launch you get a Proton launch-options reminder; **Remind me later** snoozes it for a week, **Got it** hides it permanently (also dismissible from Settings).

Add this to Steam launch options so BepInEx injects under Proton:

```
WINEDLLOVERRIDES="winhttp=n,b" %command%
```

Mods that need a specific Steam game version (depot downgrades) only auto-install on Windows.

## App data

| OS | Location |
|----|----------|
| Windows | `%AppData%\SussyModManager` |
| macOS | `~/Library/Application Support/SussyModManager` |
| Linux | `~/.config/SussyModManager` |

Downloaded mods, config, cached store data, and logs all live here.

## Building

.NET 8 SDK required.

```bash
dotnet build SussyModManager.sln
dotnet run --project src/SussyModManager/SussyModManager.csproj
```

Release builds are cut by CI when you push a version tag. The tag becomes the app version — no manual version bump in source.

```bash
git tag v1.0.4 && git push origin v1.0.4
```

That runs [`.github/workflows/sussymodmanager-release.yml`](.github/workflows/sussymodmanager-release.yml) and publishes zips + the Windows installer for all six targets. Tests run on every push via [`build.yml`](.github/workflows/build.yml).

Local portable publish (Windows):

```bash
dotnet publish src/SussyModManager/SussyModManager.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true
```

Official release zips are plain self-contained folders (not single-file) to avoid Defender false positives.

## License

Fork of [BeanModManager](https://github.com/rewalo/BeanModManager) — GPLv3, see [LICENSE](LICENSE).
