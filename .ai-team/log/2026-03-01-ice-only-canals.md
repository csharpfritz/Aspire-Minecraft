# 2026-03-01: Ice-Only Canals Implementation

**Requested by:** Jeffrey T. Fritz

## Summary
User directive to remove water from canals entirely and use blue_ice-only design. Boats slide through town to the lake naturally via Minecraft ice physics (near-zero friction allows Motion NBT to work, unlike water where Minecraft overrides it every tick). Lake at south end retains water — boats transition from ice to water at the junction.

## Changes
- **CanalService:** Remove water fills, use blue_ice blocks only
- **ErrorBoatService:** Restore Motion NBT functionality for boats on ice
- **VillageLayout:** Adjust spawn Y for ice-based approach

## Status
Rocket spawned for implementation.

## Rationale
Eliminates need for teleportation hacks. Blue ice friction model enables reliable boat motion control via NBT tags.
