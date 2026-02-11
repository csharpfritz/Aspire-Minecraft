# Project Context

- **Owner:** Jeffrey T. Fritz (csharpfritz@users.noreply.github.com)
- **Project:** Aspire.Hosting.Minecraft — .NET Aspire integration for Minecraft servers
- **Stack:** C#, .NET 10, Docker, Aspire, OpenTelemetry, Minecraft Paper Server, RCON
- **Created:** 2026-02-10

## Key Facts

- Worker service (Aspire.Hosting.Minecraft.Worker) handles in-world display
- Uses RCON to communicate with Minecraft server for commands
- DecentHolograms plugin for in-world holograms
- Worker is created internally by WithAspireWorldDisplay<TWorkerProject>()
- WithMonitoredResource() applies env vars to the internal worker
- Metrics: TPS, MSPT, players online, worlds loaded, RCON latency
- `VillageLayout` static class centralizes all per-resource position calculations (2×N grid, 10-block spacing, 7×7 footprint)

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### Consolidated Summary: Sprints 1-3 (2026-02-10)

**Worker architecture:** `MinecraftWorldWorker` (BackgroundService) polls every 10s, broadcasts every 2 min. Core services: `RconService` (OTEL tracing, 250ms dedup throttle, token-bucket rate limiter at 10 cmd/s), `AspireResourceMonitor`, `HologramManager`, `ScoreboardManager`, `StructureBuilder`, `PlayerMessageService`. Resource discovery via `ASPIRE_RESOURCE_{NAME}_TYPE/URL/HOST/PORT/DEPENDS_ON` env vars.

**Feature opt-in pattern (all 13 features):** `With{Feature}()` extension method sets `ASPIRE_FEATURE_{NAME}=true` env var, conditional DI registration in Worker Program.cs, nullable constructor injection. State tracking avoids redundant RCON commands.

**Sprint 1 features (5):** ParticleEffects, TitleAlerts, WeatherEffects, BossBar (with optional appName via `ASPIRE_APP_NAME`), SoundEffects. Particles/titles/sounds per-resource; weather/boss bar aggregate.

**Sprint 2 features (5):** ActionBarTicker (cycles TPS/MSPT/health/latency), BeaconTowers (resource-type-colored glass matching Aspire dashboard palette), Fireworks (all-recover event), GuardianMobs (iron golem=healthy, zombie=unhealthy with NoAI/Invulnerable NBT), DeploymentFanfare (Unknown->Healthy transitions).

