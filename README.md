# 🎮 Aspire.Hosting.Minecraft

[![NuGet](https://img.shields.io/nuget/v/Fritz.Aspire.Hosting.Minecraft.svg)](https://www.nuget.org/packages/Fritz.Aspire.Hosting.Minecraft) [![NuGet Downloads](https://img.shields.io/nuget/dt/Fritz.Aspire.Hosting.Minecraft.svg)](https://www.nuget.org/packages/Fritz.Aspire.Hosting.Minecraft) [![GitHub Release](https://img.shields.io/github/v/release/csharpfritz/Aspire-Minecraft)](https://github.com/csharpfritz/Aspire-Minecraft/releases/latest) [![Build](https://github.com/csharpfritz/Aspire-Minecraft/actions/workflows/build.yml/badge.svg)](https://github.com/csharpfritz/Aspire-Minecraft/actions/workflows/build.yml) [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A .NET Aspire integration for Minecraft servers — featuring OpenTelemetry instrumentation, BlueMap web maps, and live in-world visualization of your distributed system. When your Redis cache goes down, the weather darkens. When it recovers, fireworks light up the sky.

![Aspire resources visualized in Minecraft — emerald block structures with health signs, floating hologram dashboard, scoreboard sidebar, and player chat alerts](img/sample-1.png)

> 📸 **Sprint 3 Update:** The resource village now features themed structures (watchtower, warehouse, workshop, cottage), redstone dependency graphs, service switches, achievements, and a heartbeat pulse! Screenshots coming soon.

## 🚀 Quick Start

### Prerequisites

- .NET 10.0 SDK
- Docker Desktop
- Minecraft Java Edition client

### Minimal Setup

```csharp
// In your AppHost Program.cs
using Aspire.Hosting.Minecraft;

var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.MyApi>("api");

builder.AddMinecraftServer("minecraft")
    .WithPersistentWorld()
    .WithPeacefulMode()
    .WithBlueMap()
    .WithOpenTelemetry()
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithBossBar()
    .WithWeatherEffects()
    .WithHeartbeat()
    .WithAchievements()
    .WithMonitoredResource(api);

builder.Build().Run();
```

That's it — a Minecraft server appears in the Aspire dashboard with a boss bar tracking your API's health and weather that reflects system state.

### Run the Demo

```bash
cd samples/MinecraftAspireDemo/MinecraftAspireDemo.AppHost
dotnet run
```

This starts a Paper Minecraft server, a sample API + web frontend, Redis, Postgres, and a worker service that renders Aspire state inside the Minecraft world.

### Connect

1. Open Minecraft Java Edition
2. Add server: `localhost:25565`
3. Fly to coordinates ~10, -60, 0 to find the resource village

## ✨ Features

### 🏗️ World Building

- **Resource Village** — Each Aspire resource gets a themed building: Watchtower (projects), Warehouse (containers), Workshop (executables), Cottage (other). Arranged in a 2×N grid with cobblestone pathways and oak fence perimeter
- **Beacon Towers** — Per-resource beacons with stained glass matching the Aspire dashboard color palette (blue=project, purple=container, cyan=executable). Beam turns red on failure
- **Redstone Dependency Graph** — Visual redstone wires connecting dependent resources, showing the flow of your system architecture
- **Service Switches** — Levers on building fronts that reflect current resource state (up=healthy, down=unhealthy)
- **Fence & Gate** — Oak fence perimeter around the village with a gated entrance
- **Cobblestone Paths** — Boulevard between structure columns with cross-paths to each building

### 📊 Health Monitoring

- **Boss Bar** — Persistent bar at the top of the screen showing fleet health as a percentage (green/yellow/red)
- **Weather Effects** — Clear skies when all healthy, rain when degraded, thunderstorms when majority down
- **World Border Pulse** — Border shrinks from 200→100 blocks with red tint when >50% of services are down
- **Particle Effects** — Smoke and flame at crashed resources, happy villager particles on recovery
- **Guardian Mobs** — Iron golems protect healthy resources; zombies spawn at unhealthy ones

### 🔊 Audio & Effects

- **Heartbeat** — Rhythmic note block pulse whose tempo and pitch reflect fleet health. Fast and high when healthy, slow and low when degraded, flatline silence at 0%
- **Sound Effects** — Wither ambient on service failure, level-up chime on recovery
- **Fireworks** — Celebratory fireworks when all resources recover to healthy after a failure
- **Deployment Fanfare** — Lightning bolt, fireworks, and title announcement when a resource finishes starting

### 🎮 Gamification

- **Achievements** — Infrastructure milestones as in-game achievements: "First Blood" (first resource unhealthy), "Clean Sweep" (all resources healthy), "Night Shift" (monitoring at night), "The Village" (village built)
- **Title Alerts** — Full-screen "⚠ SERVICE DOWN" (red) and "✅ BACK ONLINE" (green) on health transitions
- **Action Bar Ticker** — Rotating HUD metrics above the hotbar: TPS, MSPT, healthy count, RCON latency

### ⚙️ Configuration

- **Server Properties** — `WithServerProperty()`, `WithGameMode()`, `WithDifficulty()`, `WithMaxPlayers()`, `WithMotd()`, `WithWorldSeed()`, `WithPvp()` — all Minecraft `server.properties` values via a fluent API or `WithServerPropertiesFile()` for bulk loading
- **Persistent World** — `WithPersistentWorld()` uses a named Docker volume to keep world data across restarts (default: fresh world each run)
- **Peaceful Mode** — `WithPeacefulMode()` eliminates hostile mobs for distraction-free monitoring
- **BlueMap** — `WithBlueMap()` adds an interactive 3D web map exposed as a clickable endpoint in the Aspire dashboard
- **OpenTelemetry** — `WithOpenTelemetry()` injects the OTEL Java agent for automatic JVM metrics (heap, GC, threads, CPU)
- **Startup Optimization** — Tuned view distance (6), simulation distance (4), and disabled mob spawning for fast container boot

## 📦 Full Feature Demo

All features enabled — this is what the sample AppHost uses:

```csharp
using Aspire.Hosting.Minecraft;

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("cache");
var pg = builder.AddPostgres("db-host");
var db = pg.AddDatabase("db");
var api = builder.AddProject<Projects.MyApi>("api")
    .WithReference(redis);
var web = builder.AddProject<Projects.MyWeb>("web")
    .WithReference(api)
    .WithExternalHttpEndpoints();

var minecraft = builder.AddMinecraftServer("minecraft", gamePort: 25565, rconPort: 25575)
    .WithMaxPlayers(10)
    .WithMotd("Aspire Fleet Monitor")
    .WithPersistentWorld()
    .WithPeacefulMode()
    .WithBlueMap(port: 8100)
    .WithOpenTelemetry()
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    // Health monitoring
    .WithBossBar()
    .WithWeatherEffects()
    .WithWorldBorderPulse()
    .WithParticleEffects()
    .WithGuardianMobs()
    .WithBeaconTowers()
    .WithServiceSwitches()
    .WithRedstoneGraph()
    // Audio & effects
    .WithHeartbeat()
    .WithSoundEffects()
    .WithFireworks()
    .WithDeploymentFanfare()
    // Gamification
    .WithAchievements()
    .WithTitleAlerts()
    .WithActionBarTicker()
    // Resources to monitor
    .WithMonitoredResource(api)
    .WithMonitoredResource(web)
    .WithMonitoredResource(redis)
    .WithMonitoredResource(pg);

builder.Build().Run();
```

The worker service is created internally by `WithAspireWorldDisplay` — it appears as a child of the Minecraft resource in the Aspire dashboard. Every feature is opt-in: if you don't call `.WithWeatherEffects()`, no weather commands are sent. Zero overhead for disabled features.

## 📊 Telemetry

### Automatic JVM Metrics (via OTEL Java Agent)

| Metric | Description |
|--------|-------------|
| `jvm.memory.used` | Heap/non-heap memory by pool |
| `jvm.gc.collections.count` | GC invocations per collector |
| `jvm.gc.collections.duration` | Time spent in GC pauses |
| `jvm.threads.live` | Current thread count |
| `process.cpu.utilization` | JVM CPU usage |

### Game Metrics (via RCON polling)

| Metric | Description |
|--------|-------------|
| `minecraft.tps` | Server ticks per second (target: 20.0) |
| `minecraft.mspt` | Milliseconds per tick |
| `minecraft.players.online` | Current player count |
| `minecraft.players.max` | Max player slots |
| `minecraft.worlds.loaded` | Number of loaded worlds |
| `minecraft.rcon.latency_ms` | RCON round-trip time |
| `minecraft.player_messages.sent` | System messages sent to players |

### Structured Logs

All system→player messages are logged with rich context:
```
[INF] Player message sent: {MessageType=ResourceHealthAlert, ResourceName=redis, Trigger=HealthChanged}
```

## 🏗️ Architecture

```
AppHost
  ├── Minecraft Server (Docker: itzg/minecraft-server)
  │     ├── Paper Server + BlueMap + DecentHolograms
  │     ├── OTEL Java Agent → Aspire Dashboard
  │     └── Ports: 25565 (game), 8100 (map), 25575 (RCON)
  │
  ├── Minecraft Worker Service (.NET BackgroundService)
  │     ├── RCON connection to server
  │     ├── Health polling (HTTP + TCP) of sibling resources
  │     ├── In-world rendering (structures, holograms, scoreboards)
  │     ├── Fleet-wide effects (weather, boss bar, heartbeat)
  │     ├── Event-driven feedback (particles, sounds, fireworks)
  │     └── Game metrics → OpenTelemetry
  │
  └── Your Services (API, Web, Redis, Postgres, etc.)
        └── Monitored and visualized in-world!
```

For a deep-dive into the architecture, see [Behind the Build](docs/blog/behind-the-build.md).

## 📁 Project Structure

```
src/
  Aspire.Hosting.Minecraft/        # Hosting library (NuGet package — includes RCON client)
  Aspire.Hosting.Minecraft.Rcon/   # RCON protocol client library (embedded in hosting package)
  Aspire.Hosting.Minecraft.Worker/ # Worker service for in-world display (separate project, not packaged)
samples/
  MinecraftAspireDemo/             # Demo application with all features enabled
docs/
  blog/                            # Blog posts and demo guides
```

## License

MIT