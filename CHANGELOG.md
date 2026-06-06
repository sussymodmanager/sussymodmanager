# Changelog

## 1.0.4 — 2026-06-05

Fixes the launch regression where only part of your mod list actually loaded into the game. TOU pack assets no longer wipe out other plugins on startup, and single-DLL mods with subfolders land in `BepInEx/plugins` where they belong.

Mod downloads are more resilient when GitHub rate-limits the API — direct release URLs are tried as a fallback, and the bundled store cache is merged instead of replaced wholesale.

Settings cleanup: dropped the working-mod interop reference field (interop seeding still happens automatically from cache). Custom themes now use the same four swatch colors as the built-in profiles; the rest is derived unless you tick "Edit all colors". You can export and import themes as `.json` files.

Also fixes the silent startup crash on single-file Windows builds (SkiaSharp native DLL), and the version label reading the wrong assembly.

## 1.0.0 — 2025

First public release. Cross-platform mod manager for Among Us with the SUS AF PACK, GitHub + Thunderstore installs, color profiles, first-launch wizard, auto-updates, and danger-zone vanilla restore.
