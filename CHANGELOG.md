# Changelog

## 1.0.8 — 2026-06-08

### Mod versions & store
- **Full mod audit:** all GitHub registry mods checked against live `/releases/latest`; bundled `mod-cache.json` rebuilt; redirect runs before stale cache; generalized `GitHubReleaseAssetGuesser` + atom fallback for prerelease-only repos (e.g. LevelImposter).
- **Registry fixes:** asset filters corrected for StellarRolesAU, EndlessHostRoles, LotusContinued, TOHO, BetterCrewLink, Impostor, SyzyfowyTownOfUs, PokeLobby, EmojisInTheChat, and others.
- **Town of Us Mira v1.4.0 pin fixed:** bundled cache shipped an old TOU release that re-seeded AppData on every launch; updated to **1.6.2** with correct `1.6.2` / `TouMira-v1.6.2` URL handling.
- **Removed LocalizeUs** from the registry (no GitHub releases published).

### Packs & launch
- **SUS AF pack:** GitHub wins for preset mod lists; Play installs the full pack and blocks partial launches when prep fails.
- **Vanilla Enhancements:** DLL deploys correctly; blocks launch with a clear message when GitHub rate-limits the mod’s startup API check (optional GitHub PAT in Settings reduces manager API use on your IP).
- **Launch:** Waits for Among Us to exit before copying plugins; retries on brief file locks with a clear “close the game first” message.

## 1.0.6 — 2026-06-07

**Use this release instead of 1.0.5.** The 1.0.5 build was stamped as v1.0.4, which caused auto-update to re-download 1.0.5 forever.

### App updates
- Update checks use GitHub’s `/releases/latest` redirect first (no API quota), with API as backup.
- Settings shows a clear message when the update check fails instead of “you’re on the latest”.
- Skips re-downloading when an update is already staged; clears stale staging when a newer release ships.

### Presets & packs
- **SUS AF** display name (bundled metadata wins over stale GitHub cache).
- **Select Pack** stays on the Presets tab (no Installed-page flash).
- First **Install Pack** on SUS AF auto-selects once (wizard-skipped users).
- **TOHE** built-in pack; SUS AF pinned with **FEATURED** badge; install counts (`5/7 installed`).
- **Install Pack** vs **Select Pack**: install = refresh + missing mods; select = refresh + missing + pack mode.

### Mod manager
- Store filters: Not installed / Installed / Has update.
- Play sync surfaces install/update warnings in a dialog.
- **AutoUpdateMods** defaults on; gates startup badge checks only (not auto-install).
- Installed page refreshes live after install/uninstall/update/pack play.
- Registry hardening for AUnlocker, TownOfUsMira, and TOHE.
- Background store refresh failures show a status message.

### Settings & troubleshooting
- Troubleshooting dialog (interop repair, logs, reference path, store refresh) under Danger zone.
- Proton reminder: snooze 7 days or dismiss permanently.

## 1.0.4 — 2026-06-05

Fixes the launch regression where only part of your mod list actually loaded into the game. TOU pack assets no longer wipe out other plugins on startup, and single-DLL mods with subfolders land in `BepInEx/plugins` where they belong.

Mod downloads are more resilient when GitHub rate-limits the API — direct release URLs are tried as a fallback, and the bundled store cache is merged instead of replaced wholesale.

Settings cleanup: dropped the working-mod interop reference field (interop seeding still happens automatically from cache). Custom themes now use the same four swatch colors as the built-in profiles; the rest is derived unless you tick "Edit all colors". You can export and import themes as `.json` files.

Presets page: export your custom packs as shareable `.json` files, or import one someone sent you. Imports land as user presets and won't overwrite built-ins. New built-in **Vanilla+** preset (AUnlocker + Vanilla Enhancements).

Theme editor uses a proper color picker (spectrum/sliders) instead of typing hex codes.

Also fixes the silent startup crash on single-file Windows builds (SkiaSharp native DLL), and the version label reading the wrong assembly.

## 1.0.0 — 2025

First public release. Cross-platform mod manager for Among Us with the SUS AF pack, GitHub + Thunderstore installs, color profiles, first-launch wizard, auto-updates, and danger-zone vanilla restore.
