### Canal System Junction & Routing Fix
**By:** Rocket
**Date:** 2026-02-19
**Files:** `src/Aspire.Hosting.Minecraft.Worker/Services/CanalService.cs`

**Decision:** Canal trunk-to-branch connectivity uses a post-pass junction-carving approach.

**Context:** The trunk canal is built AFTER all branch canals (to compute correct Z-range). This means the trunk's solid west wall overwrites branch canal endpoints. Rather than changing build order (which would require pre-computing the trunk Z-range), we added `OpenBranchJunctionsAsync` that runs after the trunk and carves openings at each branch's Z-level.

**Key choices:**
1. **Post-pass junction carving** (not build-order change): Simpler, doesn't require refactoring the trunk Z-range computation that depends on all branch canal entrances.
2. **Detour Z-reset**: Branch canals now return to their original Z after detouring around blocking buildings, ensuring consistent junction Z-positions at the trunk.
3. **Bridge elevation at SurfaceY + 1**: Connector bridges raised one block above canal wall tops with oak_fence railings at SurfaceY + 2.

**Impact:** All canal-related services (ErrorBoatService, MinecartRailService bridge detection) should assume branch canals always arrive at the trunk at their original entrance Z, not a detoured Z.
