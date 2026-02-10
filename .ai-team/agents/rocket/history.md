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
