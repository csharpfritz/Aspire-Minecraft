### Sprint 3 service lifecycle: no independent BackgroundServices for RCON-dependent features

**By:** Rocket
**What:** Converted HeartbeatService, RedstoneDependencyService, and ServiceSwitchService from `AddHostedService<>()` (independent BackgroundServices) to `AddSingleton<>()` called by MinecraftWorldWorker. Also fixed beacon tower positions to derive from VillageLayout instead of hardcoded offsets.
**Why:** Independent BackgroundServices start before RCON is connected and before resources are discovered, causing all Sprint 3 features to silently fail. The established pattern (used by WorldBorderService, AdvancementService, BeaconTowerService, etc.) is singleton + nullable constructor injection + calls from the main worker loop. Beacon positions used a hardcoded BaseZ=14 that overlapped with row-1 structures (z=10–16), blocking beacon sky access for 2 of 4 resources.
**Rule:** Any feature service that uses RCON or depends on discovered resources MUST be registered as `AddSingleton<>()` and called from MinecraftWorldWorker — never as an independent `AddHostedService<>()`.
**Status:** ✅ Resolved. All 303 tests pass. Build clean (0 errors, 0 warnings).
