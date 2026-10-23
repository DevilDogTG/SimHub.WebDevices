# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added
- Per-device URLs `http://<simhub-pc>:8889/<device-name>/` that show SimHub web dashboards full screen.
- Automatic dashboard switching per game and per state: no game running, in menus (loading, menus,
  paused) and driving, with defaults for unmapped games and a fallback chain for empty settings.
- Switch delays, so a game restart does not flash every device (3 s before "no game running" by default).
- Live push to devices over Server-Sent Events, with polling fallback, automatic reconnect and a
  "Reconnecting…" notice that keeps the last dashboard on screen.
- Device connections survive SimHub re-creating plugins on every game switch.
- "Web Devices" settings tab in SimHub: devices with live status and copyable URLs, per-game mappings with
  SimHub's game list and "Add current game", dashboard pickers, general settings, validation and
  warnings, Save/Revert, JSON export and import.
- "Not configured" page for unknown device names, picked up automatically once the device is added.
- `[WebDevices]` log lines for switches, connections and SimHub's raw game flags.
- Developer tools: dev install script, release package script and a dev host for testing the device page
  without a game.
