<h1 align="center">SUSSYMODMANAGER</h1>

<p align="center">
Mod manager for <strong>Among Us</strong> on Windows, macOS and Linux.
Install mods, manage presets, pick a color theme, launch the game.
</p>

---

## What it does

- **SUS AF PACK** — one click installs Town of Us Mira, Roles Extension, Draft Mode, AUnlocker, Vanilla Enhancements and Better CrewLink in dependency order.
- **Mod store** — GitHub releases and Thunderstore (Cosmella's Outfit Presets, TabsBuilderApi, etc.).
- **Presets** — save, rename, delete and load mod selections without reinstalling.
- **Themes** — built-in color profiles or a custom four-color theme; export/import `.json` if you want to share one.
- **BepInEx** — installs the right build (be.752, same as TOU Mira) and keeps dependencies sorted.
- **Updates** — checks GitHub releases on launch; optional auto-download for your OS/arch.
- **First-run wizard** — game path, theme, optional pack install.
- **Logs** — crashes go to a rotating log in the app data folder.

## Download

Latest builds: **[Releases](https://github.com/sussymodmanager/sussymodmanager/releases/latest)**

| Platform | File |
|----------|------|
| Windows (installer) | `SussyModManager-Setup-x64.exe` |
| Windows (portable) | `SussyModManager-win-x64.zip` |
| macOS | `SussyModManager-osx-arm64.zip` or `osx-x64` |
| Linux | `SussyModManager-linux-x64.zip` + `./run.sh` |

Builds are unsigned, so SmartScreen/Defender may warn the first time. Right-click → Open on macOS if Gatekeeper blocks it. See the release notes if a download gets quarantined.

## Build

Needs .NET 8 SDK.

```bash
dotnet build SussyModManager.sln
dotnet run --project src/SussyModManager/SussyModManager.csproj
```

Self-contained publish:

```bash
dotnet publish src/SussyModManager/SussyModManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Swap `win-x64` for `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64` or `osx-arm64`.

CI builds on push/PR ([`build.yml`](.github/workflows/build.yml)). Tag `v*` to ship ([`sussymodmanager-release.yml`](.github/workflows/sussymodmanager-release.yml)).

## Releasing

Version comes from the git tag — CI passes `-p:Version=${TAG#v}` and `AppInfo.Version` reads it at runtime.

```bash
git tag v1.0.4 && git push origin v1.0.4
```

The mod catalog in `data/` is also fetched live from the repo on launch, so registry/cache/preset fixes can ship without an app release.

## Platform notes

- **Windows** — BepInEx via `winhttp.dll`; game launches through Steam so it stays up if you close the manager.
- **macOS / Linux** — Among Us is a Windows build; launch goes through Steam/Proton. Set Steam launch options to `WINEDLLOVERRIDES="winhttp=n,b" %command%` for BepInEx injection.
- **Steam depot downgrades** (some version-locked mods) are Windows-only.

## Data folder

| OS | Path |
|----|------|
| Windows | `%AppData%\SussyModManager` |
| macOS | `~/Library/Application Support/SussyModManager` |
| Linux | `~/.config/SussyModManager` |

Existing **BeanModManager** installs on Windows are imported on first launch.

## License

Fork of [BeanModManager](https://github.com/rewalo/BeanModManager), GPLv3. See [LICENSE](LICENSE) and [CHANGELOG.md](CHANGELOG.md).
