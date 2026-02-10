# Project Context

- **Owner:** Jeffrey T. Fritz (csharpfritz@users.noreply.github.com)
- **Project:** Aspire.Hosting.Minecraft — .NET Aspire integration for Minecraft servers
- **Stack:** C#, .NET 10, Docker, Aspire, OpenTelemetry, Minecraft Paper Server, RCON
- **Created:** 2026-02-10

## Key Facts

- Worker service (Aspire.Hosting.Minecraft.Worker) handles in-world display
- Uses RCON to communicate with Minecraft server for commands
- Current features: hologram dashboards, scoreboards, torch-topped structures per resource
- DecentHolograms plugin for in-world holograms
- Worker is created internally by WithAspireWorldDisplay<TWorkerProject>()
- WithMonitoredResource() applies env vars to the internal worker
- Metrics: TPS, MSPT, players online, worlds loaded, RCON latency

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### Summary: Worker Architecture & Sprint 1 Features (2026-02-10)

- **Worker service:** `MinecraftWorldWorker` (BackgroundService), polls every 10s, broadcasts every 2 min. Services: `RconService`, `AspireResourceMonitor`, `HologramManager`, `ScoreboardManager`, `StructureBuilder`, `PlayerMessageService`. Resource discovery via env vars: `ASPIRE_RESOURCE_{NAME}_TYPE/URL/HOST/PORT`.
- **RCON commands in use:** `tps`, `mspt`, `list`, `dh create/delete/line add/set/remove`, `scoreboard *`, `fill`, `setblock`, `data merge block`, `tellraw @a`, `say`, `bossbar *`, `title @a *`, `particle`, `playsound`, `weather`, `summon`.
- **Key patterns:** All RCON calls via `RconService` (OTEL tracing + latency histogram). Index-based linear layout at Y=-60. Tri-state health: Unknown/Healthy/Unhealthy. Worker created via `WithAspireWorldDisplay<T>()`.
- **Sprint 1 features (5, all opt-in):** `WithParticleEffects()`, `WithTitleAlerts()`, `WithWeatherEffects()`, `WithBossBar()`, `WithSoundEffects()`. Each toggled by `ASPIRE_FEATURE_{NAME}=true` env var. Services injected as nullable. Particles/titles/sounds per-resource; weather/boss bar aggregate. State tracking avoids redundant RCON.
- **Health thresholds:** Weather: 100%=clear, ≥50%=rain, <50%=thunder. Boss bar: 100%=green, ≥50%=yellow, <50%=red.

📌 Team update (2026-02-10): NuGet packages blocked — floating deps and bloated jar must be fixed in Sprint 1 — decided by Shuri
📌 Team update (2026-02-10): 3-sprint roadmap adopted — Sprint 1 assigns Rocket: boss bars, title alerts, sounds, weather, particles (all Size S) — decided by Rhodey
📌 Team update (2026-02-10): All sprint work tracked as GitHub issues with team member and sprint labels — decided by Jeffrey T. Fritz
📌 Team update (2026-02-10): Redstone Dependency Graph + Service Switches proposed as Sprint 3 flagship feature — rich demo material — decided by Jeffrey T. Fritz
📌 Team update (2026-02-10): Single NuGet package consolidation — only one package ships now — decided by Jeffrey T. Fritz, Shuri
📌 Team update (2026-02-10): NuGet PackageId renamed from Aspire.Hosting.Minecraft to Fritz.Aspire.Hosting.Minecraft (Aspire.Hosting prefix reserved by Microsoft) — decided by Jeffrey T. Fritz, Shuri

### 2026-02-10: Sprint 2 features implemented — 3 new in-world interaction features

