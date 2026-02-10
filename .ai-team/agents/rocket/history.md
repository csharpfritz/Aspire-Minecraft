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
