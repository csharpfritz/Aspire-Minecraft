### Village fence perimeter and pathway coordinate conventions

**By:** Rocket
**What:** Added `GetVillageBounds()` and `GetFencePerimeter()` to `VillageLayout` for fence and pathway coordinate calculation. Fence perimeter is 1 block outside village bounds (2 on south/entrance side). Boulevard at `BaseX + StructureSize` (X=17). Cross paths connect each structure entrance (`originX + 3, originZ - 1`) to the boulevard. Fence built before structures in the build order.
**Why:** Any future service placing things around the village edge (torches, banners, lamp posts) should use `GetFencePerimeter()` or `GetVillageBounds()` to stay consistent. The south side has a 2-block offset to accommodate the entry path and fence gate — new services adding south-side features need to account for this.
**Status:** ✅ Implemented. Build passes, 248 tests pass.
