# Changelog

All notable changes to SUSSYMODMANAGER are documented here. This project adheres to
[Semantic Versioning](https://semver.org/) and the format is based on
[Keep a Changelog](https://keepachangelog.com/).

## [1.0.0] - Unreleased

First public release of SUSSYMODMANAGER — a modern, cross-platform Among Us mod manager.

### Added
- Cross-platform desktop app (Windows, macOS, Linux) built on .NET 8 + Avalonia.
- One-click **SUS AF PACK** with full dependency + BepInEx handling.
- Mod Store backed by GitHub Releases and the Thunderstore Among Us community.
- Color profiles (including **Bubblegum Pink** and **Toxic Green**) with custom accent support.
- First-launch onboarding wizard (game path, theme, optional SUS AF PACK).
- Automatic self-update: downloads the correct OS/arch build and applies it on restart.
- Live mod-store updates pulled from the repo `data/` folder — no app release needed.
- Danger Zone: convert to vanilla (keep downloads) or full wipe, both with confirmation.
- Crash logging with rotation to the app data folder, plus global exception handlers.
- App icon and proper platform packaging: a per-user Windows `Setup.exe` installer (Inno Setup),
  macOS `.app` + `.icns`, and Linux `.desktop` + icon + `run.sh`.

### Changed
- Version is now driven by the git tag (CI `-p:Version`) and read from the assembly at runtime;
  no more hand-edited version constants.
- Install/preset/update operations now surface warnings and errors in a dialog instead of dropping
  them silently; the status bar reflects success vs error.
- Mod bundles can no longer overwrite the manager's BepInEx core (prevents version downgrades).

### Fixed
- BepInEx version detection reads the actual DLL product version and forces a clean reinstall when
  an outdated build is found.
- Updater never serves a wrong-architecture build; it falls back to the releases page when no
  matching asset exists.
- Release builds are no longer published as a self-extracting single-file binary, which commonly
  triggered Windows Defender false positives; each release is now a plain self-contained folder.
