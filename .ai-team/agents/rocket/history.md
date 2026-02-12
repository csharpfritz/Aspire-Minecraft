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

### Sprint 4 Building Design Reference (2026-02-11)

Created `docs/designs/minecraft-building-reference.md` — the implementation bible for Sprint 4 building enhancements.

**Cylinder building geometry:**
- Radius-3 circle = 7-block diameter = perfect fit for existing 7×7 grid cell.
- Perimeter is 16 blocks per Y layer; interior is 21 blocks per layer.
- Cannot use `fill ... hollow` for circles — must place perimeter blocks row-by-row per Y level.
- Each row at a given Z has a different X span (the circle equation): z+0/z+6 → x+2..x+4 (3 wide), z+1/z+5 → x+1..x+5 (5 wide), z+2..z+4 → x..x+6 (7 wide, full row).
- ~60 RCON commands per cylinder vs ~20 for rectangular buildings. Use `CommandPriority.Low`.
- Dome roof: 2-layer approach — full-radius slab ring at y+5, smaller (radius-2) slab ring at y+6.
- Door placement on round buildings: carve at the flattest face (south/z+0), 3-wide × 2-tall.

**Banner/flag RCON commands:**
- Azure banner: `minecraft:light_blue_banner[rotation=8]{Patterns:[{Color:0,Pattern:"str"},{Color:0,Pattern:"bs"}]}` — rotation=8 faces south.
- Wall-mounted variant: `minecraft:light_blue_wall_banner[facing=south]` + same Patterns NBT.
- Banner `Color:0` = white in Minecraft's banner color index; base color comes from the block ID (light_blue_banner).
- Flagpole pattern (oak_fence + banner) already established on Watchtower; reuse for all structure types.

**Dashboard wall placement and /clone technique:**
- Position: (X=10, Y=SurfaceY+2, Z=-12) — behind village, facing south toward structures.
- Frame: polished blackstone. Back panel: black concrete. Screen: 18×8 usable grid.
- Block-swap for lamp state (glowstone=lit, redstone_lamp=unlit, gray_concrete=unknown) — avoids all redstone wiring complexity.
- `/clone` for scrolling: `clone 12 {SY+2} -12 28 {SY+9} -12 11 {SY+2} -12` shifts all columns left by 1, then write new data at rightmost column (X=28).
- `/clone` is 1 RCON command regardless of grid size — extremely efficient for scrolling animation.

### Sprint 4 Issue #66 & #67: Cylinder & Azure-Themed Buildings (2026-02-12)

