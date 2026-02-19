# Project Context

- **Owner:** Jeffrey T. Fritz (csharpfritz@users.noreply.github.com)
- **Project:** Aspire.Hosting.Minecraft — .NET Aspire integration for Minecraft servers
- **Stack:** C#, .NET 10, Docker, Aspire, OpenTelemetry, Minecraft Paper Server, RCON
- **Created:** 2026-02-10

## Key Facts

- Worker service (Aspire.Hosting.Minecraft.Worker) handles in-world display
- Uses RCON to communicate with Minecraft server for commands
- DecentHolograms plugin for in-world holograms
- Worker created by WithAspireWorldDisplay<TWorkerProject>()
- WithMonitoredResource() applies env vars to worker
- Metrics: TPS, MSPT, players online, worlds loaded, RCON latency
- `VillageLayout` centralizes position calculations; now supports 7×7 (standard) and 15×15 (grand) structures
- Current RCON rate limits: 10 cmd/s standard, 40 cmd/s burst mode

## Recent Summary (Milestones 1-5)

**Worker architecture and features:** `MinecraftWorldWorker` polls every 10s with 2-min broadcast cycle. `RconService` enforces rate limiting with token bucket (10 cmd/s standard, 40 cmd/s burst), 250ms dedup throttle, and OTEL tracing. 13 feature toggles across Sprints 1-3 (particles, weather, boss bar, sounds, action bar ticker, beacons, fireworks, guardians, fanfare, heartbeat, achievements, redstone dependency graph, service switches) all follow consistent `With{Feature}()` pattern with conditional DI. Resource discovery via `ASPIRE_RESOURCE_{NAME}_*` env vars. All services (HologramManager, ScoreboardManager, StructureBuilder, PlayerMessageService, etc.) register as singletons and integrate into worker main loop.

**Building evolution (v0.1.0-v0.5.0):** Started with 4 themed 7×7 structures (Watchtower/Warehouse/Workshop/Cottage). Sprint 3 added village fence (oak perimeter with paths and gates) and service switches (visual-only levers reflecting resource state). Sprint 4 added database cylinders (smooth_stone + polished_deepslate, ~88 RCON commands) and Azure-themed cottages (light_blue_concrete with banners), language-based color coding (purple=.NET, yellow=Node, blue=Python, cyan=Go, orange=Java, brown=Rust), and enhanced dashboard with self-luminous lamps (glowstone=healthy, redstone_lamp=unhealthy, sea_lantern=unknown). Milestone 5 redesigned all 4 structures as grand 15×15 variants with ornate medieval aesthetics: Grand Watchtower (20 blocks, 3-floor spiral staircase, deepslate brick buttresses, iron bar arrow slits, crenellated battlements), Grand Workshop (A-frame roof with chimney, tool station interior), Grand Azure Pavilion (light blue concrete with blue pilasters, skylight), Grand Cottage (cobblestone+oak upper, pitched roof, homey interior). Ornate Watchtower exterior features mossy foundation, cracked stone brick weathering, 3×3 corner turrets with pinnacles, machicolations, keystone gatehouse arch with portcullis, 2-high observation windows, proper merlons with ~99 total RCON commands.

**RCON and terrain discoveries:** Identical commands get deduped by 250ms throttle — use unique strings or micro-vary parameters to force execution. `/fill ... hollow` is most efficient wall building. Redstone signal propagation unreliable on Paper servers — use self-luminous blocks (glowstone, sea_lantern, redstone_lamp unpowered) instead of redstone power. Binary search terrain probe via `setblock` discovers surface Y-level. `wall_banner[facing=south]` requires solid block support; standing banners need solid block beneath. `/clone` is single RCON command regardless of grid size — perfect for scrolling displays. Burst mode via `EnterBurstMode()` returns `IDisposable` with SemaphoreSlim thread safety. Spacings 10→12→24 accommodate 7×7 structures (3→5→17 block gaps). Village spacing 24 = 15×15 building + 9 blocks gap for rails/paths. Fence clearance 4→10 blocks for horse roaming space. Forceload expanded to `-20,-20,120,120` for grand village coverage.

**Grand Village foundation:** Spacing doubled to 24 blocks with 10-block fence clearance. Structure size bumped to 15×15 (13×13 usable interior) supporting ~20 resources before world border issues at 512 MAX_WORLD_SIZE. Grand variants branch on `StructureSize >= 15`. Shared placement methods (health lamp, azure banner, sign) now use `StructureSize / 2` for adaptive positioning. Minecart rails coexist with redstone wires (1-block X offset) — both visual systems coexist. Easter egg: 3 named horses (Charmer/Dancer/Toby) with variants and tameness. All 7 building types (Watchtower, Warehouse, Workshop, Cottage, Cylinder, AzureThemed, + Grand variants) follow consistent placement/health indicator patterns.

