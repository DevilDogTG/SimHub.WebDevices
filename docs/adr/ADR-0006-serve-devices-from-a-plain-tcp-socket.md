# ADR-0006: Serve devices from a minimal HTTP server on a plain TCP socket

**Date:** 2026-10-17
**Status:** accepted
**Deciders:** DevilDogTG (project owner)

## Context
The plugin must accept HTTP from other devices on the LAN (default port 8889) while running inside
SimHub's process on .NET Framework 4.8. Checked on the owner's PC:
- SimHub's installer adds an inbound firewall rule for `SimHubWPF.exe` covering any port, protocol and
  profile. SimHub's own :8888 server (Kestrel, socket-based) is reachable through that rule alone.
- There is no URL reservation for SimHub, and none for port 8889.

The routes are few and simple: a static page, a JSON poll and a Server-Sent Events stream, all bodiless GETs.

## Options Considered

### Option A: `HttpListener` (http.sys)
- **Pros:** A full, well-tested HTTP stack in the base library.
- **Cons:** Listening on all interfaces needs a URL reservation (`netsh http add urlacl`, admin) or
  running SimHub as admin. http.sys receives traffic in the kernel, so the program firewall rule for
  `SimHubWPF.exe` does not apply and a separate port rule (admin) is also needed. Every user would have
  a one-time elevated setup step, plus a localhost-only fallback and UI to explain it.

### Option B: Minimal HTTP/1.1 server on `TcpListener`
- **Pros:** No URL reservation; the socket belongs to `SimHubWPF.exe`, so SimHub's existing firewall rule
  covers it. Zero setup for users. Small, fully under test (routing and parsing are pure functions).
- **Cons:** Hand-written HTTP handling; only what the device page needs (GET/HEAD, no request bodies,
  no keep-alive except the event stream).

### Option C: Host ASP.NET Core / Kestrel inside the plugin
- **Pros:** Full HTTP stack without http.sys.
- **Cons:** Large dependency set that must match whatever versions SimHub already loads; high risk of
  assembly conflicts inside SimHub's process.

## Decision
Use a minimal HTTP/1.1 server on a dual-mode `TcpListener`, because it needs no admin setup or firewall
change on a standard SimHub install and the required HTTP surface is tiny.

## Consequences
**Easier:** Devices connect right after installing the plugin; no elevation, banner or grant button.
**Harder:** HTTP features beyond the device page's needs are not supported; every non-stream response
closes its connection. If a firewall blocks `SimHubWPF.exe` (for example the rule was removed), devices
cannot connect to :8888 either, so the fix is the same for both.
**Follow-up:** If SimHub ever drops its program firewall rule, document adding one for port 8889.
