# Decision: Grand Watchtower Size-Based Branching

**Date:** 2026-02-12
**Author:** Rocket
**Issue:** #78

## Context

The Grand Watchtower (15×15, 20 tall, 3 floors) needs to coexist with the standard watchtower (7×7, 10 tall). The routing in `BuildResourceStructureAsync` dispatches by structure type ("Watchtower"), not by size.

## Decision

`BuildWatchtowerAsync` checks `VillageLayout.StructureSize` at runtime: if 15 (Grand mode), it delegates to `BuildGrandWatchtowerAsync`; otherwise it builds the standard watchtower. This avoids changing the structure type string or the routing switch.

## Consequences

- **No new structure type string.** `GetStructureType` still returns `"Watchtower"` for all Project resources. Other services (beacons, particles, holograms, service switches) continue to work without modification.
- **Health indicator and Azure banner** adapted with size-based conditionals (`VillageLayout.StructureSize == 15`) for X-centering and roof Y.
- Same pattern can be applied to other grand buildings (Warehouse, Workshop, etc.) without touching the routing layer.
