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
