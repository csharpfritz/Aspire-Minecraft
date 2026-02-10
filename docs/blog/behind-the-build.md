# Behind the Build: How Fritz.Aspire.Hosting.Minecraft Works

> **Author:** Jeffrey T. Fritz  
> **Audience:** .NET developers who want to understand or contribute  
> **Package:** [Fritz.Aspire.Hosting.Minecraft](https://www.nuget.org/packages/Fritz.Aspire.Hosting.Minecraft)

---

When I first wired up a Minecraft server as an Aspire resource, the demo was simple: run `dotnet run`, watch a Minecraft server appear in the dashboard, connect with the game client, and see some blocks. It was neat. But it wasn't *interesting*.

The moment it became interesting was when we added health monitoring. A Minecraft world that reacts to your distributed system — where the weather darkens because your Redis cache went down, where a wither's growl plays because your API threw a 503 — that's not a dashboard. That's an experience. And it turns out, building that experience required solving some genuinely tricky engineering problems.

This post walks through the architecture of `Fritz.Aspire.Hosting.Minecraft` — from the .NET BackgroundService that orchestrates it all, to the RCON commands that build villages and summon weather, to the design decisions that keep the whole thing from melting your Minecraft server. If you've ever wondered how a .NET worker talks to a Java game server running in Docker, this is your post.

## Architecture Overview

The system has three layers:

```
AppHost (your Aspire application)
  │
  ├── Minecraft Server (Docker: itzg/minecraft-server)
  │     ├── Paper Server + BlueMap + DecentHolograms plugins
  │     ├── OTEL Java Agent → Aspire Dashboard
  │     └── Ports: 25565 (game), 8100 (BlueMap), 25575 (RCON)
  │
  ├── Minecraft Worker Service (.NET BackgroundService)
  │     ├── Connects to server via RCON
  │     ├── Polls sibling resource health via HTTP/TCP
  │     ├── Translates health states → RCON commands
  │     └── Publishes game metrics via OpenTelemetry
  │
  └── Your Services (API, Web, Redis, Postgres, etc.)
        └── Monitored and visualized in-world
```

The hosting library (`Aspire.Hosting.Minecraft`) configures the Minecraft Docker container and internally registers a worker service project. The worker is the brain — it discovers sibling Aspire resources, polls their health, and issues RCON commands to the Minecraft server to reflect that state in the game world.

## The Worker Service Pattern

The worker is a standard .NET `BackgroundService` using `Host.CreateApplicationBuilder`. Here's the heart of it — the main loop in `MinecraftWorldWorker`:

```csharp
// From src/Aspire.Hosting.Minecraft.Worker/Program.cs
file sealed class MinecraftWorldWorker(
    RconService rcon,
    AspireResourceMonitor resourceMonitor,
    PlayerMessageService playerMessages,
    HologramManager holograms,
    ScoreboardManager scoreboard,
    StructureBuilder structures,
    ILogger<MinecraftWorldWorker> logger,
    // Opt-in feature services — null when not enabled
    ParticleEffectService? particles = null,
    TitleAlertService? titleAlerts = null,
    WeatherService? weather = null,
    BossBarService? bossBar = null,
    SoundEffectService? sounds = null,
    ActionBarTickerService? actionBarTicker = null,
    BeaconTowerService? beaconTowers = null,
    FireworksService? fireworks = null,
    GuardianMobService? guardianMobs = null,
    DeploymentFanfareService? deploymentFanfare = null,
    WorldBorderService? worldBorder = null,
    AdvancementService? achievements = null) : BackgroundService
```

Notice the nullable parameters. Each opt-in feature is registered in DI only when its environment variable is set. The worker asks for them via constructor injection, and if they're null, it skips them. This is the core design decision: **features are zero-cost when disabled**. No extra RCON traffic, no extra CPU, no unused services sitting in memory.

The loop itself runs on a 10-second interval:

1. **Poll game metrics** — TPS, MSPT, player count via RCON
2. **Poll resource health** — HTTP health checks for web services, TCP socket checks for databases and caches
3. **React to transitions** — particles, title alerts, sounds, fireworks, achievements on state changes
4. **Update displays** — holograms, scoreboard, village structures every cycle
5. **Update fleet-wide indicators** — weather, boss bar, action bar ticker, beacons, world border
6. **Periodic summary** — broadcast a status message to chat every 2 minutes

## Feature Opt-In via Environment Variables

Every feature follows the same pattern. On the hosting side, the `With*()` extension method sets an environment variable on the worker project:

```csharp
// From src/Aspire.Hosting.Minecraft/MinecraftServerBuilderExtensions.cs
public static IResourceBuilder<MinecraftServerResource> WithWeatherEffects(
    this IResourceBuilder<MinecraftServerResource> builder)
{
    var workerBuilder = builder.Resource.WorkerBuilder
        ?? throw new InvalidOperationException(
            "WithWeatherEffects() requires WithAspireWorldDisplay() to be called first.");

    workerBuilder.WithEnvironment("ASPIRE_FEATURE_WEATHER", "true");
    return builder;
}
```

On the worker side, `Program.cs` checks the variable and registers the service:

```csharp
// From src/Aspire.Hosting.Minecraft.Worker/Program.cs
if (!string.IsNullOrEmpty(builder.Configuration["ASPIRE_FEATURE_WEATHER"]))
    builder.Services.AddSingleton<WeatherService>();
```

This pattern repeats for all 13 features. The naming convention is always `ASPIRE_FEATURE_{NAME}`. The check is `!string.IsNullOrEmpty()`, so any truthy value enables the feature.

**Why environment variables instead of a configuration object?** Because the worker is a separate .NET project that runs in its own process. The hosting library and the worker communicate through environment variables that Aspire injects into the worker's launch environment. There's no shared configuration object — just key-value pairs.

## Village Layout System

When the worker discovers Aspire resources, it builds a village — a collection of themed structures laid out in a 2-column grid. The `VillageLayout` class centralizes all positioning:

```csharp
// From src/Aspire.Hosting.Minecraft.Worker/Services/VillageLayout.cs
internal static class VillageLayout
{
    public const int BaseX = 10;
    public const int BaseY = -60;   // Surface level for superflat world
    public const int BaseZ = 0;
    public const int Spacing = 10;  // Center-to-center distance
    public const int Columns = 2;
    public const int StructureSize = 7;  // 7×7 footprint

    public static (int x, int y, int z) GetStructureOrigin(int index)
    {
        var col = index % Columns;
        var row = index / Columns;
        return (BaseX + (col * Spacing), BaseY, BaseZ + (row * Spacing));
    }
}
```

The grid looks like this for 4 resources:

```
     Col 0        Col 1
     ┌───────┐    ┌───────┐
Row 0│ api   │    │ web   │
     └───────┘    └───────┘
     ┌───────┐    ┌───────┐
Row 1│ cache │    │ db    │
     └───────┘    └───────┘

Boulevard runs between columns (cobblestone paths)
Oak fence perimeter with gate at the south entrance
```

Each resource gets a building whose style depends on the Aspire resource type:

| Aspire Resource Type | Minecraft Structure | Material |
|---|---|---|
| `Project` (e.g., API, Web) | Watchtower — tall, narrow tower with flag | Stone bricks + blue wool |
| `Container` (e.g., Redis) | Warehouse — wide, flat cargo bay | Iron blocks + purple glass |
| `Executable` | Workshop — small with chimney | Oak planks + cyan glass |
| Unknown / Other | Cottage — humble dwelling | Cobblestone + light blue wool |

The mapping lives in `StructureBuilder.GetStructureType()`:

```csharp
// From src/Aspire.Hosting.Minecraft.Worker/Services/StructureBuilder.cs
internal static string GetStructureType(string resourceType) =>
    resourceType.ToLowerInvariant() switch
    {
        "project"    => "Watchtower",
        "container"  => "Warehouse",
        "executable" => "Workshop",
        _            => "Cottage"
    };
```

The `VillageLayout` also does dependency-aware ordering. If your API depends on your database, they'll be placed adjacent in the grid. This uses a BFS topological sort — parents before children, shared dependencies grouped together.

## Health Monitoring Pipeline

The health monitoring pipeline is the core of the system. Here's how it flows:

```
Aspire Resources (API, Redis, Postgres, etc.)
    │
    ▼
AspireResourceMonitor.PollHealthAsync()
    │   HTTP GET → 200 OK = Healthy, anything else = Unhealthy
    │   TCP Connect → success = Healthy, timeout = Unhealthy
    │
    ▼
List<ResourceStatusChange>  (only on state transitions)
    │
    ├──▶ ParticleEffectService     — smoke/flame or happy_villager
    ├──▶ TitleAlertService         — full-screen "⚠ SERVICE DOWN"
    ├──▶ SoundEffectService        — wither.ambient or player.levelup
    ├──▶ FireworksService          — celebrate all-green recovery
    ├──▶ DeploymentFanfareService  — lightning + fireworks on deploy
    └──▶ AdvancementService        — "Survived a Crash" achievement
```

Resource discovery happens at startup by scanning environment variables:

```csharp
// From src/Aspire.Hosting.Minecraft.Worker/Services/AspireResourceMonitor.cs
public void DiscoverResources()
{
    var envVars = Environment.GetEnvironmentVariables();
    foreach (DictionaryEntry entry in envVars)
    {
        var key = entry.Key?.ToString() ?? "";
        if (key.StartsWith("ASPIRE_RESOURCE_") && key.EndsWith("_TYPE"))
        {
            var name = key["ASPIRE_RESOURCE_".Length..^"_TYPE".Length]
                .ToLowerInvariant();
            // Also reads _URL (HTTP), _HOST/_PORT (TCP), _DEPENDS_ON
        }
    }
}
```

The convention is straightforward:
- `ASPIRE_RESOURCE_API_TYPE=Project` — resource name and type
- `ASPIRE_RESOURCE_API_URL=http://host:port` — HTTP endpoint for health checks
- `ASPIRE_RESOURCE_CACHE_HOST=redis` + `_PORT=6379` — TCP endpoint for non-HTTP resources
- `ASPIRE_RESOURCE_API_DEPENDS_ON=cache,db` — dependency ordering for village layout

The hosting library's `WithMonitoredResource()` method sets all of these. It even auto-detects endpoint types — HTTP/HTTPS endpoints use URL-based health checks, while TCP-only resources (like Redis) get socket-based checks.

## RCON Command Patterns and Gotchas

RCON is the Remote Console protocol — originally from Valve's Source engine, adopted by Minecraft. We use it to send server commands: building blocks, spawning entities, changing weather, playing sounds.

The `RconService` wraps the raw connection with three critical layers:

### 1. Command Throttling (250ms Deduplication)

The Minecraft server runs on a single thread at 20 ticks per second. Flooding it with RCON commands causes lag spikes. The `RconService` deduplicates identical commands sent within 250ms:

```csharp
// From src/Aspire.Hosting.Minecraft.Worker/Services/RconService.cs
if (_lastCommandTimes.TryGetValue(command, out var lastTime)
    && (now - lastTime) < _minCommandInterval)
{
    return string.Empty;  // Throttled — skip duplicate
}
_lastCommandTimes[command] = now;
```

This caught a real bug: the `HologramManager` was issuing identical `dh line add &7...` commands in a tight loop. The throttle silently dropped them, resulting in missing hologram lines. The fix was using unique text per line: `&7line{n}` instead of `&7...`.

**Lesson:** If you're sending multiple RCON commands in sequence and they have identical text, the deduplication will eat them. Always make commands unique.

### 2. Rate Limiting (Token Bucket)

Beyond deduplication, there's a rate limiter: 10 commands per second by default. High-priority commands (like metric polling) bypass it. Low-priority commands (like structure updates) queue:

```csharp
private readonly Channel<QueuedCommand> _commandQueue;

// High-priority: always goes through
// Normal: waits briefly for a token
// Low: queues for later execution
```

### 3. JSON Text Components

Several RCON commands use Minecraft's JSON text format for colored and styled text. For example, title alerts:

```csharp
// From src/Aspire.Hosting.Minecraft.Worker/Services/TitleAlertService.cs
await rcon.SendCommandAsync(
    """title @a title {"text":"⚠ SERVICE DOWN","color":"red","bold":true}""", ct);
await rcon.SendCommandAsync(
    $$"""title @a subtitle {"text":"{{change.Name}}","color":"gray"}""", ct);
```

The boss bar uses percent signs, which is noteworthy because RCON over some implementations can interpret `%` as a format specifier. We avoided this by spelling out "percent" in the display text:

```csharp
// From src/Aspire.Hosting.Minecraft.Worker/Services/BossBarService.cs
await rcon.SendCommandAsync(
    $"bossbar set {BossBarId} name \"{_appName} Fleet Health: {value} percent\"", ct);
```

### 4. State Tracking

Every fleet-level feature tracks its last state and only sends commands on transitions:

```csharp
// From src/Aspire.Hosting.Minecraft.Worker/Services/WeatherService.cs
var desired = healthyRatio switch
{
    1.0   => WeatherState.Clear,
    >= 0.5 => WeatherState.Rain,
    _      => WeatherState.Thunder
};

if (desired == _lastWeather) return;  // No change — skip RCON command

await rcon.SendCommandAsync(command, ct);
_lastWeather = desired;
```

Without state tracking, the worker would issue `weather clear` every 10 seconds even when the weather is already clear. That's wasted RCON bandwidth and server ticks.

## Feature Deep-Dives

### Weather Effects

Fleet health maps to Minecraft weather through a simple ratio:

| Healthy Ratio | Weather | Visual Effect |
|---|---|---|
| 100% | Clear | Blue sky, sunshine |
| ≥ 50% | Rain | Gray clouds, rain drops |
| < 50% | Thunder | Dark sky, lightning, rain |

One important design choice: the thresholds are 100% / 50% rather than continuous. We tried proportional cloud cover, but Minecraft only has three weather states. A binary "degraded" threshold at 50% prevents flickering between rain and thunder when you have 3/6 services oscillating.

### Boss Bar

The boss bar is a persistent UI element at the top of the screen. The lifecycle:

1. `bossbar add aspire:fleet_health "Aspire Fleet Health"` — create once
2. `bossbar set aspire:fleet_health players @a` — re-sent every cycle so new players see it
3. `bossbar set aspire:fleet_health value 75` — only on value change
4. `bossbar set aspire:fleet_health color yellow` — only on color change

The color breakpoints: green at 100%, yellow at 50–99%, red below 50%.

### Beacon Towers

Each monitored resource gets an iron-base beacon with colored stained glass above it. The glass color matches the Aspire dashboard's resource type palette:

```csharp
// From src/Aspire.Hosting.Minecraft.Worker/Services/BeaconTowerService.cs
["Project"]    = "blue_stained_glass",
["Container"]  = "purple_stained_glass",
["Executable"] = "cyan_stained_glass",
// Unknown type = light_blue_stained_glass
// Unhealthy    = red_stained_glass
// Starting     = yellow_stained_glass
```

The beacons are placed at Z-offset 14 from the village structures to avoid collision. When a resource goes unhealthy, its beacon glass switches to red — the beam color changes instantly, visible from hundreds of blocks away.

### Heartbeat

The heartbeat is the only feature that runs on its own `BackgroundService` timing loop, independent of the main 10-second worker cycle. It plays a note block bass sound whose tempo reflects fleet health:

| Fleet Health | Interval | Pitch | Feel |
|---|---|---|---|
| 100% | 1 second | 1.5 | Steady, confident pulse |
| 50–99% | 2 seconds | 1.0 | Slower beat |
| 1–49% | 4 seconds | 0.7 | Labored, concerning |
| 0% | Silent | — | Flatline |

A subtle trick: the volume varies slightly each tick (`0.5 + (tick % 10) * 0.001`) to avoid the RCON 250ms deduplication throttle. Without this, the identical `playsound` command would get dropped as a duplicate.

### Achievements

The achievement system grants one-time announcements using title popups and the `ui.toast.challenge_complete` sound. Current achievements:

- **First Service Online** — first resource transitions to Healthy
- **Full Fleet Healthy** — all resources Healthy simultaneously  
- **Survived a Crash** — a resource recovers from Unhealthy to Healthy
- **Night Shift** — all resources healthy during Minecraft nighttime (ticks 13000–23000)

Each achievement is tracked in a `HashSet<string>` and only fires once per session.

## OpenTelemetry: Two Worlds of Metrics

The system produces two distinct telemetry streams:

**JVM Metrics (automatic):** The OpenTelemetry Java agent is bind-mounted into the Minecraft container and configured via environment variables. It instruments JVM heap, GC, threads, and CPU automatically — no code needed on the Java side.

**Game Metrics (via RCON polling):** The worker polls RCON commands (`tps`, `mspt`, `list`) every 5 seconds and publishes them as .NET `System.Diagnostics.Metrics`:

- `minecraft.tps` — server ticks per second (target: 20.0)
- `minecraft.mspt` — milliseconds per tick
- `minecraft.players.online` / `minecraft.players.max`
- `minecraft.rcon.latency_ms` — round-trip time for RCON commands

Both streams flow into the Aspire dashboard's OTLP endpoint, giving you a unified view of your Minecraft server's JVM health and game-level performance alongside your .NET services.

## Lessons Learned

1. **RCON is synchronous and single-threaded.** The Minecraft server processes RCON commands on the main server thread. Flood it, and you'll drop TPS. The rate limiter and state tracking are not optional — they're essential.

2. **Identical commands get throttled.** The 250ms deduplication is intentional to prevent server flooding during rapid health oscillations. But it means you must ensure unique command strings when sending similar commands in sequence.

3. **State tracking prevents command spam.** Without `_lastWeather`, `_lastColor`, `_lastValue` fields, every 10-second cycle would re-send dozens of RCON commands even when nothing changed.

4. **Minecraft commands are not transactional.** If the worker crashes mid-village-build, you get a half-built structure. The next restart just builds over it. Idempotency is on you — `fill` and `setblock` commands overwrite whatever was there.

5. **The `%` character in RCON.** Some RCON implementations interpret `%` as a format specifier. The boss bar text avoids this by spelling out "percent" instead of using the `%` symbol.

6. **Environment variables are the communication channel.** The hosting library and worker run in separate processes. There's no shared DI container or configuration object. Everything flows through `ASPIRE_FEATURE_*` and `ASPIRE_RESOURCE_*` environment variables.

7. **The OTEL Java agent is 23 MB.** We embed it in the NuGet package rather than downloading at runtime. Offline environments and restricted networks make runtime downloads unreliable.

## What's Next

The architecture is designed to be extensible. Adding a new feature means:

1. Create a service class in the Worker project
2. Add a `With*()` extension method that sets an environment variable
3. Register the service conditionally in `Program.cs`
4. Wire it into the worker loop (health transitions or every-cycle)

The `IRconCommandSender` interface for testability, RCON command batching, and the redstone dependency graph are all on the roadmap. The village structures could become the foundation for interactive operations — imagine flipping a Minecraft lever to restart a service.

If you want to contribute, the [GitHub repo](https://github.com/csharpfritz/Aspire-Minecraft) has issues labeled by sprint and team member. The best place to start is the worker services in `src/Aspire.Hosting.Minecraft.Worker/Services/` — each one is a self-contained feature with a clear pattern to follow.

---

*Fritz.Aspire.Hosting.Minecraft is MIT-licensed and available on [NuGet](https://www.nuget.org/packages/Fritz.Aspire.Hosting.Minecraft).*
