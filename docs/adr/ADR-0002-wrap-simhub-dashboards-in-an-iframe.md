# ADR-0002: Wrap SimHub's own dashboard page in an iframe, over plain HTTP

**Date:** 2026-10-17
**Status:** accepted
**Deciders:** DevilDogTG (project owner)

## Context
Each device needs a stable URL whose dashboard changes without a touch. SimHub already serves web
dashboards on port 8888 at `/Dash?v=<cachebuster>#<DashCode>|flags`. Verified on SimHub v5042: the
:8888 server sends no `X-Frame-Options` or CSP header, so its pages can be framed. Changing only the
URL hash does not reload the dashboard, so a switch must set a new `v=` value. SimHub's own
`remotecontrolleddash.html` uses the same iframe pattern. Devices are old (iPad mini 2 on iOS 12.5,
Galaxy S8) and on a home LAN.

## Options Considered

### Option A: Wrapper page with a full-screen iframe to :8888
The plugin serves a small page per device that frames SimHub's dashboard and swaps the iframe URL.
- **Pros:** No changes to SimHub; the plugin stays small; dashboards render exactly as SimHub draws them.
- **Cons:** Depends on SimHub continuing to allow framing; HTTPS would cause mixed-content blocking.

### Option B: Reverse proxy SimHub through the plugin
The plugin relays all of :8888's pages, assets and live data (including WebSockets), optionally over TLS.
- **Pros:** Works even if framing were blocked; allows HTTPS (Screen Wake Lock).
- **Cons:** Several times the code and failure surface; must track SimHub's internal protocol.

### Option C: Redirect the device to the dashboard URL
The device page navigates to the dashboard directly.
- **Pros:** Simplest.
- **Cons:** The device leaves our page, so nothing can switch it again.

## Decision
Serve a per-device wrapper page that frames SimHub's dashboard over plain HTTP, because framing is
verified to work and it avoids reimplementing SimHub's web traffic.

## Consequences
**Easier:** Small plugin; no TLS certificates; the dashboard's own controls and scaling keep working.
**Harder:** No Screen Wake Lock (needs HTTPS); devices stay awake through OS settings or a kiosk app.
If a future SimHub version blocks framing, this ADR must be revisited (Option B).
**Follow-up:** Re-check framing headers after SimHub upgrades.
