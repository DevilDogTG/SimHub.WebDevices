# ADR-0007: Keep the device server alive across SimHub's plugin instances

**Date:** 2026-10-17
**Status:** accepted
**Deciders:** DevilDogTG (project owner)

## Context
Observed on SimHub v9.13.2: on every game switch SimHub restarts its plugin manager. It calls `End` on
the current plugin instance, creates a **new** instance of every plugin and calls `Init` on it, all within
about 100 ms. With the server, router and device registry owned by the plugin instance, each game switch:
- stopped the server, dropping every device's event stream (devices reconnected about 2 s later);
- wiped the device registry (connected / last seen), which the settings tab will display;
- reset the debounce timing and the routed state.

A game switch is exactly the moment devices must switch dashboards, so it must not be the moment they
lose their connection.

## Options Considered

### Option A: Process-wide host shared by plugin instances
A `RouterHost` (router, registry, server, clock) held in a static `SharedInstance<T>`. `Init` acquires it
(creating it the first time) and applies the settings; `End` releases it. The host is disposed only after
nobody has held it for a grace period (10 s), which never happens on a game switch.
- **Pros:** Devices stay connected; a game switch is just a state change pushed over the open stream;
  registry history and debounce timing survive. Same port, no rebind race.
- **Cons:** State lives in a static; the grace timer needs care (covered by tests).

### Option B: Keep per-instance ownership, rely on reconnects
- **Pros:** No static state.
- **Cons:** Every game switch shows a reconnect gap on every device and loses the registry; on slow
  devices (iPad mini 2) the page reload adds more delay.

## Decision
Hold the router, registry, server and clock in a process-wide `RouterHost` shared through
`SharedInstance<T>`, because game switches re-create the plugin and must not drop device connections.

## Consequences
**Easier:** Seamless switching on game change; the settings tab can show stable connection history.
**Harder:** Plugin instances are thin adapters; anything stateful belongs in the host. On SimHub exit the
host is not disposed explicitly (the process ends, closing the socket).
**Follow-up:** If SimHub ever stops re-creating plugins, the design still works unchanged.
