# ADR-0001: Target the .NET Framework 4.8 runtime, build with the .NET 10 SDK

**Date:** 2026-10-17
**Status:** accepted
**Deciders:** DevilDogTG (project owner)

## Context
SimHub loads plugins into its own process, which runs on .NET Framework 4.8. A plugin assembly must
target a framework that process can load, and must reference SimHub's own assemblies
(`SimHub.Plugins`, `GameReaderCommon`, `SimHub.Logging`) from the local install rather than shipping
copies. The routing logic (state detection, dashboard resolution, debouncing) should be testable
without SimHub running.

## Options Considered

### Option A: net48 plugin + netstandard2.0 core, built with the modern SDK
SDK-style projects built by the .NET 10 SDK; the plugin targets `net48`, the core targets `netstandard2.0`.
- **Pros:** One modern toolchain (`dotnet build`, central package management, `slnx`); the core is
  unit-testable on `net10.0`; latest C# via PolySharp.
- **Cons:** Needs the .NET Framework 4.8 targeting pack; some newer BCL APIs are unavailable.

### Option B: single net48 project, legacy tooling
One classic project built with Visual Studio / MSBuild.
- **Pros:** Mirrors many SimHub plugin samples.
- **Cons:** Logic tied to SimHub types is hard to test; older tooling and language version.

## Decision
Build with the .NET 10 SDK; the plugin targets `net48` and all SimHub-free logic lives in a
`netstandard2.0` core tested on `net10.0`, because that keeps the logic testable without SimHub.

## Consequences
**Easier:** Fast unit tests for routing rules; consistent builds via `global.json`.
**Harder:** Core code must stay within `netstandard2.0` APIs; SimHub assemblies are referenced from
the install folder (`SimHubPath`), so building the plugin needs SimHub installed.
**Follow-up:** Newtonsoft.Json is compiled against but never shipped (SimHub provides 13.0.0.0).
