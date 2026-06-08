# Changelog

## Unreleased

- **Settings troubleshooting:** Reactor/interop status, repair interop, interop reference folder, open app data / BepInEx log, manual store refresh.
- **Store filters:** Not installed / Installed / Has update.
- **Presets:** TOHE built-in pack; SUS AF pinned with FEATURED badge; install counts (`5/7 installed`) based on files on disk.
- **Play sync:** Pack play surfaces install/update warnings in a dialog instead of failing silently.
- **AutoUpdateMods:** Toggle now gates startup update checks only (badges, not auto-install). Label clarifies behavior.
- **Installed page:** Refreshes live after install, uninstall, update, or pack play without switching tabs.
- **Proton reminder:** “Remind me later” snoozes for 7 days; “Got it” dismisses permanently.
- **Registry:** Explicit DLL filters for AUnlocker, TownOfUsMira, and TOHE pack mods.
- **Install vs Select Pack:** Install = refresh + missing mods only; Select = refresh + missing + pack mode. Wizard uses Select. First SUS AF **Install Pack** also selects the pack (once), so skipping the wizard still activates it for Play.
- **AutoUpdateMods:** Defaults on unless explicitly opted out; Installed first-visit check respects the toggle.
- **Background refresh:** Failed GitHub store refresh shows a status message instead of failing silently.

## 1.0.4 — 2026-06-05

Fixes the launch regression where only part of your mod list actually loaded into the game. TOU pack assets no longer wipe out other plugins on startup, and single-DLL mods with subfolders land in `BepInEx/plugins` where they belong.

Mod downloads are more resilient when GitHub rate-limits the API — direct release URLs are tried as a fallback, and the bundled store cache is merged instead of replaced wholesale.

Settings cleanup: dropped the working-mod interop reference field (interop seeding still happens automatically from cache). Custom themes now use the same four swatch colors as the built-in profiles; the rest is derived unless you tick "Edit all colors". You can export and import themes as `.json` files.

Presets page: export your custom packs as shareable `.json` files, or import one someone sent you. Imports land as user presets and won't overwrite built-ins. New built-in **Vanilla+** preset (AUnlocker + Vanilla Enhancements).

Theme editor uses a proper color picker (spectrum/sliders) instead of typing hex codes.

Also fixes the silent startup crash on single-file Windows builds (SkiaSharp native DLL), and the version label reading the wrong assembly.

## 1.0.0 — 2025

First public release. Cross-platform mod manager for Among Us with the SUS AF PACK, GitHub + Thunderstore installs, color profiles, first-launch wizard, auto-updates, and danger-zone vanilla restore.
