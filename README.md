# SUSSYMODMANAGER

Desktop mod manager for Among Us. Windows, macOS, and Linux.

You point it at your game folder, install mods from the store, tick what you want for launch, and hit play. BepInEx and dependencies (Reactor, MiraAPI, etc.) are handled for you. Fork of [BeanModManager](https://github.com/rewalo/BeanModManager) — GPLv3, see [LICENSE](LICENSE).

## Download

Grab the latest build from [releases](https://github.com/sussymodmanager/sussymodmanager/releases/latest).

| Platform | What to grab |
|----------|----------------|
| Windows | `SussyModManager-Setup-x64.exe` (installer) or `SussyModManager-win-x64.zip` (portable folder) |
| macOS | `SussyModManager-osx-arm64.zip` (Apple Silicon) or `osx-x64` (Intel) |
| Linux | `SussyModManager-linux-x64.zip` — unzip, then `./run.sh` |

Nothing is code-signed yet. Windows SmartScreen / Defender and macOS Gatekeeper may block the first run. If that happens: unblock the zip in Properties, or right-click the app → Open. Defender sometimes quarantines new exes — check Protection history and allow it if you're sure you downloaded from here.

## Quick start

1. Run the app. The setup wizard asks for your Among Us folder (auto-detect works for Steam, Epic, and Xbox/MS Store on Windows).
2. Go to **Presets** and install **SUS AF PACK** or **Vanilla+**, or browse **Mod Store** and install what you want.
3. On **Installed**, check **Launch** next to the mods you want active, then hit **PLAY**.

If you already used BeanModManager on Windows, your game path and downloaded mods import on first launch.

## Mod store

Mods come from GitHub releases and Thunderstore. The catalog lives in [`data/`](data/) in this repo and is also pulled live on startup, so registry fixes can ship without waiting for a new app build.

Dependencies install in the right order. If GitHub rate-limits the API, the manager falls back to direct release URLs and bundled cache data.

You can also drop in your own plugin DLLs from **Installed → Add custom .dll**.

## Presets

Two built-in packs ship with the app:

**SUS AF PACK** — Town of Us Mira, Roles Extension, Draft Mode, AUnlocker, Vanilla Enhancements, Better CrewLink. Installs in dependency order; one button.

**Vanilla+** — AUnlocker and Vanilla Enhancements only. Light tweaks, no role mods.

For your own setups: save the current selection as a preset, rename or delete it later, **Load selection** without reinstalling. Custom presets can be exported to `.json` and shared; import someone else's file from the Presets page. Built-in packs don't have an export button — they're already in the repo.

## Installed mods

This is where you control what actually loads into the game.

- **Launch** checkbox per mod — only checked mods (plus their dependencies) get copied into `BepInEx/plugins` when you play.
- **PLAY** — syncs plugins and launches the game. On Windows it goes through Steam so Among Us stays running if you close the manager.
- **Launch Vanilla** — same game folder, no mods injected. Useful for public lobbies or when Reactor is being annoying.
- **Check for updates / Update all** — refresh installed mods from the store.

BepInEx is **be.752**, the same build bundled with Town of Us Mira — not a random bleeding-edge beta. Install or repair it from **Settings** if something's off.

## Themes

Eight built-in profiles (Sus Default, Impostor Red, Crew Blue, Skeld Teal, Bubblegum Pink, Toxic Green, AMOLED Black, Light Mint) plus custom themes.

Custom themes start from four colors — the rest is filled in automatically. Open the color picker if you want to tweak further, or tick **Edit all colors** for full control. Export/import `.json` to share a theme.

## Settings worth knowing

- **Game channel** — Steam vs Epic/MS Store picks the right BepInEx build for your install.
- **Danger zone** — convert back to vanilla (keeps downloaded mods) or nuke everything including BepInEx. Both need the safety checkbox first.
- **Updates** — optional auto-download of new app releases for your OS/arch.

Crash logs land in the app data folder (paths below).

## Linux and macOS

Among Us is a Windows binary everywhere. The manager installs the matching BepInEx and launches through Steam/Proton.

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

## Changelog

See [CHANGELOG.md](CHANGELOG.md). Current release: **1.0.4**.
