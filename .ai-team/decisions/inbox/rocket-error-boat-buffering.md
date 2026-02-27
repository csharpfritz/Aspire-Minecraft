### 2026-02-27: ErrorBoatService uses buffering pattern to handle initialization race

**By:** Rocket

**What:** ErrorBoatService now buffers health status changes that arrive before canals are built, then replays them once CanalService.InitializeAsync() completes. Program.cs explicitly calls `errorBoats.SpawnBoatsForChangesAsync(Array.Empty<ResourceStatusChange>())` after canal initialization to trigger the replay.

**Why:** Health monitoring happens at the start of each worker loop, but canal construction happens mid-loop after structures are built (to avoid being paved over). If a resource transitions to unhealthy during iteration 1 (before canals exist), the change event was lost because AspireResourceMonitor only emits transition events — by iteration 2, the resource is already unhealthy and no new event fires. Buffering pending changes ensures no transitions are missed, while preserving the "structures before canals" ordering constraint.

**Pattern:** When service A depends on service B's initialization and both are idempotent, use a pending-change buffer in A rather than reordering initialization. This works when B must run after C for spatial reasons (canals after structures), but health events arrive before C completes.
