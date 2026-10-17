# ADR-0003: Push dashboard changes with Server-Sent Events, polling as fallback

**Date:** 2026-10-17
**Status:** accepted
**Deciders:** DevilDogTG (project owner)

## Context
Devices must switch within about two seconds of a game or state change and reconnect by themselves
after Wi-Fi drops or a SimHub restart. The oldest client is Safari 12 on iOS 12.5. The plugin hosts
its own HTTP server on .NET Framework 4.8.

## Options Considered

### Option A: Server-Sent Events
A long-lived `text/event-stream` response per device; the plugin writes an event on each switch.
- **Pros:** Plain HTTP, simple to serve from `HttpListener`; the browser's `EventSource` reconnects
  automatically; supported by Safari 12 and Android Chrome; the open stream doubles as "connected".
- **Cons:** One-way only (enough here); proxies can buffer it (not a concern on a LAN).

### Option B: WebSocket
- **Pros:** Two-way; what SimHub's own remote dashboard uses.
- **Cons:** More server code (handshake, framing, pings) on `HttpListener`; manual reconnect logic.

### Option C: Polling only
- **Pros:** Simplest server.
- **Cons:** Latency vs. load trade-off; constant traffic from every device.

## Decision
Use Server-Sent Events for push, with a `GET /<device>/state` polling fallback for clients where the
stream fails, because it meets the latency goal with the least server and client code.

## Consequences
**Easier:** Automatic reconnect; connection tracking from open streams.
**Harder:** No device-to-server channel; anything a device must send later needs a separate request.
**Follow-up:** Send a heartbeat comment about every 15 s so dead connections are detected.
