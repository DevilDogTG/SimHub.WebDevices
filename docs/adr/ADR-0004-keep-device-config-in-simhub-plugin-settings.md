# ADR-0004: Keep device and dashboard config in SimHub's plugin settings

**Date:** 2026-10-17
**Status:** accepted
**Deciders:** DevilDogTG (project owner)

## Context
Devices and their game-to-dashboard mappings must be added, changed and removed. An early draft
proposed a JSON file in a GitHub repository as the master copy, pulled by the plugin. The owner wants
to manage everything from a tab inside SimHub.

## Options Considered

### Option A: SimHub plugin settings, edited in a SimHub settings tab
Stored through SimHub's common settings storage; JSON export/import for backup.
- **Pros:** One place to edit; works offline; no sync or conflict handling.
- **Cons:** No built-in history; sharing between PCs is manual (export/import).

### Option B: GitHub repository as master
- **Pros:** Versioned history; easy to share.
- **Cons:** Editing outside SimHub; network dependency; cache and fallback logic.

### Option C: Two-way sync
- **Pros:** Both of the above.
- **Cons:** Conflict handling; the most work for a single-user setup.

## Decision
Store the config in SimHub's plugin settings and edit it in a SimHub settings tab, with JSON
export/import, because the owner manages devices from SimHub and a single master copy needs no sync.

## Consequences
**Easier:** No network or sync code; changes apply immediately.
**Harder:** History and sharing rely on exported JSON files.
**Follow-up:** GitHub sync stays on the "later" list and would build on the export format.
