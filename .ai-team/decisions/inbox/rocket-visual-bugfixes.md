### Visual Bug Fixes: Structure Elevation & Health Lamp Alignment

**By:** Rocket
**What:** Fixed two visual bugs: (1) structures placed 1 block below ground — `GetStructureOrigin()` now returns `SurfaceY + 1`; (2) Warehouse health lamp overlapping cargo door — lamp moved to `y+4` for structures with 3-tall doors.
**Why:** SurfaceY is the topmost solid block. Placing floors there replaces the grass and buries walls. Health lamps at `y+3` overlap 3-tall doors (y+1 to y+3). Both fixes are surgical (VillageLayout.cs, StructureBuilder.cs) with updated tests.
**Status:** ✅ Resolved
