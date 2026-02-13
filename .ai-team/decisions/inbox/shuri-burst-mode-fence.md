### RCON Burst Mode: No-Op on Re-Entry (#85)

**By:** Shuri
**What:** `EnterBurstMode()` now returns a no-op `IDisposable` instead of throwing `InvalidOperationException` when burst mode is already active.
**Why:** Callers using nested `using` blocks (e.g., multiple services building concurrently) don't need try/catch. The first caller owns the burst session; subsequent callers get a harmless no-op disposable. Thread safety maintained via `SemaphoreSlim.Wait(0)`.

### Fence/Forceload Grand Village Verification (#84)

**By:** Shuri
**What:** Verified all fence, path, and forceload code already uses dynamic `VillageLayout` properties — no hardcoded values remain.
**Why:** Prior sprint (#84 history entry) already converted gate position to `BaseX + StructureSize`, fence clearance to `VillageLayout.FenceClearance`, forceload to `GetFencePerimeter(10)`, and `MAX_WORLD_SIZE` to 512. No changes were needed.
