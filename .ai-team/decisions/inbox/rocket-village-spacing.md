# Village Spacing & Fence Clearance Update

**Date:** 2026-02-12
**Author:** Rocket (Integration Dev)
**Requested by:** Jeffrey T. Fritz

## Decision

1. **Village spacing doubled from 12 to 24 blocks** — provides 17-block walking gap between 7×7 structures for a much more spacious village feel.
2. **Fence clearance increased from 4 to 10 blocks** — gives horses and players generous roaming space between buildings and the perimeter fence.
3. **Forceload area expanded from `(-10,-10)-(80,80)` to `(-20,-20)-(120,120)`** — covers the larger village footprint with extra margin.
4. **Horse CustomName uses simple string format** — matches `GuardianMobService` pattern (`CustomName:"\"Name\""`) instead of JSON text component, fixing raw JSON display on Paper servers.
5. **Horse spawn Z moved from `BaseZ-2` to `BaseZ-6`** — centers horses in the wider clearance area.

## Rationale

The original 12-block spacing with 7×7 structures left only a 5-block gap — cramped for visual appeal and horse movement. Doubling to 24 blocks creates a proper village feel with wide streets. The 10-block fence clearance gives horses room to trot without clipping into buildings.

## Impact

All layout-dependent services automatically inherit the new positions via `VillageLayout.GetStructureOrigin()`. Test expectations updated in 4 test files. All 382 tests pass.
