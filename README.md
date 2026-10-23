# DevDogs Web Devices

A SimHub plugin that switches the dashboard on every phone and tablet showing SimHub in a browser,
automatically, by game and by what you are doing in it. Each device opens one fixed URL once,
`http://<simhub-pc>:8889/<device-name>/`, and never needs touching again.

## 🚀 Features
- **One URL per device.** Add devices by name in SimHub; each gets its own page to open in the device's browser.
- **Three states per game.** Different dashboards when no game is running, when a game is in its menus
  (loading, main menu, paused) and while driving, with a default for games you have not set up.
- **Instant, touch-free switching.** Changes are pushed to every device as they happen; game switches in
  SimHub do not disconnect anything.
- **Works on old devices.** Tested design targets are an iPad mini 2 (iOS 12, Safari 12) and a Galaxy S8;
  no app to install, just a browser.
- **Managed inside SimHub.** A "Web Devices" tab lists devices with live connection status and edits every
  mapping; export and import the whole configuration as JSON.
- **No network setup.** Uses SimHub's existing firewall rule; no admin rights, URL reservations or certificates.

## 🛠️ Getting Started

### Prerequisites
- Windows with [SimHub](https://www.simhubdash.com/) (developed against v9.13.2).
- Phones or tablets on the same network as the SimHub PC, with a browser (Safari on iOS 12 or later,
  Chrome on Android).
- SimHub's own web dashboards working on those devices (`http://<simhub-pc>:8888/`).

### Installation
1. Download `DevDogs.WebDevices-<version>.zip` from the releases page.
2. Close SimHub.
3. Copy `DevDogs.WebDevices.dll` and `DevDogs.WebDevices.Core.dll` into the SimHub folder
   (usually `C:\Program Files (x86)\SimHub`).
4. Start SimHub and enable **DevDogs Web Devices** when asked.

### Usage
1. Open **Web Devices** in SimHub's left menu.
2. Click **Add device** and give it a name such as `ipad-left` (lowercase letters, digits, `-` and `_`).
3. Pick dashboards:
   - **When no game is running**: shown while no game is detected.
   - **Any other game**: *In menus* and *Driving* for every game without its own row.
   - **Per game**: pick a game (or **Add current game**) and set its *In menus* and *Driving* dashboards.
     An empty *In menus* shows the driving dashboard.
4. Click **Save**. Changes apply immediately; no restart is needed.
5. On the device, open the URL shown under **Open on device** (use **Copy**). On iOS, *Share → Add to Home
   Screen* gives a full-screen, SimHub-branded launcher.

Set the device's own screen timeout to "never" (or use a kiosk browser): over plain HTTP, browsers cannot
keep the screen awake by themselves.

## 🔀 How switching works

| State | When | Dashboard used (first one set wins) |
|---|---|---|
| **No game running** | No game process and no telemetry | *When no game is running* |
| **In menus** | Game running but loading, in its menus or paused | game *In menus* → *Any other game · In menus* → whatever *Driving* would show |
| **Driving** | Telemetry flowing, not in menus, not paused | game *Driving* → *Any other game · Driving* → *When no game is running* |

- Switching to *No game running* waits 3 s by default, so restarting a game does not flash every device.
  Both delays are in **General**.
- A device whose dashboard does not change is not reloaded.
- Games are matched by SimHub's game code (custom games appear by name in the tab).

## 🩺 Troubleshooting

| Problem | What to check |
|---|---|
| Device page does not load | Same network as the PC? The **Status** line should say *Listening on port 8889*. If it reports the port is in use or reserved by Windows, choose another **Device port**. |
| "… is not configured in SimHub" | The name in the URL must match a saved device. The page picks the device up by itself once it is added and saved. |
| Page loads but stays black | SimHub's own web server must be reachable: try `http://<simhub-pc>:8888/` on the device; check **SimHub web port**. |
| "Reconnecting to SimHub…" | The device lost the connection (Wi-Fi drop, SimHub restart). It recovers by itself; the last dashboard stays on screen meanwhile. |
| Game not detected | SimHub must switch to the game (its current game shows in **Status**). Process detection for custom games does not work for programs running as administrator. |
| Menus show the driving dashboard | Some games never report "in menus". ETS2/ATS report their Esc menu as paused, which counts as in menus; sitting in the truck before driving counts as driving. |

Every switch, connection and raw game flag is logged with a `[WebDevices]` prefix in SimHub's log
(`SimHub\Logs\SimHub.txt`).

## 🏗️ Architecture
- **`DevDogs.WebDevices.Core`** (netstandard2.0, no SimHub dependency): settings and validation, game-state
  detection, dashboard resolution, switch debouncing, the device registry, a minimal HTTP/1.1 server with
  Server-Sent Events, the device page, and the settings-tab view models.
- **`DevDogs.WebDevices`** (.NET Framework 4.8, WPF): the SimHub plugin. Feeds SimHub's game data into a
  shared host that survives SimHub re-creating plugins on every game switch, and provides the settings tab.
- The device page frames SimHub's own dashboard page (`:8888/Dash`) full screen and swaps it when told to.

Design decisions are recorded in [`docs/adr/`](docs/adr/).

## 🤝 Contributing

### Build and test
Needs the .NET 10 SDK and SimHub installed (its assemblies are referenced from the install folder; pass
`-p:SimHubPath=<folder>` if it is not in the default place).
```bash
dotnet build DevDogs.WebDevices.slnx
```
```bash
dotnet test tests/DevDogs.WebDevices.Core.Tests
```

### Try a build in SimHub
Close SimHub first; the script builds and copies the plugin into the SimHub folder.
```bash
pwsh ./build/Install-DevBuild.ps1
```

### Try the device page without a game
The dev host serves device pages from the real server with game states typed in (`idle`, `menu <game>`,
`drive <game>`, `pause <game>`, `reload`, `quit`). SimHub must be running for the dashboards themselves.
```bash
dotnet run --project tools/DevDogs.WebDevices.DevHost -- --port 8890
```

### Package a release
```bash
pwsh ./build/New-ReleasePackage.ps1 -Version 0.1.0
```

## 📄 License
No license has been chosen yet; all rights reserved until one is added.
