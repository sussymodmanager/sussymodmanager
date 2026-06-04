<h1 align="center">SUSSYMODMANAGER</h1>

<p align="center">
A modern, cross-platform mod manager for <strong>Among Us</strong> — install, manage and launch
mods on Windows, macOS and Linux, with one-click curated packs and fully customizable color
profiles. The sussiest mod manager alive.
</p>

---

## Highlights

- **Cross-platform** — native desktop app on Windows, macOS and Linux (built on .NET 8 + Avalonia).
- **One-click SUS AF PACK** — installs Town of Us Mira + its Roles Extension, Draft Mode, AUnlocker,
  Outfit Presets, AleLuduMod (15+ players with fixed Transporter/meeting menus), Vanilla
  Enhancements and Better CrewLink in the correct dependency order.
- **GitHub + Thunderstore** — installs mods from GitHub releases and the Thunderstore Among Us
  community (e.g. Cosmella's Outfit Presets and TabsBuilderApi).
- **Color profiles** — pick from built-in themes (Sus Default, Impostor Red, Crew Blue, Skeld
  Teal, AMOLED Black, Light Mint, **Bubblegum Pink**, **Toxic Green**) or set a custom accent.
  The whole UI recolors instantly.
- **Automatic dependency + BepInEx handling** — Reactor, MiraAPI, etc. are resolved for you, and
  the correct BepInEx build is installed (and auto-updated if an outdated one is found).
- **Built-in updater** — checks the GitHub releases on launch and automatically downloads + applies
  the right build for the user's OS/arch.
- **First-launch wizard** — guides new users through setting the game path, picking a theme and
  optionally installing the SUS AF PACK.
- **Crash logging** — unhandled errors are written to a rotating log in the app data folder.

## Download

Grab the latest build for your OS from the
**[Releases page](https://github.com/sussymodmanager/sussymodmanager/releases/latest)**:

- **Windows** — `SussyModManager-win-x64.zip` (or `win-arm64`). Unzip the folder and run
  `SussyModManager.exe` inside it.
- **macOS** — `SussyModManager-osx-arm64.zip` (Apple Silicon) or `osx-x64` (Intel). Unzip and move
  `SussyModManager.app` to Applications. Gatekeeper may block it (unsigned) — **right-click → Open**.
- **Linux** — `SussyModManager-linux-x64.zip` (or `linux-arm64`). Unzip and run `./run.sh` (or the
  `SussyModManager` binary). A `.desktop` file + icon are included.

### "Windows protected your PC" / Defender warning

The builds are **not code-signed**, so Windows SmartScreen and Microsoft Defender may warn the first
time you download or run the app. It is safe — the full source is in this repo. To get past it:

1. **If the browser blocks the download:** click the **···** next to the download → **Keep** →
   **Keep anyway**.
2. **Unblock the zip:** right-click the downloaded `.zip` → **Properties** → tick **Unblock** →
   **OK**, then extract.
3. **If SmartScreen appears on launch:** click **More info → Run anyway**.
4. **If Defender quarantines the exe (false positive):** open **Windows Security → Virus & threat
   protection → Protection history**, find the item, and choose **Allow**.

These warnings fade as the app builds download reputation, and disappear entirely once releases are
code-signed (planned).

## Architecture

```
SussyModManager.sln
├── src/SussyModManager.Core   # platform-agnostic mod engine (no UI)
│   ├── Models                 # Config, Mod, registry, presets, color profiles
│   ├── Services               # ModStore, GitHub/Thunderstore providers, downloader,
│   │                          #   installer, BepInEx, launch, presets, migration
│   └── Platform               # OS detection + per-OS data paths
├── src/SussyModManager        # Avalonia UI (MVVM)
│   ├── ViewModels / Views     # Store, Installed, Presets, Settings
│   ├── Themes                 # control styles
│   └── Services/ThemeService  # applies color profiles to live resources
└── data                       # mod-registry.json, mod-cache.json,
                               #   builtin-presets.json, color-profiles.json
```

## Build & run

Requires the **.NET 8 SDK**.

```bash
dotnet build SussyModManager.sln
dotnet run --project src/SussyModManager/SussyModManager.csproj
```

### Publish a self-contained build

```bash
dotnet publish src/SussyModManager/SussyModManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
# swap win-x64 for win-arm64, linux-x64, linux-arm64, osx-x64 or osx-arm64
```

Two GitHub Actions workflows:

- [`build.yml`](.github/workflows/build.yml) — builds + runs the unit tests on every push/PR to `main`.
- [`sussymodmanager-release.yml`](.github/workflows/sussymodmanager-release.yml) — on every `v*` tag,
  builds all six targets (a proper macOS `.app` bundle, a Linux folder with `.desktop` + icon +
  `run.sh`, and Windows zips) and attaches them to a GitHub Release.

## Releasing & in-app updates

The version is driven entirely by the git tag — there are **no version constants to hand-edit**.
`AppInfo.Version` reads the assembly version at runtime, CI injects it from the tag
(`-p:Version=${TAG#v}`), and `Directory.Build.props` holds the local default.

To ship an update, just push a tag:

```bash
git tag v1.0.1 && git push origin v1.0.1
```

The release workflow builds the Windows/macOS/Linux zips and publishes a GitHub Release.

Every installed copy checks `releases/latest` on launch. When the tag is newer it **downloads the
zip for the user's OS automatically**, then either applies it on the next launch or via the
**Restart & update** button in the banner — no manual download/extract. Users can toggle this off in
**Settings → Updates** (it falls back to a Download button that opens the release page). That's the
whole loop — bump the version, push a tag, done.

### Updating the mod store without an app release

The mod catalog lives in `data/` and is also fetched live from this repo. On launch the app pulls
the latest `mod-registry.json`, `mod-cache.json`, `builtin-presets.json` and `color-profiles.json`
from `https://raw.githubusercontent.com/<owner>/<repo>/<branch>/data/` (branch set by
`AppInfo.GitHubBranch`), caches them locally, and reloads the store live if anything changed. So to
add or fix a mod, edit the JSON in `data/`, commit, and push — every client picks it up on next
launch. No app update required. If the user is offline, the bundled copies are used.

## Platform notes

- **Windows** — BepInEx injects automatically via `winhttp.dll`; the game launches directly.
- **macOS / Linux** — Among Us ships Windows binaries, so it runs through Steam (Proton/Wine/
  CrossOver). SUSSYMODMANAGER installs the BepInEx build that matches the game's binaries and hands
  launching off to Steam. For BepInEx to inject under Proton, set the Steam launch options to
  `WINEDLLOVERRIDES="winhttp=n,b" %command%`.
- **Steam depot downgrades** (needed by a few version-locked mods like The Other Roles) are
  Windows-only; on other platforms the mod still installs and you are warned to supply the matching
  game version manually.

## Data locations

| OS | Path |
|----|------|
| Windows | `%AppData%\SussyModManager` |
| macOS | `~/Library/Application Support/SussyModManager` |
| Linux | `$XDG_CONFIG_HOME/SussyModManager` or `~/.config/SussyModManager` |

On first launch on Windows, an existing **BeanModManager** install is imported automatically
(game path, downloaded mods and selection).

## Credits & license

SUSSYMODMANAGER is a fork of [BeanModManager](https://github.com/rewalo/BeanModManager) and remains
licensed under the **GNU GPLv3** (see [LICENSE](LICENSE)). The complete corresponding source for
every release is this repository at the matching `v*` tag. Huge thanks to the original authors and
to every Among Us mod developer whose work is listed in the registry.

See [CHANGELOG.md](CHANGELOG.md) for release history.
