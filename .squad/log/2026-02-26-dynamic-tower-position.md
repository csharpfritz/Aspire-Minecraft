# 2026-02-26: Dynamic Tower Position

**Requested by:** Jeffrey T. Fritz

**What happened:**
- Rocket refactored `GrandObservationTowerService` to compute tower position dynamically from village bounds
- Removed hardcoded `TowerOriginX = 25` and `TowerOriginZ = -45` constants
- Added `SetPosition(int resourceCount)` method that uses `VillageLayout.GetFencePerimeter()` to center tower
- Tower X positioned at village midpoint; Z placed 15 blocks north of fence's north edge
- Updated `Program.cs` to call `SetPosition()` before building the tower
- `NorthGap` constant exposed as `internal const` for test access

**Outcomes:**
- 81 tower tests pass, 2 skipped
- Tower position now adapts to village size and layout changes
