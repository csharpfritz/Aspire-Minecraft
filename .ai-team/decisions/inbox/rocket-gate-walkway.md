# Decision: Gate Centering + Cobblestone Walkway + Tower Entrance Cleanup

**Date:** 2026-02-27
**Author:** Rocket
**Status:** Implemented

## Context

The village fence gate was positioned at `BaseX + StructureSize` (aligned with the boulevard between buildings), not centered on the village. The observation tower entrance had a single oak door and no visual connection to the village.

## Decisions

1. **Gate centered on village midpoint** — `villageCenterX - gateWidth / 2` instead of hardcoded boulevard offset. Aligns gate directly opposite the tower entrance.

2. **Walkway owned by GrandObservationTowerService** — the 5-wide cobblestone walkway between tower and gate lives in `BuildWalkwayAsync` inside the tower service, because the tower already stores both its position and the fence bounds from `SetPosition`. No new service needed.

3. **Forceload extended to cover walkway gap** — the 15-block gap between tower and fence may span unloaded chunks, so `ForceloadAsync` now extends south to `fenceMinZ`.

4. **Double doors in exterior phase** — door placement moved from `BuildFloor1EntranceHallAsync` to `BuildExteriorAsync` to ensure correct ordering after the `air` fill clears the opening. Two oak doors (hinge=right + hinge=left) placed side by side.

5. **Stone brick threshold** — placed at `z2 + 1` (outside the tower wall) so players step up cleanly from the walkway.

## Impact

- `StructureBuilder.cs`: Gate X calculation changed (2 lines)
- `GrandObservationTowerService.cs`: New fields `_villageCenterX`/`_fenceMinZ`, extended forceload, new `BuildWalkwayAsync`, entrance refactored with double doors and threshold
- `GrandObservationTowerTests.cs`: `Tower_AllBlocksWithinFootprint` updated to allow walkway blocks south of tower