**Features implemented (all opt-in via builder extension methods):**
1. **Boss bar app name support** (`WithBossBar(appName?)`) — Issue #38. Added `ASPIRE_APP_NAME` env var support. `WithBossBar()` now accepts optional `appName` parameter. `BossBarService` reads `ASPIRE_APP_NAME` at startup, falls back to "Aspire". Boss bar title shows `{appName} Fleet Health: {value} percent`.
2. **ActionBarTickerService** (`WithActionBarTicker()`) — Issue #20. Cycles through TPS, MSPT, healthy resource count, and RCON latency via `title @a actionbar` command. Rotates each poll cycle. Uses `ASPIRE_FEATURE_ACTIONBAR` env var.
3. **BeaconTowerService** (`WithBeaconTowers()`) — Issue #22. Builds beacon-powered towers per monitored resource. 3x3 iron block base at Y=-60, beacon center at Y=-59, stained glass at Y=-58. Green glass = healthy, red glass = unhealthy. Towers at Z=8 offset to avoid overlap with existing 3x3 structures at Z=0. Uses `ASPIRE_FEATURE_BEACONS` env var.

**Architecture decisions:**
- **Consistent opt-in pattern:** All three features follow the Sprint 1 env var pattern — extension method sets env var, Program.cs registers conditionally, worker injects as nullable.
- **Action bar ticker design:** Reads metrics live each tick (TPS, MSPT via RCON parse, RCON latency via stopwatch) rather than caching from the main loop. This gives the action bar its own fresh data independent of the poll interval.
- **Beacon tower placement:** Z=8 offset chosen to separate beacon towers from existing structures (at Z=0) while keeping them visible in the same area. Single-layer iron base is the minimum for beacon activation in Minecraft.
- **Boss bar % symbol:** Used "percent" instead of "%" in RCON strings to avoid parsing issues, consistent with Sprint 1 pattern.

**RCON commands used:**
- `title @a actionbar "{message}"` — plain string, not JSON text component
- `fill {x} {y} {z} {x+2} {y} {z+2} minecraft:iron_block` — 3x3 iron base
- `setblock {x+1} {y+1} {z+1} minecraft:beacon` — beacon on center
- `setblock {x+1} {y+2} {z+1} minecraft:{color}_stained_glass` — health indicator

### 2026-02-10: Sprint 2 remaining features — fireworks, guardians, fanfare, startup optimization

