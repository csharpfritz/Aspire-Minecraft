# Service Switches Are Display-Only

**Date:** 2026-02-11  
**Decided by:** Rocket (Integration Dev)  
**Status:** Informational

## Decision

Service switches (levers + lamps) in the Minecraft village are **display-only** indicators. They reflect the current status of Aspire resources but do not control them.

## Context

User testing revealed confusion: manually flipping a service switch in Minecraft does not affect the corresponding Aspire resource. This is by design.

## Rationale

- **One-way sync:** Aspire resource state → Minecraft visual state
- **Safety:** Prevents accidental resource control from game interface
- **Simplicity:** No reverse RCON → Aspire control flow needed
- **Consistency:** Matches other display-only features (health lamps, hologram, boss bar)

## Implications

- Switches are visual feedback, not interactive controls
- Manually flipping a lever in-game will be overwritten on next update cycle
- If user wants to control resources, they must use Aspire dashboard or CLI
- Documentation should clearly state this is a monitoring/visualization tool, not a control panel

## Related Features

- Health indicator lamps (also display-only)
- Redstone dependency graph (also display-only)
- Holograms, boss bar, scoreboards (all display-only)
