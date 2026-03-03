# Session: Minecart Rails Implementation
**Date:** 2026-03-02
**Requested by:** Jeffrey T. Fritz

## Summary
Rocket removed canal infrastructure (walls, ice, excavation) and fixed rail junction curves.
- Rails now sit on ground surface instead of in trenches
- Junction corners use regular rails (not powered) for auto-curving
- CanalService no longer builds stone brick walls or blue ice floors
- Rails placed at SurfaceY + 1 instead of CanalY

## Decisions
- Rails directly on ground surface (SurfaceY + 1)
- Junction curves use `minecraft:rail` (not `powered_rail`) for auto-curving perpendicular connections
- E-W rail ends at trunkX+1 to ensure proper neighbor detection for L-turns
- Lake landing zone preserved for water transition
