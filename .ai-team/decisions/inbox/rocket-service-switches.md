### Service Switches — visual-only levers representing resource status (Issue #35)

**By:** Rocket
**Issue:** #35

**What:** Added `ServiceSwitchService` (BackgroundService) with `WithServiceSwitches()` extension method and `ASPIRE_FEATURE_SWITCHES` env var. Places levers and lamps on each resource structure. Healthy = lever ON + glowstone, Unhealthy = lever OFF + unlit redstone lamp.

**Key decision:** Visual only — levers reflect state, they do not control Aspire resources. The `ResourceNotificationService` is read-only from the worker's perspective, so programmatic start/stop of individual resources is not possible.

**Placement:** Lever at `(x+1, y+2, z)`, lamp at `(x+1, y+3, z)` on the front wall of each structure. Uses glowstone/redstone_lamp swap pattern (same as StructureBuilder health indicator) rather than redstone signal propagation.

**Status:** ✅ Implemented. Build passes (0 errors), 303 tests pass.