**Features implemented (Issues #15, #18, #23):**
1. **FireworksService** (`WithFireworks()`) — Issue #15. Tracks `_wasAnyUnhealthy` state. When ALL resources transition to healthy from at least one being unhealthy, launches `summon firework_rocket` at 5 positions around the resource area. Uses `ASPIRE_FEATURE_FIREWORKS` env var.
2. **GuardianMobService** (`WithGuardianMobs()`) — Issue #18. Spawns iron golem per healthy resource, zombie per unhealthy resource. Mobs have `NoAI:1b`, `Invulnerable:1b`, `PersistenceRequired:1b` NBT tags to prevent wandering/despawning. Uses `kill @e[name=guardian_{name}]` to clean up before respawning. Tracks `_lastKnownStatus` per resource to avoid redundant respawns. Uses `ASPIRE_FEATURE_GUARDIANS` env var.
3. **DeploymentFanfareService** (`WithDeploymentFanfare()`) — Issue #23. Fires on `Unknown → Healthy` transitions (representing Starting → Running). Spawns lightning bolt, 2 fireworks, and shows title/subtitle announcement with plain strings. Uses `ASPIRE_FEATURE_FANFARE` env var.

**Investigation (Issue #37): Minecraft Server Startup Time**
- **Current config:** Using `itzg/minecraft-server:latest` with `TYPE=PAPER`, `LEVEL_TYPE=flat`, `SEED=aspire2026`, `MODE=creative`.
- **Easy wins implemented:**
  - `SPAWN_PROTECTION=0` — disables spawn protection radius (no need for it in a dashboard world)
  - `VIEW_DISTANCE=6` — reduced from default 10, less chunk generation on startup
  - `SIMULATION_DISTANCE=4` — reduced from default 10, less entity/block tick processing
  - `GENERATE_STRUCTURES=false` — disables structure generation (villages, temples) — saves world gen time
  - `SPAWN_ANIMALS=FALSE`, `SPAWN_MONSTERS=FALSE`, `SPAWN_NPCS=FALSE` — no mob spawning, saves tick budget
  - `MAX_WORLD_SIZE=256` — limits world border to 256 blocks radius (plenty for dashboard, prevents chunk gen on exploration)
- **Why these help:** Flat world + no structures + small world size = minimal chunk generation. No mob spawning = less entity processing. Reduced view/sim distance = fewer chunks loaded/ticked. These are all itzg/minecraft-server env vars that map to server.properties.
- **Not implemented (deferred):** Pre-generating world via volume mount, custom Paper config (paper-global.yml), Aikar's JVM flags (would conflict with OTEL agent JVM_OPTS), custom startup scripts.

**Architecture patterns confirmed:**
- Fireworks/fanfare are event-driven (fire on health transitions in the changes block)
- Guardian mobs are continuous (update every cycle, but track state to avoid redundant respawns)
- Mob cleanup via `kill @e[name=...]` selector is the standard pattern for entity management
- NBT tags (`NoAI`, `Invulnerable`, `PersistenceRequired`) essential for stationary display mobs

### 2026-02-10: Hologram bug fix — RCON throttle eating duplicate line-add commands

**Root cause:** `HologramManager.UpdateHologramLinesAsync()` used identical placeholder text `&7...` for every `dh line add` RCON command when growing the hologram. `RconService` has a 250ms command throttle that deduplicates identical command strings. Since the while loop runs without delays, only the first `dh line add` succeeded — the rest were silently throttled. This caused the hologram to show only 2 lines (title + 1 resource) instead of all registered resources.

**Fix:** Changed placeholder text to `&7line{_lastLineCount}` so each add command has a unique string. The placeholders are immediately overwritten by the `dh line set` calls that follow, so the visible content is unaffected.

**Key learning:** Any RCON command that runs in a tight loop with identical text will be eaten by the throttle. Future services that issue repetitive identical commands must either use unique command strings or add deliberate delays between calls.

### Beacon color fix — match Aspire dashboard resource type colors

**Bug:** Beacon towers used simple green/red stained glass for healthy/unhealthy. This didn't match the Aspire dashboard's resource-type-specific color palette.

**Fix:** Updated `BeaconTowerService` to map resource types to Minecraft stained glass colors matching the dashboard:
- **Healthy:** Project → `blue_stained_glass`, Container → `purple_stained_glass`, Executable → `cyan_stained_glass`, Unknown type → `light_blue_stained_glass`
- **Unhealthy:** `red_stained_glass`
- **Starting (Unknown status):** `yellow_stained_glass`

**Implementation:** Added `GetGlassBlock(ResourceInfo)` static method with a `ResourceTypeGlassColors` dictionary (case-insensitive). Uses `info.Status` as the primary switch — unhealthy always shows red, unknown shows yellow, healthy fans out to type-specific colors via `info.Type`. Method is `internal static` for testability.

**Key learning:** `ResourceInfo.Type` is already populated from the `ASPIRE_RESOURCE_{NAME}_TYPE` env var set by `WithMonitoredResource()`. No new plumbing needed — just consume the existing field.

📌 Team update (2026-02-10): NuGet package version now defaults to 0.1.0-dev; CI overrides via -p:Version from git tag — decided by Shuri
📌 Team update (2026-02-10): Release workflow extracts version from git tag and passes to dotnet build/pack — decided by Wong
📌 Team update (2026-02-10): Sprint 2 API review complete — 5 additive recommendations for Sprint 3 (WithAllFeatures, ParseConnectionString extraction, IRconCommandSender, env var tightening, auto-discovery) — decided by Rhodey
📌 Team update (2026-02-10): WithServerProperty API and ServerProperty enum added for server.properties configuration — decided by Shuri

### Resource Village with Themed Architecture (Issue #25)

**What:** Replaced simple 3×3×2 colored block platforms with themed mini-buildings per Aspire resource type. Created a centralized `VillageLayout` helper for 2×N grid positioning.

**Structure types:**
- **Watchtower** (Project/.NET app): Stone brick tower, 10 blocks tall, blue wool trim, blue banner flag. Corner pillars, glass pane windows.
- **Warehouse** (Container/Docker): Iron block frame, wide cargo bay door, purple stained glass windows, barrel storage interior. 5 blocks tall.
- **Workshop** (Executable): Oak plank walls, peaked A-frame roof, cyan stained glass accents, cobblestone chimney with campfire, crafting table + anvil. 7 blocks tall.
- **Cottage** (Unknown/Other): Cobblestone walls, light blue wool trim, cobblestone slab roof, glass pane windows. 5 blocks tall.

**Layout change:** Linear (Spacing=6, single row) → 2×N grid (Spacing=10, two columns). Cobblestone path between columns. `VillageLayout` static class centralizes coordinate calculation for all services.

**Health indicator:** Replaced torch-on-top with redstone lamp in front wall at eye level. Glowstone (healthy/always lit), redstone lamp (unhealthy/unlit), sea lantern (unknown/starting).

**Services updated:**
- `StructureBuilder` — complete rewrite with 4 structure templates + path builder
- `ParticleEffectService` — uses `VillageLayout.GetAboveStructure()` for consistent positioning
- `BeaconTowerService` — updated to 2×N grid layout, BaseZ moved to 14 (outside 7-block village footprint)
- `GuardianMobService` — updated to 2×N grid layout

**Key learnings:**
- `fill ... hollow` is the most efficient RCON command for building walls — one command creates a hollow rectangular shell.
- Each structure uses ~15-25 RCON commands (well within the 50-100 target), far fewer than individual setblock calls.
- The 2×N grid layout scales better than linear — 10 resources only need 5 rows instead of spreading 60+ blocks in one direction.
- `VillageLayout` as a shared static class prevents coordinate drift between services. All services that place things per-resource-index should use it.
- Stairs with `facing` and `half` NBT properties create convincing peaked roofs with minimal commands.

### Azure Resource Visualization Design (2026-02-10)

**What:** Created comprehensive design document (`docs/epics/azure-minecraft-visuals.md`) mapping 15 Azure resource types to Minecraft structures for in-world visualization of live Azure Resource Groups.

**Key design decisions:**
- **Two-universe separation:** Aspire village = warm medieval workshop (wood, stone, torches). Azure citadel = cool prismarine/quartz/end stone civilization. Instant visual distinction between local dev and cloud infrastructure.
- **Azure block palette:** Primary: `prismarine_bricks`, `dark_prismarine`, `quartz_block`, `end_stone_bricks`. Accent: `sea_lantern`, `end_rod`, `amethyst`, `crying_obsidian`. Chosen to evoke "built by a higher power" — ocean monument and End dimension blocks.
- **Structure-to-resource metaphors:** App Service=Cathedral (workhorse, central), Key Vault=Obsidian Stronghold (impenetrable, compact), AKS=Honeycomb Tower (pod cells), Redis=Redstone Engine (fast signal), Function App=Lightning Spire (event-driven, ephemeral).
- **3-column tiered layout:** Resources grouped by functional tier (Gateway → Compute → Data → Infra → Monitoring) instead of alphabetical. Mirrors how Azure architects think. 14-block spacing for larger structures.
- **District separation:** Azure citadel at X=60 (20-block gap from Aspire village at X=10–40). Connected by prismarine boulevard with end rod lampposts.
- **Azure health states are richer than Aspire:** Stopped=dark+cobwebs, Deallocated=soul sand perimeter, Failed=netherrack+fire on roof. Provisioning states: Creating=live build animation, Deleting=reverse deconstruction.
- **Scale handling:** 1–15 resources = 3×5 grid. 16–30 = 3×10. 50+ = multiple Z-offset planes. Progress boss bar during initial build.
- **RCON budget:** 15–35 commands per Azure structure (comparable to Aspire's 15–25). 15 resources ≈ 94s build time. 50 resources ≈ 5.2 min (needs progress indicator).
- **Beacon color palette for Azure:** Compute=cyan, Data=blue, Networking=purple, Security=black, Messaging=orange, Observability=magenta. Unhealthy=red, Starting=yellow (consistent with Aspire).

### Village Fence Perimeter and Cobblestone Pathways (2026-02-10)

**What:** Added oak fence perimeter around the entire village and enhanced cobblestone pathway network between buildings.

**Fence perimeter:**
- `VillageLayout.GetVillageBounds(resourceCount)` computes the bounding box of all structures.
- `VillageLayout.GetFencePerimeter(resourceCount)` returns coordinates 1 block outside bounds (2 blocks on south/entrance side for the entry path).
- Oak fence placed at `BaseY + 1` (on top of ground level) on all four sides.
- 3-wide `oak_fence_gate[facing=south]` centered on the main boulevard on the south side.
- Fence is built BEFORE structures and paths so structures can overlap if needed.

**Cobblestone pathways:**
- Main boulevard: 3-wide cobblestone strip between the two columns along Z axis (enhanced from existing single strip).
- Cross paths: 2-wide cobblestone paths from each structure's entrance (`z-1`) connecting to the main boulevard. Left column paths run east, right column paths run west.
- Entry path: 3-wide cobblestone from the fence gate to the start of the main boulevard.
- All paths at `BaseY` level (ground level).

**Key coordinate decisions:**
- Fence perimeter: `minZ = BaseZ - 2` (south side gets 2-block offset for entry path + gate), other sides 1-block offset.
- Boulevard X starts at `BaseX + StructureSize` (X=17), which is the 3-block gap between columns.
- Cross path entrance X is at `structureOriginX + 3` (center of the 7-wide structure front face).
- Gate aligned with boulevard center for seamless entry path connection.

### Redstone Heartbeat Circuit (Issue #27)

**What:** Implemented `HeartbeatService` — an audible note block heartbeat that pulses at a rate proportional to fleet health. Unlike other services (which are singletons called from the main worker loop), this is a `BackgroundService` with its own independent timing loop, allowing sub-second pulse intervals that the 10-second display update cycle can't support.

**Health-to-rhythm mapping:**
- 100% healthy: note every 1 second, pitch 1.5 (steady, high-pitched heartbeat)
- 50–99% healthy: note every 2 seconds, pitch 1.0 (slower beat)
- 1–49% healthy: note every 4 seconds, pitch 0.7 (labored, low-pitched pulse)
- 0% healthy: silence (flatline)

**RCON command:** `execute at @a run playsound minecraft:block.note_block.bass block @a ~ ~ ~ {volume} {pitch}` — plays at each online player's location. Volume varied slightly per tick (0.500–0.509) to avoid the 250ms RCON deduplication throttle eating repeated commands.

**Architecture:**
- First `BackgroundService` feature (all others are singletons invoked by `MinecraftWorldWorker`). Registered with `AddHostedService<HeartbeatService>()` instead of `AddSingleton`.
- Feature gate: `ASPIRE_FEATURE_HEARTBEAT` env var, set by `WithHeartbeat()` extension method.
- Waits for `monitor.TotalCount > 0` before starting pulse loop (resources must be discovered first by the main worker).
- No injection into `MinecraftWorldWorker` — runs completely independently.

**Key learnings:**
- `BackgroundService` is the right pattern when a feature needs its own timing independent of the main poll loop. Singletons called from the worker are limited to the 10s `DisplayUpdateInterval`.
- Volume micro-variation (0.001 increments) is inaudible to players but makes each RCON command string unique, sidestepping the throttle without adding delays.

### Advancement Achievements — Gamification (Issue #32)

**What:** Created `AdvancementService` that grants in-world achievement-style announcements for infrastructure milestones. Uses RCON title commands and sounds instead of Minecraft datapack advancements (which are complex to mount via Docker).

**Four achievements (granted once per session):**
1. **First Service Online** — granted when the first resource transitions to Healthy
2. **Full Fleet Healthy** — granted when ALL resources are Healthy simultaneously
3. **Survived a Crash** — granted when a resource recovers from Unhealthy → Healthy (tracks previous status per resource)
4. **Night Shift** — granted when all resources healthy during Minecraft night (ticks 13000-23000, queried via `time query daytime`)

**Implementation approach:**
- RCON `title @a title/subtitle` with JSON text components for gold/yellow achievement popups
- `playsound minecraft:ui.toast.challenge_complete` for achievement sound
- `HashSet<string>` tracks granted achievements to ensure once-per-session
- `Dictionary<string, ResourceStatus>` tracks per-resource previous status for crash recovery detection
- `ParseDaytimeTicks()` is `internal static` for testability — extracts tick count from "The time is {ticks}" response
- Called both on health transitions (for First Service, Full Fleet, Survived a Crash) and every cycle (for Night Shift time check)

**Opt-in pattern:** `ASPIRE_FEATURE_ACHIEVEMENTS` env var, `WithAchievements()` extension method, conditional registration in Program.cs, nullable injection in MinecraftWorldWorker constructor. Fully consistent with Sprint 1/2 patterns.

**Key decision:** No Minecraft datapacks — title + subtitle + sound gives equivalent player feedback without needing to mount custom advancement JSON into the container's data directory.

### Redstone Dependency Graph — visualize resource connections (Issue #36)

**What:** Created `RedstoneDependencyService` (BackgroundService) that draws redstone wire circuits between dependent resource structures in the Minecraft world. Uses the `Dependencies` property on `ResourceInfo` (populated from `ASPIRE_RESOURCE_{NAME}_DEPENDS_ON` env vars) to build visual connections.

**How it works:**
- On startup (after structures are built, with 15s delay), calculates L-shaped wire paths between dependent structures
- For each dependency edge: lays `minecraft:redstone_wire` along the ground, places `minecraft:repeater` every 15 blocks, and `minecraft:redstone_lamp` at structure entrances
- Powers circuit with `minecraft:redstone_block` next to parent lamp
- Wire routing: L-shaped paths (X-axis first, then Z-axis) to avoid complex pathfinding
- Uses `VillageLayout.ReorderByDependency()` and `GetStructureOrigin()` for consistent positioning

**Health-reactive wiring:**
- Monitors resource health changes every 5 seconds
- When a resource goes unhealthy: removes redstone block (kills power) and breaks wire at every 5th position (visual disconnect)
- When it recovers: restores wire positions and redstone block (circuit lights up again)
- Tracks `_connectionState` per edge and `_lastKnownStatus` per resource to avoid redundant RCON commands

**Architecture:**
- Second `BackgroundService` feature (after `HeartbeatService`). Registered with `AddHostedService<RedstoneDependencyService>()`.
- Feature gate: `ASPIRE_FEATURE_REDSTONE_GRAPH` env var, set by `WithRedstoneDependencyGraph()` extension method.
- Uses `CommandPriority.Low` for wire placement (bulk building commands) and `CommandPriority.Normal` for health-reactive toggles.

**Key learnings:**
- Redstone wire auto-connects to adjacent wire and repeaters — no need to specify connection state in block data.
- Repeater `facing` property must point toward the signal destination for correct signal propagation.
- L-shaped routing (X then Z) is simple and avoids structure collisions since structures are on a grid.
- Breaking wire at intervals (every 5th block) rather than removing all wire is more visually dramatic and uses fewer RCON commands.

### Service Switches — visual resource status levers (Issue #35)

**What:** Created `ServiceSwitchService` (BackgroundService) that places Minecraft levers and redstone lamps on each resource structure to visually represent service status. Healthy = lever ON + glowstone (always lit), Unhealthy = lever OFF + redstone lamp (unlit).

**Architecture:**
- Third `BackgroundService` feature (after `HeartbeatService` and `RedstoneDependencyService`). Registered with `AddHostedService<ServiceSwitchService>()`.
- Feature gate: `ASPIRE_FEATURE_SWITCHES` env var, set by `WithServiceSwitches()` extension method.
- Waits for `monitor.TotalCount > 0` + 15s delay (structures must be built first).
- Monitors health changes every 5 seconds, tracks `_lastKnownStatus` per resource to avoid redundant RCON commands.

**IMPORTANT CONSTRAINT:** Visual only — levers reflect state, they don't control Aspire resources. The `ResourceNotificationService` is read-only from the worker's perspective.

**Placement:**
- Lever at `(x+1, y+2, z)` on the front wall (z-min side) of each structure.
- Lamp at `(x+1, y+3, z)` above the lever on the front wall.
- Uses `minecraft:lever[face=wall,facing=south,powered=true/false]` block data.
- Lamp uses glowstone (always lit) when powered, redstone_lamp (unlit) when not — same pattern as existing health indicator in StructureBuilder.

**Key learnings:**
- Minecraft levers with `powered=true/false` block state can be placed/updated via `setblock` RCON commands without player interaction.
- Using glowstone vs redstone_lamp (rather than trying to power/unpower a lamp via redstone) is more reliable since RCON `setblock` doesn't propagate redstone signal updates.
