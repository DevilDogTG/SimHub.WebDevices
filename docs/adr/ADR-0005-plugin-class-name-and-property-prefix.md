# ADR-0005: Name the plugin class `WebDevicesPlugin`

**Date:** 2026-10-17
**Status:** accepted
**Deciders:** DevilDogTG (project owner)

## Context
SimHub prefixes every property, event and action a plugin registers with the plugin's class name, and
dashboards and user settings refer to those names. Renaming the class later breaks them.

## Options Considered

### Option A: `DevDogs.WebDevices.WebDevicesPlugin`
Namespace `DevDogs.WebDevices`, class `WebDevicesPlugin`; properties appear as `WebDevicesPlugin.*`.
- **Pros:** Matches the product name; readable in Dash Studio's property picker.
- **Cons:** None significant.

### Option B: A shorter abbreviation (for example `DDWebDevicesPlugin`)
- **Pros:** Shorter prefix.
- **Cons:** Less readable; not the owner's choice.

## Decision
The plugin class is `DevDogs.WebDevices.WebDevicesPlugin`, so its SimHub prefix is
`WebDevicesPlugin.*`, as chosen by the owner.

## Consequences
**Easier:** Stable, readable property names for dashboards.
**Harder:** The class must never be renamed or moved to another type name.
**Follow-up:** None.