**Sprint 3 features (3 + village rework):**
- **Resource Village** (#25): 4 themed structures (Watchtower/Warehouse/Workshop/Cottage) in 2x N grid. `fill ... hollow` for walls. Health lamp in front wall.
- **Village Fence** — oak fence perimeter via `GetVillageBounds()`/`GetFencePerimeter()`. Boulevard at X=17, cross paths to each entrance.
- **Heartbeat** (#27): First `BackgroundService` feature — independent 1-4s pulse loop. Volume micro-variation avoids RCON dedup throttle.
- **Achievements** (#32): 4 milestones via RCON titles+sounds (no datapacks). Per-session `HashSet<string>` tracking.
- **Redstone Graph** (#36): L-shaped wire routing, repeaters every 15 blocks, circuit breaking on unhealthy. `CommandPriority.Low` for bulk building.
- **Service Switches** (#35): Visual-only levers+lamps on structures. Levers reflect state, cannot control resources.

**Key RCON learnings:**
- Identical commands in tight loops get deduped by throttle — use unique strings or micro-vary parameters.
- `fill ... hollow` is the most efficient wall-building command.
- `setblock` doesn't propagate redstone signals — use glowstone/redstone_lamp swap instead.
- Redstone wire auto-connects; repeater `facing` must point toward destination.
- Mob cleanup via `kill @e[name=...]` selector is the standard entity management pattern.
- NBT tags (`NoAI`, `Invulnerable`, `PersistenceRequired`) essential for stationary display mobs.

**Startup optimizations:** `SPAWN_PROTECTION=0`, `VIEW_DISTANCE=6`, `SIMULATION_DISTANCE=4`, `GENERATE_STRUCTURES=false`, no mob spawning, `MAX_WORLD_SIZE=256`.

### Azure Resource Visualization Design (2026-02-10)

Design doc (`docs/epics/azure-minecraft-visuals.md`) mapping 15 Azure resource types to Minecraft structures. Two-universe separation: Aspire village (warm wood/stone) vs Azure citadel (cool prismarine/quartz/end stone) at X=60. 3-column tiered layout by functional tier. Azure beacon colors: Compute=cyan, Data=blue, Networking=purple, Security=black, Messaging=orange, Observability=magenta. Rich health states: Stopped=cobwebs, Deallocated=soul sand, Failed=netherrack fire. Scale: 3x5 grid for <=15, multiple Z-offset planes for 50+.

### Team Updates

- NuGet packages blocked — floating deps fixed in Sprint 1 — decided by Shuri
- 3-sprint roadmap adopted — decided by Rhodey
- All sprint work tracked as GitHub issues with labels — decided by Jeffrey T. Fritz
- Single NuGet package consolidation + PackageId renamed to Fritz.Aspire.Hosting.Minecraft — decided by Jeffrey T. Fritz, Shuri
- NuGet package version defaults to 0.1.0-dev; CI overrides via -p:Version — decided by Shuri
- Release workflow extracts version from git tag — decided by Wong
- Sprint 2 API review complete — 5 recommendations for Sprint 3 — decided by Rhodey
- WithServerProperty API and ServerProperty enum added — decided by Shuri
- Beacon tower colors match Aspire dashboard palette — decided by Rocket
- Hologram line-add bug fixed (RCON throttle dedup) — decided by Rocket
- Azure RG epic designed — Rocket owns Phase 2 (Azure structure mapping, visuals) — decided by Rhodey
- Azure monitoring ships as separate NuGet package Fritz.Aspire.Hosting.Minecraft.Azure — decided by Rhodey, Shuri

### Sprint 3 Bug Fixes (service startup race + beacon overlap)

**Bug 1 — Sprint 3 services not running:** HeartbeatService, RedstoneDependencyService, and ServiceSwitchService were registered as `AddHostedService<>()` (independent BackgroundServices). They started executing immediately before RCON was connected and before resources were discovered, causing silent failures. Fix: converted all three to plain singleton classes (`AddSingleton<>()`) and wired them into `MinecraftWorldWorker`'s main loop — same pattern as WorldBorderService, AdvancementService, etc.

- HeartbeatService: removed BackgroundService inheritance, converted to `PulseAsync()` method called each worker cycle with internal time-based gating (only plays sound when enough time has elapsed since last pulse based on health-derived interval).
- RedstoneDependencyService: removed BackgroundService inheritance, added `InitializeAsync()` (called once after DiscoverResources) and `UpdateAsync()` (called on health changes).
- ServiceSwitchService: removed BackgroundService inheritance, added `UpdateAsync()` (called each worker cycle; places switches on first call, then tracks transitions).

**Bug 2 — Only 2 of 4 beacon beams visible:** Beacon positions used hardcoded `BaseZ = 14` with row-based offsets. For row 1+ structures (index 2, 3), the 7×7 structure footprint (z=10 to z=16) overlapped the beacon at z=14, blocking sky access. Fix: replaced hardcoded position calculation with `GetBeaconOrigin(index)` that derives position from `VillageLayout.GetStructureOrigin(index)` and places the beacon behind the structure at `z + StructureSize + 1`. This guarantees no overlap regardless of grid size.

**Key learning:** Any service that depends on RCON or discovered resources must NOT be an independent `BackgroundService`. It must be a singleton called from `MinecraftWorldWorker`'s main loop, which handles the RCON wait and resource discovery lifecycle.

### Sprint 3.1 Village Placement & Bug Fixes (consolidated, 2026-02-10 to 2026-02-11)

Multiple iterative fixes to village rendering, consolidated here. Final state of all placements:

- **Fences:** `BaseY` (y=-60), 4-block gap from buildings on all sides. Gate at boulevard X=17.
- **Paths:** `BaseY - 1` (y=-61) with grass cleared first via `/fill ... minecraft:air replace grass_block`. Recessed flush with terrain.
- **Service switches:** Front wall at `(x+2, y+2, z)` facing north, lamp at `(x+2, y+3, z)`. Self-healing (placed every cycle). Display-only — cannot control Aspire resources.
- **Doors:** 2-blocks wide (x+2 to x+3). Door clearing runs LAST in structure build to avoid overwrites.
- **Health lamps:** `(x+3, y+5, z+1)` — raised for visibility on taller structures, embedded in wall.
- **Watchtower doors:** 2-wide × 3-tall opening, cleared at end of build sequence.
- **Boss bar:** `WithBossBar(title?)` sets `ASPIRE_BOSSBAR_TITLE` env var. Default: "Aspire Fleet Health". Displays `"{title}: {pct}%"`. `ASPIRE_APP_NAME` no longer used.
- **Idempotent building:** `HashSet<string> _builtStructures` tracks built resources. Structures build once, then only health indicators update.
- **Peaceful mode:** `WithPeacefulMode()` → `/difficulty peaceful` once at startup. No service class needed.

**Key learnings:**
- Clear critical openings (doors, windows) LAST in multi-stage structure builds.
- Path depth matters — flush paths replace surface layer, not sit on top.
- Idempotent building prevents decorative element overwrites and visual glitching.
- Always verify coordinates against in-game geometry when `hollow` fills create walls.
 Team update (2026-02-11): All sprints must include README and user documentation updates to be considered complete  decided by Jeffrey T. Fritz
 Team update (2026-02-11): All plans must be tracked as GitHub issues and milestones; each sprint is a milestone  decided by Jeffrey T. Fritz

### Dynamic Terrain Detection (2026-02-11)

**Architecture:** Added `TerrainProbeService` singleton that runs ONCE at startup (after RCON connect, before resource discovery). Uses binary search with `setblock X Y Z yellow_wool keep` to find the highest solid block at (BaseX, BaseZ). Search range: Y=100 to Y=-64, ~8 RCON commands max. Cleans up any probe blocks placed. On failure, gracefully falls back to `BaseY = -60`.

**Key decisions:**
- `VillageLayout.SurfaceY` is a static mutable property (not const) defaulting to `BaseY`. All services use `SurfaceY` instead of `BaseY` for Y positioning.
- `VillageLayout.BaseY` kept as const fallback — backward compat preserved.
- `HologramManager.SpawnY` → `VillageLayout.SurfaceY + 5` (was hardcoded `-55`).
- `GuardianMobService.BaseY` → `VillageLayout.SurfaceY + 2` (was hardcoded `-58`).
- `RedstoneDependencyService` wireY → `VillageLayout.SurfaceY` (was `VillageLayout.BaseY`).
- `StructureBuilder.BuildPathsAsync` made terrain-agnostic: `fill ... air` replaces ALL surface blocks, not just `grass_block`.
- `StructureBuilder.BuildFencePerimeterAsync` uses `SurfaceY` instead of `BaseY`.
- `TerrainProbeService` called in `MinecraftWorldWorker.ExecuteAsync` BEFORE `DiscoverResources()` and all feature initialization.

**Key files:**
- `src/Aspire.Hosting.Minecraft.Worker/Services/TerrainProbeService.cs` — binary search terrain detection via RCON setblock
- `src/Aspire.Hosting.Minecraft.Worker/Services/VillageLayout.cs` — `SurfaceY` property added
- `tests/Aspire.Hosting.Minecraft.Worker.Tests/Services/TerrainProbeServiceTests.cs` — probe fallback and integration tests

**RCON learning:** `setblock X Y Z block keep` returns "Changed the block at X, Y, Z" when air (placed), or error when solid. This is the cleanest non-destructive block probe mechanism — no world modification if you clean up immediately after successful placement.

### Visual Bug Fixes: Structure Elevation & Health Lamp Alignment (2026-02-11)

**Bug 1 — Buildings 1 block below ground:** `TerrainProbeService` detects `SurfaceY` as the Y of the highest solid block (the grass block). Structures placed their floor AT `SurfaceY`, replacing the grass and burying the bottom wall row underground. Fix: `VillageLayout.GetStructureOrigin()` now returns `SurfaceY + 1`. Also adjusted `StructureBuilder.BuildFencePerimeterAsync` (`fenceY = SurfaceY + 1`) and `BuildPathsAsync` (air clearing at `SurfaceY + 1`, cobblestone at `SurfaceY`).

**Bug 2 — Warehouse health lamp misaligned:** The health indicator glowstone was placed at `y+3`, which overlapped with the 3-tall cargo door (y+1 to y+3). Fixed `PlaceHealthIndicatorAsync` to use `y+4` for Watchtower and Warehouse (3-tall doors), keeping `y+3` for Workshop and Cottage (2-tall doors).

**Key learning:** When `SurfaceY` represents the topmost solid block, structure floors must be placed at `SurfaceY + 1` (above the surface), not at `SurfaceY` (replacing the surface). Health indicators must be placed above door openings, not overlapping them.