📌 Team update (2026-02-18): NBT library evaluation for #95 complete — recommended fNbt 1.0.0 (most actively maintained v1.0.0 Jul 2025, broadest .NET compatibility via netstandard2.0, largest community adoption 200+ stars). Documented requirement for custom 80-line AnvilRegionReader to parse MCA file header, sector offsets, and per-chunk decompression. Designed block lookup API with 1.18+ bit-packed palette decoding. Unblocks #93 (AnvilRegionReader implementation) and #94 (WorldSaveDirectory fixture) — decided by Rocket

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

## Learnings

### Village Grid Ordering Convention
- ALL services that place elements on the village grid MUST use `VillageLayout.ReorderByDependency(monitor.Resources)` for index-to-position mapping, not raw `monitor.Resources` dictionary order.
- Dictionary iteration order is non-deterministic with respect to dependency relationships. Services that used different orderings would place features (beacons, guardians, particles, levers, wires, rails) at wrong buildings.
- Services affected: `StructureBuilder`, `BeaconTowerService`, `GuardianMobService`, `ParticleEffectService`, `ServiceSwitchService`, `RedstoneDependencyService`, `MinecartRailService`.
- Services NOT affected (don't use grid indices): `HologramManager`, `ScoreboardManager`, `BossBarService`, `WeatherService`, `FireworksService`, `DeploymentFanfareService`, `ActionBarTickerService`, `WorldBorderService`, `HeartbeatService`.

### Minecraft Wall-Mounted Block Placement
- `facing=X` means the item extends in X direction; support block is in the OPPOSITE direction.
- For a lever on a building's front wall (Z-min side): place lever at `FaceZ - 1` (one block in front of wall), use `facing=north` so support is to the south at `FaceZ` (the wall).
- Never place a wall-mounted block AT the wall's Z coordinate — that replaces the wall block and the support direction points to interior air.
- Lamp companions for levers go IN the wall at `(leverX, leverY + 1, leverZ + 1)` = `(leverX, leverY + 1, FaceZ)`.

### Worker Loop Feature Gating
- Transition-only features (`particles`, `titleAlerts`, `sounds`, `fireworks`, `deploymentFanfare`, `achievements`) run inside `changes.Count > 0` guard — correct, they only fire on health changes.
- Continuous features (`weather`, `bossBar`, `beaconTowers`, etc.) run every cycle — they have internal transition tracking and only send RCON commands when state actually changes.
- `redstoneGraph` and `minecartRails` were moved to the continuous section because they have their own `_lastKnownStatus` tracking and need to run every cycle to reconcile state.

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

### Village Spacing & Horse Fixes (2026-02-12)

**Spacing doubled (12 → 24):** `VillageLayout.Spacing` increased from 12 to 24, giving a 17-block walking gap between 7×7 structures (was 5 blocks). All structure placement derives from `GetStructureOrigin()` which uses `Spacing`, so all services automatically get the new positions.

**Fence clearance increased (4 → 10):** `GetFencePerimeter()` now uses 10-block clearance from village bounds instead of 4. Gives horses plenty of room to roam between buildings and the fence.

**Forceload area expanded:** Updated from `forceload add -10 -10 80 80` to `forceload add -20 -20 120 120` to cover the larger village footprint with doubled spacing and wider fence clearance.

**Horse CustomName fix:** Changed from JSON text component format (`'{"text":"Charmer","color":"dark_gray","bold":true}'`) to simple quoted string format (`"\"Charmer\""`) matching the pattern used by `GuardianMobService`. The JSON text component was rendering as raw JSON in the hover tooltip on Paper servers.

**Horse spawn position:** Changed Z coordinate from `BaseZ - 2` to `BaseZ - 6` to place horses in the middle of the now-larger clearance area between the south fence and the first row of structures.

**Test updates:** Updated hardcoded coordinate expectations in 4 test files: VillageLayoutTests, HealthTransitionRconMappingTests, StructureBuilderTests, ParticleEffectServiceIntegrationTests. All 382 tests pass.

**Key learning:** For mob CustomName via RCON on Paper servers, use the simple double-quoted string format (`CustomName:"\"Name\""`) rather than JSON text component objects. The `GuardianMobService` already uses this pattern correctly.
 Team update (2026-02-12): Village spacing doubled to 24 blocks (15 + 9 gap between buildings) with enhanced fence clearance (10 blocks)  decided by Rocket
 Team update (2026-02-12): Sprint 4 building designs specified: database cylinders (77 cell, smooth stone + deepslate, ~88 RCON commands), Azure banners (light_blue with patterns on all Azure resources), enhanced palettes (Watchtower, Warehouse, Workshop, Cottage with detailed interior)  decided by Rocket

### RCON Burst Mode (Milestone 5, Issue #85)

Added `EnterBurstMode(int commandsPerSecond = 40)` to `RconService`. Returns `IDisposable` — callers wrap construction in a `using` block and the rate limit auto-restores on dispose.

### Ornate Grand Watchtower Exterior (Milestone 5, Issue #78)

Redesigned `BuildGrandWatchtowerAsync` exterior from plain cube to ornate medieval tower. Same 15×15 footprint, 20 blocks tall, interior unchanged. Key exterior features:

- **Tapered base:** mossy_stone_bricks foundation (y) + stone_brick_stairs sloped plinth (y+1) facing outward on all 4 sides — anchors the tower visually.
- **Mixed wall materials:** stone_bricks hollow shell (y+2 to y+18), cracked_stone_bricks weathering on lower front/back walls (y+2 to y+4).
- **Prominent 3×3 corner buttresses:** polished_andesite pillars extending full height at all 4 corners — much more imposing than the old 2×2.
- **Wool bands preserved** at y+6 and y+12, skipping corner buttress areas (x+3 to x+s-3 / z+3 to z+s-3).
- **Corbel string courses:** stone_brick_stairs (half=top) above wool bands at y+7 on front/back — adds horizontal depth lines.
- **Wider window bays:** 2-wide glass_pane pairs on ground floor (y+3), full-width on second floor (y+9), panoramic observation on third floor (y+15) all 4 sides.
- **Machicolations:** upside-down stone_brick_stairs at y+19 on all 4 sides, creating the characteristic medieval overhang below the parapet.
- **Pronounced battlements:** stone_bricks hollow ring at y+20, 2-high merlons (y+20-21) at regular intervals on front/back.
- **Corner turret caps:** stone_brick_stairs conical roofs over buttresses at y+19, stone_brick_wall pinnacles at y+20.
- **Pointed gatehouse arch:** 5-wide stone_bricks frame, stone_brick_stairs converging on keystone (chiseled_stone_bricks), 3×4 air opening, flanking lanterns.
- **4 banners** on turret pinnacles at (x+1,y+21,z+1), (x+s-1,y+21,z+1), (x+1,y+21,z+s-1), (x+s-1,y+21,z+s-1).
- **RCON budget:** 84 commands in method, ~98 total with fence/paths/health/sign — under 100 limit.

**Implementation details:**
- `_maxCommandsPerSecond` changed from `readonly` to mutable. `_defaultCommandsPerSecond` stores the original value.
- Thread safety via `_burstModeSemaphore` (SemaphoreSlim(1,1)): `Wait(0)` for non-blocking acquire; throws `InvalidOperationException` if already active.
- `BurstModeScope` inner class: `IDisposable` with `Interlocked.Exchange` guard preventing double-dispose.
- Logs at INFO on enter and exit with before/after rate values.
- Token bucket (`RefillTokens()`) automatically adapts because it reads `_maxCommandsPerSecond` dynamically — no bucket reset needed.
- `_burstModeSemaphore` disposed in `DisposeAsync`.

**Key learning:** The token bucket's `RefillTokens()` already uses `_maxCommandsPerSecond` for both refill rate and cap. Making the field mutable is sufficient — no need to reset the bucket on mode change. The burst rate takes effect on the next token refill cycle naturally.

### Grand Workshop (Milestone 5, Issue #82)

Redesigned `BuildWorkshopAsync()` with Grand mode (15×15) branching. When `VillageLayout.StructureSize >= 15`, delegates to `BuildGrandWorkshopAsync()`. Standard 7×7 version preserved unchanged.

**Grand Workshop exterior (15×15, 10 blocks tall):**
- Oak plank walls with spruce log corner posts (y+1 to y+5) and horizontal beam frame at y+5.
- A-frame peaked roof: 4 layers of spruce stair shingles (y+6 eaves → y+9 ridge cap with spruce_slab).
- 2×2 cobblestone chimney at back-right corner (y+6 to y+10) topped with campfire.
- Cyan stained glass windows (2×2) flanking door on front wall, plus side and back walls.
- Flower pots under front windows at y+2.
- 3-wide × 3-tall door centered at x+6..x+8 on front wall (z).

**Grand Workshop interior:**
- Tool stations along back wall: crafting_table, smithing_table, stonecutter, anvil, grindstone (spaced evenly).
- Furnace at left back corner, brewing_stand at right back corner.
- Loft at y+6: half-floor (back half, z+7 to z+13) with oak fence railing, ladder access against side wall (x+1, y+1..y+6).
- Loft furnishing: 3 barrels + bookshelf against back wall.
- 3 hanging lanterns at ceiling (y+5).

**Support method updates:**
- `PlaceAzureBannerAsync`: Grand Workshop roofY = y+10, flagpole centered at x+half/z+half.
- `PlaceHealthIndicatorAsync`: Grand Workshop lampY = y+4 (above 3-tall door), lampX = x+7 (centered).

**RCON budget:** 47 commands in `BuildGrandWorkshopAsync` + 3-5 external (health lamp, sign, optional azure banner) = ~50-52 total. Within the ~55-65 budget.

**Key learning:** A-frame roof on a 15-block-wide building needs 4 layers to reach the ridge. Each layer uses matching spruce stair `facing` directions (south for front slope, north for back slope) with oak plank fill in the gable center. The ridge cap uses `spruce_slab` for a clean peak line.

### Grand Azure Pavilion & Grand Cottage (Milestone 5, Issue #80)

Added grand variants for the two remaining building types: Azure Pavilion and Cottage. Both branch on `VillageLayout.StructureSize == 15` — if grand, delegate to `BuildGrandAzurePavilionAsync` / `BuildGrandCottageAsync`; otherwise keep the standard 7×7 build.

**Grand Azure Pavilion (BuildGrandAzurePavilionAsync):**
- 15×15 footprint, 8 blocks tall. Light blue concrete walls with blue concrete pilaster strips at all 4 corners and 4 wall midpoints.
- Blue concrete trim band at wall top (y+7). Flat light blue concrete roof (y+8) with 3×3 light blue stained glass skylight in center.
- Azure banners on all 4 roof corners (y+9). Blue stained glass pane windows on all 4 walls.
- Interior: light blue carpet floor, brewing stand + cauldron (cloud services aesthetic), 4 hanging lanterns.
- ~50 RCON commands. Door is 2-wide centered at `x + half - 1` to `x + half`.

**Grand Cottage (BuildGrandCottageAsync):**
- 15×15 footprint, 8 blocks tall. Cobblestone lower walls (y+1 to y+4), oak plank upper walls (y+5 to y+7).
- Language-colored wool trim band at y+7. Cobblestone slab pitched roof (y+8).
- Flower pots on front face below windows. Glass pane windows on front and sides.
- Interior: red bed, crafting table, bookshelf, furnace, 2 chests, potted poppy + dandelion, 4 wall torches.
- ~45 RCON commands. Door is 2-wide centered at `x + half - 1` to `x + half`.

**Supporting changes:**
- `PlaceHealthIndicatorAsync` — lampX now uses `x + half` for all grand variants (was only handling Watchtower/Warehouse/Cylinder).
- `PlaceAzureBannerAsync` — roofY for grand Cottage updated to `y + 9` (was `y + 6` for standard).
- `PlaceSignAsync` — signX now uses `x + half - 1` derived from `VillageLayout.StructureSize / 2`, keeping backward compat (7/2 = 3, so x+2).
- `BuildFencePerimeterAsync` — gate width now uses `VillageLayout.GateWidth` (3 standard, 5 grand).
- `BuildPathsAsync` — grand layout gets a central stone brick boulevard between the two columns.
- `BuildCylinderAsync` — added size check to branch to `BuildGrandCylinderAsync` when grand.

**Key learning:** When modifying shared placement methods (health lamp, sign, banner) for new grand variants, use `VillageLayout.StructureSize / 2` instead of hardcoding `3` vs `7`. This makes the code adaptive to any structure size without needing per-type grand checks.

📌 Team update (2026-02-15): Structural validation requirements — all acceptance tests must verify door accessibility, staircase connectivity, and wall-mounted items. Created 86 structural geometry tests. — decided by Jeff (Jeffrey T. Fritz)

📌 Team update (2026-02-15): Fill-overlap detection is now a standard test pattern. New building types must include fill-overlap detection tests using the FillOverlapDetectionTests infrastructure. — decided by Nebula

📌 Team update (2026-02-15): MCA Inspector milestone launched — read-only Minecraft Anvil format (NBT) library for bulk structural verification. Phase 1 (library) and Phase 2 (test infrastructure) are parallel-safe. Timeline: ~1.5 weeks. — decided by Rhodey

📌 Team update (2026-02-16): Feature monitoring services moved to continuous loop and lever placement fixed (facing direction and wall attachment) — decided by Coordinator

### Grand Watchtower (Milestone 5, Issue #78) — IMPLEMENTED

**Implementation:** `BuildGrandWatchtowerAsync` — 15×15 footprint, 20 blocks tall, 3 interior floors connected by spiral staircase. Activated when `VillageLayout.StructureSize >= 15` (same check as all other grand builders); standard 7×7 watchtower remains the fallback.

**Architecture (as built):**
- Single `fill ... hollow` for full 19-block-tall stone brick shell (y+1 to y+19) — simpler than per-floor sections.
- 4 polished andesite corner buttresses (2×2) rising full height, placed after walls so they overlay corners.
- Language-colored wool bands at y+6 and y+12 (floor boundaries) on all 4 wall faces.
- Arrow slit windows: individual glass panes at y+3 and y+9 (ground/second floor), 3-wide glass pane fills at y+15 on all sides for observation deck.
- Crenellated battlements at y+20: fill full ring of stone bricks, then place stone_brick_stairs[half=top] at alternating positions.
- 4 standing banners at roof corner posts (y+21) on buttress inner corners.
- Spiral staircase: Flight 1 along north wall (z+1, east-facing oak stairs, y+1 to y+6); Flight 2 along east wall (x+s-1, south-facing, y+8 to y+13). Stairwell holes cleared in floor platforms.
- Floor platforms: oak planks at y+7 (second floor) and y+13 (third floor) with air holes for stairwell access.
- Ground floor: crafting table, resource name sign on back wall, 4 wall torches.
- Second floor: enchanting table centered at (half, y+8, half), bookshelves along south and west walls.
- Third floor: lectern centered at (half, y+14, half) as observation deck.
- Iron door entrance: stone brick archway frame with stone_brick_stairs[half=top] lintel at y+5, 3-wide × 4-tall air opening.
- Returns `DoorPosition(x + half, y + 4, z)` — door on front wall at z, matching 4-tall opening.

**RCON command count:** ~85 commands. Single hollow fill for the full tower shell is key to staying under budget.

**Key learnings:**
- Single tall `fill ... hollow` + floor platform fills is more command-efficient than per-floor wall sections.
- Crenellations: fill a complete row of stone brick, then overlay stairs at alternating positions (no air carving needed).
- Door archway with stone_brick_stairs[half=top] lintel gives a nice visual effect.
- Grand variant branching uses `>= 15` (not `== 15`) to match all other grand builder checks.

📌 Team update (2026-02-12): RCON Burst Mode API (#85) — EnterBurstMode(int=40) returns IDisposable, thread-safe single burst per SemaphoreSlim, logs on enter/exit, rate limit auto-restores — decided by Rocket

### Grand Watchtower Ornate Redesign (2026-02-15)

- Redesigned Grand Watchtower exterior from plain rectangle to ornate medieval castle tower.
- Key architectural changes: deepslate brick corner buttresses (replacing polished_andesite), cracked_stone_bricks weathering on lower walls, iron_bars arrow slits on ground floor, 2-high observation windows, taller corner turrets with pinnacle wall posts extending above parapet (y+22), portcullis iron bars in gatehouse arch, deeper gatehouse arch (y+6 keystone vs y+5), and proper alternating merlons on the battlements.
- RCON command budget: 85 commands in BuildGrandWatchtowerAsync (58 exterior + 27 interior), ~14 village overhead = ~99 total, under the <100 test limit.
- Mixed block palette creates depth: mossy_stone_bricks (base), cracked_stone_bricks (weathering), deepslate_bricks (buttresses), chiseled_stone_bricks (gatehouse keystone), stone_brick_stairs (machicolations/corbels/turret caps), iron_bars (arrow slits + portcullis), glass_pane (observation windows).
- Design constraint: visual richness must come from block variety and placement order (layering fills), not from additional commands. Every decorative element must justify its RCON cost.
- Interior left completely unchanged — floors, staircase, furniture, sign, torches all preserved.
- DoorPosition return value unchanged at (x + half, y + 4, z) to maintain health indicator and sign compatibility.

📌 Team update (2026-02-15): Python and Node.js sample APIs added to MinecraftAspireDemo; separate GrandVillageDemo created on milestone-5 showcasing all grand building variants (15×15 structures for Project, Container, Database, Azure types) — decided by Shuri

### Grand Watchtower Entrance Cleanup (2026-02-16)
- Eliminated the visible "lower level" by removing the stair skirt at y+1 and starting walls at y+1 instead of y+2.
- The tapered base concept (4 stair fills at y+1) created an awkward 2-block-high shelf below the entrance — players saw grass, mossy stone, stairs, THEN the actual door. Removing it gives a clean transition: mossy plinth at y, walls at y+1.
- Simplified the gatehouse entrance from a tall cluttered opening (5-wide frame up to y+7 with portcullis iron bars and lanterns) to a clean 3-wide × 4-tall opening (y+1 to y+4) with a proportional arch at y+5.
- Removed: iron_bars portcullis at y+6 (visual noise), hanging lanterns at y+5 (clutter), oversized gatehouse frame reaching y+7 (exposed oak planks from second floor visible inside entrance).
- Kept: chiseled stone keystone, decorative stone_brick_stairs arch shoulders, all upper features (wool bands, battlements, turrets, banners, observation windows).
- DoorPosition updated from (x+half, y+5, z) to (x+half, y+4, z). GlowBlock now at y+5 (was y+6).
- Net savings: 6 fewer RCON commands (removed 4 stair skirt + portcullis + 2 lanterns, gatehouse frame shrunk).
- Health indicator test updated to match new GlowBlock position (17, -54, 0).

## Learnings
- Stair skirts around a building base look like unintentional sub-floors when there's a door at that level. Only use them on non-entrance faces.
- Gatehouse entrances get cluttered fast — each decorative element (portcullis, lanterns, extra frame height) competes for attention in a narrow space. Simpler is better.
- DoorPosition.TopY should match the actual top of the walkable opening, not decorative elements above it.
📌 Team update (2026-02-15): Grand Watchtower entrance redesigned — removed stair skirt, simplified gatehouse to 3×4 opening, walls start at y+1, DoorPosition.TopY changed from y+5 to y+4. All 7 tests pass. — decided by Rocket

📌 Team update (2026-02-15): Improved acceptance testing required before marking work complete — validate against known constraints (geometry, visibility, placement). Nebula added 26 geometric validation tests covering doorway visibility, ground-level continuity, and health indicator placement. — decided by Jeff

### Structural Geometry Validation Tests (2026-02-15)

Created comprehensive structural geometry tests in `StructuralGeometryTests.cs` (86 passing, 5 skipped) that validate physical integrity of all 12 building variants by parsing RCON setblock/fill commands.

**Test categories:**
- Door accessibility (12 tests): door opening dimensions, air blocks, ground floor connectivity
- Staircase connectivity (5 tests): grand watchtower spiral stairs, stairwell holes, facing direction
- Wall-mounted items (59 tests): torch support, sign support, lever support, ladder support for all structure types
- Bug documentation (5 skipped tests): each documents a specific StructureBuilder bug

**5 bugs discovered in StructureBuilder.cs:**
1. Grand Watchtower torch (line ~601): `wall_torch[facing=north]` at z+s — support at z+s+1 is outside structure
2. Grand Watchtower spiral staircase (line ~547): first stair at (x+2, y+1, z+1) overlaps corner buttress footprint
3. Grand Watchtower/Warehouse wall signs (lines ~586-592, ~792-799): same outside-support pattern as torch bug
4. Grand Cylinder wall signs (lines ~1494-1496): interior air clear removes support block at z+2
5. Grand Cylinder ladders (lines ~1474-1475): `facing=west` but copper pillar support is to the west, not east

**Key technical patterns established:**
- `ParseSetblockCommands()` / `ParseFillCommands()` with `[GeneratedRegex]` for efficient RCON parsing
- `GetBlockAt()` with last-write-wins semantics and hollow fill support
- `GetWallMountDirection()` for Minecraft facing→support direction mapping (east→west, north→south, etc.)
- `BuildResult` record captures VillageLayout state immediately to avoid static state race conditions
- Wall torches/signs/ladders use opposite-direction convention: `facing=X` means support block is in opposite direction

**VillageLayout parallelism issue:**
- VillageLayout is a static class shared across all test classes running in parallel
- Tests that call ConfigureGrandLayout()/ResetLayout() can interfere with parallel tests
- Fix: capture layout state (origin coords, StructureSize) immediately after setting it, before any parallel test can mutate it
- Pre-existing race condition in StructureBuilderTests.UpdateStructuresAsync_TenResources_NoExceptions — not caused by our tests
📌 Team update (2026-02-15): Created 91 structural geometry validation tests (86 pass, 5 skipped with bug documentation). Discovered 5 StructureBuilder bugs in grand structure wall-mounted items and staircase placement. Fixed VillageLayout static state race condition in test infrastructure. — decided by Rocket

### Minecraft Test Automation Research (2026-02-15)

Key findings from researching automated acceptance testing against a live Minecraft server:

- `execute if block X Y Z minecraft:<block>` is the canonical RCON command for block verification — returns empty string on match. Already used in our integration tests via `RconAssertions.AssertBlockAsync()`.
- `execute if blocks` compares two world regions block-by-block in a single RCON command — useful for whole-structure validation against a "golden reference" region.
- `data get block X Y Z` only works for block entities (chests, signs, banners), NOT simple blocks (stone, cobblestone, etc.). Useful for verifying sign text and banner patterns.
- `/testforblock` was removed in Minecraft 1.13+, replaced by `execute if block`.
- Structure blocks have a 48×48×48 size limit and require Docker file extraction to compare NBT — more complex than direct RCON checks.
- Paper server in Docker starts in ~45-60s cold (with JAR download), ~15-20s warm (cached JAR). Flat worlds with no players need ~1 GB RAM minimum.
- Minecraft's Java GameTest Framework (`@GameTest`) requires writing a Java mod/plugin — wrong tech stack for our .NET project. Not applicable.
- .NET NBT libraries exist (fNbt, SharpNBT, NbtToolkit, Unmined.Minecraft.Nbt) for parsing Anvil world files, but none handle the region file format out of the box — need a ~200-300 line wrapper.
- World file inspection (reading .mca files directly) bypasses RCON entirely and gives ground-truth block state. Requires `save-all flush` RCON command first to ensure chunks are written to disk.
- BlueMap can render headlessly via CLI (`java -jar bluemap-cli.jar -r`), but visual diff testing is inherently flaky (lighting, anti-aliasing) and not suitable as primary verification.
- Recommended approach: tiered strategy with RCON block verification as primary (P0), world file inspection as secondary (P1), and BlueMap visual regression as tertiary (P2).
📌 Team update (2026-02-15): Minecraft automated acceptance testing strategy — gap analysis and solution roadmap consolidated. Current state: 372 tests but zero world-state verification. Root cause: tests verify RCON command strings are correct but not what actually exists in the Minecraft world (command-ordering and fill-overlap bugs escape). P0 recommendations: fix integration test CI, add fill-overlap detection (unit test), expand RCON block verification (integration test). With CI optimizations, integration tests will run in ~2 minutes. — decided by Nebula, Rocket


 Team update (2026-02-16): Minecart lifecycle finalizedspawn on HTTP request, 3s timeout-based despawn at destination, max 5 carts/rail. NBT Age tracking with 5s polling cycle. ~1-2 RCON cmd/sec sustainable. Affects MinecartRailService implementation  decided by Rhodey

### Tech Branding Color System Update (2026-02-16)

Updated the `GetLanguageColor` method in StructureBuilder.cs to modernize tech stack color palette and apply Docker branding to Container resources:

**Color changes:**
- Rust: brown → red (matches Rust logo)
- Go: cyan → light_blue (matches Go gopher branding)
- Docker Container types: NEW cyan/aqua branding (Docker whale logo color)

**New language support:**
- PHP: magenta (Laravel/Symfony)
- Ruby: pink (Ruby/Rails)
- Elixir/Erlang: lime (Phoenix framework)

**Warehouse building enhancements:**
- Standard Warehouse: added language-colored stripe at y+2 (hollow fill) and banner on roof
- Grand Warehouse: added two hollow stripe bands (y+3, y+5) and four corner banners on roof (wall_banner facing N/S)
- Both now match Workshop buildings with tech branding visual identity
- Container types (AddContainer()) get aqua stripes/banners automatically

**RCON budget impact:** Standard Warehouse +2 commands, Grand Warehouse +6 commands — both well under burst mode limits.

**Final color palette:**
.NET Project = purple | JavaScript/Node = yellow | Python = yellow + blue secondary | Rust = red | Go = light_blue | Java/Spring = orange | Docker Container = cyan | PHP = magenta | Ruby = pink | Elixir/Erlang = lime | Default = white

📌 Team update (2026-02-16): Tech branding color system updated — Rust→red, Go→light_blue, Container→cyan, +3 new languages (PHP/Ruby/Elixir). Warehouse buildings now have language-colored stripes and banners matching Workshop aesthetic. — decided by Rocket

### Grand Village is Now the Only Option (2026-02-17)
- Removed small village structures entirely  no more conditional dispatch
- Grand layout values are now permanent defaults:
  - StructureSize = 15 (was 7 default)
  - Spacing = 36 (was 24 default)
  - GateWidth = 5 (was 3 default)
  - FenceClearance = 10 (unchanged)
- Removed ConfigureGrandLayout() method from VillageLayout.cs
- Removed ResetLayout() method from VillageLayout.cs
- Removed IsGrandLayout property from VillageLayout.cs
- Removed ASPIRE_FEATURE_GRAND_VILLAGE environment variable check from Program.cs
- All Build*Async methods in StructureBuilder.cs now directly call their BuildGrand*Async variants
- Central boulevard is always built now (removed IsGrandLayout guard, kept rows > 0 check)
- Rationale: Jeff's directive to focus on the grand design only. Simplifies codebase by removing two code paths for every structure type.

### NBT Library Evaluation for MCA Inspector (2026-02-18)

Evaluated 3 NBT library candidates (fNbt, SharpNBT, Unmined.Minecraft.Nbt) for issue #95 — prerequisite for AnvilRegionReader (#93) and fixture integration (#94).

**Decision:** Recommended **fNbt 1.0.0** (BSD-3-Clause, .NET Standard 2.0, most actively maintained, largest community).

**Key findings:**
- All 3 libraries are NBT-only parsers — none handle Anvil region (.mca) file format directly
- We must write our own AnvilRegionReader (~80 lines) for the MCA header/chunk offset/decompression layer
- Block state extraction from chunk NBT requires custom bit-unpacking logic for the 1.18+ palette format
- fNbt wins on maintenance (v1.0.0 Jul 2025), community adoption, NuGet availability, and .NET Standard 2.0 breadth
- SharpNBT viable but stale (last release Sep 2023, targets .NET 7 only)
- Unmined.Minecraft.Nbt too niche (pre-release, GitHub Packages only, 10 stars)
- Full evaluation with comparison table and conceptual API code written to `.ai-team/decisions/inbox/rocket-nbt-library-evaluation.md`

### AnvilRegionReader Implementation (Issue #93)

Implemented `AnvilRegionReader` in `tests/Aspire.Hosting.Minecraft.Integration.Tests/Helpers/AnvilRegionReader.cs` for reading Minecraft Anvil (.mca) region files. Used for test verification of in-world block placement.

**Key files:**
- `tests/Aspire.Hosting.Minecraft.Integration.Tests/Helpers/AnvilRegionReader.cs` — Full MCA reader with `GetBlockAt(worldX, worldY, worldZ)` API
- `tests/Aspire.Hosting.Minecraft.Integration.Tests/Aspire.Hosting.Minecraft.Integration.Tests.csproj` — Added fNbt 1.0.0 package reference

**Implementation details:**
- `AnvilRegionReader.Open(filePath)` parses `r.{x}.{z}.mca` filename and reads 4KB offset header
- `GetBlockAt()` converts world coords → region/chunk/section/block offsets, handles negative Y (1.18+ format, -64 to 319)
- Chunk decompression supports zlib (type 2), gzip (type 1), and uncompressed (type 3)
- Block state extraction navigates sections list → block_states compound → palette + packed long array
- Bit-unpacking uses 1.18+ format: indices don't span long boundaries, minimum 4 bits per entry
- `BlockState` record holds name + properties dictionary with human-readable `ToString()`
- Static helpers: `WorldToRegion()` and `GetRegionFilePath()` for coordinate/path lookups
- Edge cases: out-of-bounds Y returns null, missing chunks return null, wrong region returns null

## Learnings
- fNbt `NbtFile.LoadFromStream()` with `NbtCompression.None` works after manual decompression — don't double-decompress
- MCA sector offsets are 3-byte big-endian integers (shift+OR, not BitConverter) — Java heritage means all binary is big-endian
- 1.18+ section Y index for negative coords: use floor division `(worldY - 15) / 16` not truncation
- Palette bit-packing: indices per long = `64 / bitsPerEntry` (integer division), unused bits are padding at the high end
- `ZLibStream` (System.IO.Compression) handles the raw zlib format Minecraft uses — no need for third-party decompression

### Canal System Bug Fixes (2026-02-19)

**Three canal rendering bugs fixed in CanalService.cs:**

1. **Branch-to-trunk junction connectivity:** Trunk canal walls (built AFTER branches) sealed off every branch canal connection. Added OpenBranchJunctionsAsync post-pass that carves openings in the trunk's west wall at each branch canal's Z-level  air to remove wall, blue_ice for floor continuity, water to connect flows.

2. **Detour routing stays at original Z:** Branch canals that detoured south around blocking buildings never returned to their original Z-level, creating permanent southward drift. Fixed by resetting currentZ = z after each detour, producing clean U-shaped routes that return to the correct Z for trunk connection.

3. **Bridge elevation:** Connector bridges were placed at SurfaceY (same height as canal walls), creating flat ground-level surfaces. Raised to SurfaceY + 1 with oak_fence railings at SurfaceY + 2 for proper elevated walkway appearance.

## Learnings
- Build-order matters for overlapping fill commands: when trunk overwrites branch endpoints, a post-pass junction-carving step is needed rather than trying to prevent the overlap
- Canal junction pattern: air fill to remove wall, blue_ice floor, water to connect  three RCON commands per junction
- Bridge elevation needs to be at least SurfaceY + 1 (one above wall tops) with fence railings at +2 for visual walkway effect
- Detour routing must reset to original Z after passing each blocker to avoid permanent drift toward detour Z
- `CommunityToolkit.Aspire.Hosting.Java` AddSpringApp injects `JAVA_TOOL_OPTIONS=-javaagent:/opentelemetry-javaagent.jar` by default. If the container image stores the OTEL agent at a different path (e.g. `/agents/opentelemetry-javaagent.jar`), you MUST set `OtelAgentPath` in `JavaAppContainerResourceOptions` to match the image layout — otherwise the JVM fails on startup with "Error opening zip file or JAR manifest missing"
- The `aliencube/aspire-spring-maven-sample` image bundles its OTEL agent at `/agents/opentelemetry-javaagent.jar`, not at the root path the CommunityToolkit defaults to


 Team update (2026-02-19): Canal system junction fix  post-pass carving for branch-trunk connections, detour Z-reset for consistent junctions, bridge elevation to SurfaceY+1 with fence railings. ErrorBoatService and MinecartRailService should assume branch canals arrive at original entrance Z.  decided by Rocket
