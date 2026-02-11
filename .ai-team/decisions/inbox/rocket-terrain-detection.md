### 2026-02-11: Dynamic terrain detection replaces hardcoded superflat Y=-60

**By:** Rocket
**What:** Added `TerrainProbeService` that uses RCON binary search (`setblock ... keep`) to detect surface height at startup. All village services now use `VillageLayout.SurfaceY` instead of hardcoded `BaseY`. Path building made terrain-agnostic (clears all blocks, not just grass_block). Falls back to BaseY=-60 if detection fails, preserving backward compatibility.
**Why:** The village was hardcoded to Y=-60 (superflat grass layer). This broke on any other world type (normal, amplified, custom). Dynamic detection makes the village work on ANY world type while keeping superflat as the safe fallback. Binary search keeps RCON usage minimal (~8 commands) and the probe is non-destructive (cleans up placed blocks immediately).
