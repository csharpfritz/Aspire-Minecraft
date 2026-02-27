### 2026-02-27: ErrorBoatService — fixed canal entrance coordinates, added propulsion and CanalService gate
**By:** Rocket
**What:** Fixed three critical bugs in ErrorBoatService:
1. `VillageLayout.GetCanalEntrance` now returns `(ox + StructureSize, CanalY, oz + StructureSize + 4)` — the actual per-building canal position behind the building, not the west side at Z midpoint.
2. Boat summon includes `{Motion:[-0.5,0.0,0.0]}` for westward propulsion toward the trunk canal.
3. ErrorBoatService takes nullable `CanalService?` dependency and gates spawning on `CanalPositions.Count > 0`.
**Why:** Boats were spawning on dry land at wrong coordinates, had no motion on still water, and could spawn before canals existed. All three bugs combined meant error boats were completely non-functional.
**Impact:** VillageLayout.GetCanalEntrance return values changed — any code using these methods now gets the corrected coordinates. Program.cs forceload calculations using GetCanalEntrance are now more accurate (covering actual canal area instead of building midpoints).
