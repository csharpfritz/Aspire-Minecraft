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

📌 Team update (2026-02-10): Redstone Dependency Graph + Service Switches proposed as Sprint 3 flagship feature — rich demo material — decided by Jeffrey T. Fritz
📌 Team update (2026-02-10): Single NuGet package consolidation — only one package ships now — decided by Jeffrey T. Fritz, Shuri
