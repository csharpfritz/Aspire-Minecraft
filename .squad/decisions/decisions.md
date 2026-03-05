# Team Decisions Log

## Active Decisions

### Bridge Clearance Standard: SurfaceY+4 Deck Height

**Date:** 2025-07-18  
**By:** Rocket  
**Status:** Decided  

Bridge deck height is SurfaceY+4 (3 blocks above rail height at SurfaceY+1). Air clearing for bridges starts at SurfaceY+2, never SurfaceY+1, to preserve rails underneath. Bridge supports use `oak_fence` instead of solid blocks so minecarts can pass through at rail height.

**Why:** A minecart (~0.7 blocks) carrying a creeper (~1.7 blocks) needs ~2.5 blocks clearance. SurfaceY+2 deck only gave 1 block above rails — guaranteed collision. The old air clear at SurfaceY+1 destroyed E-W rails placed by CanalService before BridgeService ran, breaking the entire rail network at bridge intersections. `stone_bricks` supports at SurfaceY+1 were solid blocks on the rail plane — minecarts can't pass through solid blocks but CAN pass through `oak_fence` posts.

**Impact:**
- BridgeService.cs: `deckY = sy + 4`, 3-step ramps, fence supports
- Any future bridge types must follow the same clearance standard
- Build order (canals → bridges → rails) is now safe because bridges don't clear rail height

---

### Remove HealthCheckAnnotation.Key from WithMonitoredResource

**Date:** 2026-03-02  
**By:** Rocket  
**Status:** Implemented  

Removed the `HealthCheckAnnotation` extraction block from `WithMonitoredResource` in `MinecraftServerBuilderExtensions.cs` entirely. Without a `HEALTH_PATH` env var, `AspireResourceMonitor.CheckHttpHealthAsync` falls through to check the base URL, which the API already maps to return 200 (healthy) or 503 (unhealthy).

**Context:** `HealthCheckAnnotation.Key` extracted was the health check **registration name** (e.g. `"api_HttpHealthCheck"`), not a URL path. This caused the worker to poll `https://host/api_HttpHealthCheck` which returned 404, making healthy resources appear permanently unhealthy in Minecraft.

**Impact:**
- **Worker:** Now correctly detects resource health via base URL polling.
- **Error minecarts:** Will only spawn when resources actually go unhealthy (via "Trigger Error" button), not due to false-negative health detection.
- **No breaking changes:** Resources without explicit health paths were already handled by the base-URL fallback.