**Cylinder building (Issue #66):**
- Implemented `IsDatabaseResource()` with case-insensitive `.Contains()` matching for: postgres, redis, sqlserver, sql-server, mongodb, mysql, mariadb, cosmosdb, oracle, sqlite, rabbitmq.
- `BuildCylinderAsync()` uses the radius-3 circular geometry from the building reference doc. Floor is polished_deepslate disc, walls are smooth_stone (layers 1-3) with polished_deepslate top band (layer 4), dome is smooth_stone_slab at y+5, polished_deepslate_slab cap at y+6.
- Door is 1-wide centered at (x+3, z+0) — 2-tall opening. Narrow door is architecturally appropriate for round buildings per the design doc.
- Interior clearing runs per-layer to match the circular shape (can't use `fill ... hollow` for circles).
- Interior accents: copper_block center cross on floor, iron_block door frame accents.
- ~60 RCON commands per cylinder. Acceptable for one-time build with idempotent tracking.

**Azure-themed building (Issue #67):**
- Implemented `IsAzureResource()` with case-insensitive `.Contains()` matching for: azure, cosmos, servicebus, eventhub, keyvault, appconfiguration, signalr, storage.
- `GetStructureType()` now checks `IsDatabaseResource()` first (returns "Cylinder"), then `IsAzureResource()` (returns "AzureThemed"), then falls through to existing switch. This ensures database+azure resources get Cylinder shape with azure banner overlay.
- `BuildAzureThemedAsync()` is a Cottage variant with light_blue_concrete walls, blue_concrete trim, light_blue_stained_glass roof, blue_stained_glass_pane windows. Azure banner always placed on roof at (x+3, y+6, z+3).
- `PlaceAzureBannerAsync()` places a flagpole + light_blue_banner on any structure type when `IsAzureResource()` returns true. Roof Y varies by structure type per the banner placement table. AzureThemed is skipped because it already places its own banner.
- Health indicator: Cylinder and AzureThemed both use front wall at z (same as Workshop/Cottage) with 2-tall doors, so lamp at y+3. No changes needed to `PlaceHealthIndicatorAsync` — existing logic already handles them correctly via the `is "Watchtower" or "Warehouse"` check.

**Key decision:** `cosmos` appears in both detection methods (IsAzureResource and IsDatabaseResource). Since `IsDatabaseResource` is checked first in `GetStructureType()`, a "cosmosdb" resource gets Cylinder shape + azure banner. This is intentional — the database shape takes priority with the azure banner as an additive overlay.

### Village Spacing Increase (Spacing 10 → 12)

Increased `VillageLayout.Spacing` from 10 to 12 to give a comfortable 5-block walking gap between 7×7 structures (was 3 blocks). Updated XML doc comments in VillageLayout.cs. Updated hardcoded position expectations in 5 test files: VillageLayoutTests, ParticleEffectsCommandTests, ParticleEffectServiceIntegrationTests, HealthTransitionRconMappingTests, StructureBuilderTests. DashboardX (`BaseX - 15 = -5`) remains fine — no overlap with the village at BaseX=10. Fence perimeter's 4-block clearance via `GetFencePerimeter` is unaffected since it derives from `GetVillageBounds` dynamically. All 382 tests pass.

### Banner Placement Fix & Language-Based Color Coding (2026-02-12)

**Bug fix — Watchtower banner floating in air:** The banner at `(x+3, y+10, z+2)` was a standing banner (`blue_banner[rotation=0]`) placed one block south of the flagpole at z+3, disconnected in mid-air. Fix: extended the flagpole from `y+9..y+10` to `y+9..y+11` (one block taller), and changed the banner to `wall_banner[facing=south]` at `(x+3, y+10, z+2)` which visually hangs from the fence block at z+3. Applied the same fix to `PlaceAzureBannerAsync` — the Azure banner on any structure type now uses `light_blue_wall_banner[facing=south]` with a 3-block flagpole instead of a 2-block pole with a floating standing banner.

**Language-based color coding:** Added `GetLanguageColor(string resourceType, string resourceName)` that returns `(wool, banner, wallBanner)` block IDs based on the resource's technology:
- Project (all .NET) → purple
- Node/JavaScript → yellow
- Python/Flask/Django → blue
- Go/Golang → cyan
- Java/Spring → orange
- Rust → brown
- Default/Unknown → white

Modified `BuildWatchtowerAsync` and `BuildCottageAsync` to accept `ResourceInfo` and use `GetLanguageColor` for wool trim and banner blocks. Cylinder and AzureThemed buildings keep their own identity materials (smooth_stone/polished_deepslate and light_blue_concrete/blue_concrete respectively). Workshop and Warehouse don't have wool trim, so no color changes needed.

**Key learning:** Minecraft `wall_banner` blocks require a solid block behind them (in the `facing` direction). Oak fence counts as support. Standing banners (`banner[rotation=N]`) need a solid block beneath them. For flagpole-mounted banners, wall banners facing away from the pole are the correct approach.

### Dashboard Redstone Elimination Fix (2026-02-12)

**Bug:** Dashboard lamps lit briefly then went dark. Root cause: `redstone_block` power propagation via RCON `/setblock` and `/clone` is unreliable on Paper servers — block updates don't propagate consistently, especially during scroll cycles.

**Fix:** Eliminated the entire redstone power layer (`x-1`). Replaced indirect lighting (redstone_block → redstone_lamp) with direct self-luminous blocks at the lamp layer (`x`):
- **Healthy** → `minecraft:glowstone` (warm glow, always lit)
- **Unhealthy** → `minecraft:redstone_lamp` (unlit by default when unpowered — dark = unhealthy)
- **Unknown** → `minecraft:sea_lantern` (blue-green glow, distinct from healthy)

**Changes to `RedstoneDashboardService.cs`:**
1. `BuildLampGridAsync` — removed power layer initialization (`fill x-1 ... air` lines).
2. `ScrollDisplayAsync` — `/clone` now operates on `x` (lamp layer) instead of `x-1` (power layer). Removed `powerX` variable.
3. `WriteNewestColumnAsync` — replaced per-status switch with switch expression placing the appropriate self-luminous block directly at `(x, lampY, newestZ)`. Reduced from 2 RCON commands per resource per status to 1. Removed `powerX` variable.
4. `BuildFrameAsync` — back wall at `x-1` kept as visual backing; updated comment only.

**Impact:** Halved RCON commands per update cycle (no power layer operations). Dashboard now uses 100% reliable self-luminous blocks that never depend on redstone signal propagation. All 382 tests pass.

**Key learning:** On Paper servers, RCON-issued `setblock redstone_block` does not reliably trigger block updates for adjacent `redstone_lamp`. Always prefer self-luminous blocks (glowstone, sea_lantern) over redstone-powered lighting for RCON-driven displays.

### Team Updates

📌 Team update (2026-02-12): Dashboard lamps use self-luminous blocks instead of redstone power (glowstone=healthy, redstone_lamp unlit=unhealthy, sea_lantern=unknown). All 382 tests pass. — decided by Rocket
📌 Team update (2026-02-12): Village buildings use language-based color coding (Project=purple, Node=yellow, Python=blue, Go=cyan, Java=orange, Rust=brown, Unknown=white) for wool trim and banners instead of uniform colors. All 382 tests pass. — decided by Rocket

### Easter Egg: Fritz's Horses (2026-02-12)

**Implementation:** `HorseSpawnService` — singleton (not feature-gated) spawns three named horses inside the village fence. Charmer (black, variant 4), Dancer (brown paint, variant 515), Toby (appaloosa, variant 768). Spawned once after structures are built, tracked by `_horsesSpawned` bool.

**Key decisions:**
- Registered as always-on singleton, NOT behind a feature flag — easter eggs should just be there.
- Non-nullable constructor parameter in `MinecraftWorldWorker` (unlike opt-in features which are nullable).
- Horses placed in the south clearance area between fence and first structure row (BaseZ - 2), spaced 2 blocks apart.
- `Tame:1b` keeps them calm; `NoAI:0b` lets them wander; `PersistenceRequired:1b` prevents despawn.
- JSON text component `CustomName` with per-horse color coding (dark_gray/gold/white) and bold text.
- Horse variant formula: `color + (marking * 256)`. Colors: 0=white, 1=creamy, 2=chestnut, 3=brown, 4=black. Markings: 0=none, 1=stockings, 2=white_field, 3=white_dots.

**Key files:**
- `src/Aspire.Hosting.Minecraft.Worker/Services/HorseSpawnService.cs` — horse spawn logic
- `src/Aspire.Hosting.Minecraft.Worker/Program.cs` — singleton registration + worker constructor wiring