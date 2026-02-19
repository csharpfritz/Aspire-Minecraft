### 2026-02-19: Village layout flattened to single row

**By:** Rocket

**What:** Changed VillageLayout from 2x2 zone quadrant grid (NW/NE/SW/SE) to single horizontal row layout. All buildings now placed at BaseZ (same Z level), extending along X axis with Spacing=36 increments. Zones placed sequentially: Zone1 (DotNetProject) → Zone2 (Azure) → Zone3 (ContainerOrDatabase) → Zone4 (Executable), all at same depth.

**Why:** Jeff Fritz reported "TWO LEVELS to the town" when expecting a single row. The 2-column grid (Columns=2) caused buildings to wrap into multiple rows, and 4-zone quadrant layout created vertical stacking. Flattening to single row ensures:
1. All buildings visible in one horizontal sweep (no Z-depth navigation needed)
2. Back canal can run behind ALL buildings in one straight E-W line
3. Clearer visual organization — zones laid out left-to-right like city blocks
4. Scales horizontally (limited only by X dimension, not Z wrapping)

**Changes:**
- `PlanNeighborhoods`: Zones placed sequentially along X at BaseZ, not 2x2 quadrants
- `AddZone` lambda: Single row per zone (all at originZ, x = originX + i*Spacing)
- `GetStructureOrigin(int index)`: Simplified to x = BaseX + index*Spacing, z = BaseZ (constant)
- `GetVillageBounds` fallback: maxZ = BaseZ + StructureSize - 1 (one structure deep)
- Removed unused `ZoneRows` helper (no longer needed)
- CanalService unchanged — `GetVillageBounds` automatically adjusts back canal to run behind new single row

**Impact:** All existing services (StructureBuilder, MinecartRailService, FenceService, WorldBorderService) automatically adapt via VillageLayout API — no changes needed outside VillageLayout.cs. Canal system now correctly positions back canal behind entire single-row village.
