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

### 2026-02-10: Deep dive into Worker service and RCON capabilities

**Worker service architecture:**
- `MinecraftWorldWorker` is a `BackgroundService` registered in `Program.cs`
- Polls metrics every 10s, broadcasts status every 2 min
- Services: `RconService` (instrumented wrapper), `AspireResourceMonitor` (env-var discovery + HTTP/TCP health), `HologramManager` (DecentHolograms plugin), `ScoreboardManager` (sidebar objective), `StructureBuilder` (3x3x2 block cubes + torch + sign), `PlayerMessageService` (tellraw/say + OTEL audit)
- Resource discovery is env-var based: `ASPIRE_RESOURCE_{NAME}_TYPE`, `_URL`, `_HOST`, `_PORT`

**RCON commands currently used:**
- `tps`, `mspt`, `list` — game metrics polling
- `dh create/delete/line add/set/remove` — DecentHolograms plugin for floating text
- `scoreboard objectives add/remove/setdisplay`, `scoreboard players set/reset` — sidebar display
- `fill`, `setblock`, `data merge block` — structure building + sign text
- `tellraw @a`, `say` — player-facing messages

**RCON commands available but NOT yet used (high potential):**
- `bossbar` — persistent bar at top of screen (perfect for fleet health %)
- `title @a title/subtitle/actionbar` — dramatic screen text + HUD ticker
- `particle` — visual effects at coordinates
- `playsound` — audio feedback on events
- `weather clear/rain/thunder` — atmosphere reflecting system state
- `worldborder set/warning` — world border as health indicator
- `summon` — spawn entities (guardian mobs per resource)
- `execute` — conditional command chaining

**Key patterns:**
- All RCON calls go through `RconService` which wraps `RconConnection` with OTEL activity tracing + latency histogram
- Structures use index-based linear layout: `BaseX + (index * Spacing)` at Y=-60 on superflat
- Resource type → block material mapping in `StructureBuilder.ResourceBlocks` dictionary
- Health status uses tri-state enum: `Unknown`, `Healthy`, `Unhealthy`
- Worker is created internally via `WithAspireWorldDisplay<TWorkerProject>()` — users never manually add it
- `WithMonitoredResource()` injects env vars into the worker's project builder stored on `MinecraftServerResource.WorkerBuilder`
- Connection string format: `Host=...;Port=...;Password=...` parsed in Program.cs

📌 Team update (2026-02-10): NuGet packages blocked — floating deps and bloated jar must be fixed in Sprint 1 — decided by Shuri
📌 Team update (2026-02-10): 3-sprint roadmap adopted — Sprint 1 assigns Rocket: boss bars, title alerts, sounds, weather, particles (all Size S) — decided by Rhodey
📌 Team update (2026-02-10): All sprint work tracked as GitHub issues with team member and sprint labels — decided by Jeffrey T. Fritz

### 2026-02-10: Sprint 1 features implemented — 5 RCON-based in-world effects

**Features implemented (all opt-in via builder extension methods):**
1. **ParticleEffectService** (`WithParticleEffects()`) — `particle` commands at structure coordinates on health transitions. Crash: `large_smoke` + `flame`, Recovery: `happy_villager`. Uses `force` mode for visibility from distance.
2. **TitleAlertService** (`WithTitleAlerts()`) — `title @a times/title/subtitle` commands. Red "⚠ SERVICE DOWN" on failure, green "✅ BACK ONLINE" on recovery. JSON text components with bold + color.
3. **WeatherService** (`WithWeatherEffects()`) — `weather clear/rain/thunder` mapped to fleet health ratio. Tracks last state to avoid redundant commands.
4. **BossBarService** (`WithBossBar()`) — `bossbar add/set` with persistent bar showing 0-100% fleet health. Color transitions: green→yellow→red. Only sends RCON when value or color actually changes.
5. **SoundEffectService** (`WithSoundEffects()`) — `playsound` commands. Down: `entity.wither.ambient`, Up: `entity.player.levelup`, All-green celebration: `ui.toast.challenge_complete`.

**Architecture decisions:**
- **Opt-in via env vars:** Each feature is toggled by `ASPIRE_FEATURE_{NAME}=true` env var, set by builder extension methods (e.g., `WithBossBar()` → `ASPIRE_FEATURE_BOSSBAR`). This preserves the existing pattern where the hosting library configures the worker via env vars on `WorkerBuilder`.
- **Optional DI injection:** Services are registered conditionally in `Program.cs` and injected as nullable parameters (`ParticleEffectService? particles = null`) into `MinecraftWorldWorker`'s primary constructor.
- **Per-resource vs aggregate features:** Particles, titles, and sounds fire per-resource-change. Weather and boss bar are aggregate (fleet-level) and update every poll cycle but only send commands on state transitions.
- **Transition-only logic:** Weather tracks `_lastWeather`, boss bar tracks `_lastValue`/`_lastColor` to avoid redundant RCON traffic.

**RCON command patterns used:**
- `particle minecraft:{type} {x} {y} {z} {dx} {dy} {dz} {speed} {count} force` — `force` ensures visibility from far away
- `title @a times {fadeIn} {stay} {fadeOut}` then `title @a title {json}` then `title @a subtitle {json}`
- `weather clear|rain|thunder` — no duration argument so it persists until next change
- `bossbar add {namespace:id} {json}` → `bossbar set {id} max|value|players|visible|color|name`
- `playsound minecraft:{sound} master @a ~ ~ ~ {vol} {pitch}` — `~ ~ ~` for relative coords, `master` channel

**Extension method conventions:**
- All 5 follow the `With{Feature}()` pattern matching `WithBlueMap()`, `WithOpenTelemetry()`, etc.
- All require `WithAspireWorldDisplay()` first (validated via `WorkerBuilder` null check).
- All return `IResourceBuilder<MinecraftServerResource>` for chaining.
- All set a single env var on the worker builder.

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
