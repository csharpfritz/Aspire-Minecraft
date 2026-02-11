# 🎮 Aspire.Hosting.Minecraft

[![NuGet](https://img.shields.io/nuget/v/Fritz.Aspire.Hosting.Minecraft.svg)](https://www.nuget.org/packages/Fritz.Aspire.Hosting.Minecraft) [![NuGet Downloads](https://img.shields.io/nuget/dt/Fritz.Aspire.Hosting.Minecraft.svg)](https://www.nuget.org/packages/Fritz.Aspire.Hosting.Minecraft) [![GitHub Release](https://img.shields.io/github/v/release/csharpfritz/Aspire-Minecraft)](https://github.com/csharpfritz/Aspire-Minecraft/releases/latest) [![Build](https://github.com/csharpfritz/Aspire-Minecraft/actions/workflows/build.yml/badge.svg)](https://github.com/csharpfritz/Aspire-Minecraft/actions/workflows/build.yml) [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A .NET Aspire integration for Minecraft servers — featuring OpenTelemetry instrumentation, BlueMap web maps, and live in-world visualization of your distributed system. When your Redis cache goes down, the weather darkens. When it recovers, fireworks light up the sky.

![Resource village in Minecraft with themed structures, beacons, cobblestone paths, fence perimeter, boss bar showing fleet health, and Aspire Status scoreboard](img/sample-1.png)

> 🎉 **v0.3.0:** Sprint 3 complete! The resource village features themed structures (watchtower/warehouse/workshop/cottage), comprehensive cobblestone pathways, redstone dependency graphs, interactive service switches, configurable boss bar, achievements, and a rhythmic heartbeat that reflects fleet health.

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

- **Resource Village** — Each Aspire resource gets a themed building in a 2×N grid layout:
  - **Watchtower** (stone brick, 10 blocks tall) — .NET Projects
  - **Warehouse** (iron block, cargo bay style) — Docker Containers  
  - **Workshop** (oak planks, peaked roof) — Executables
  - **Cottage** (cobblestone, humble dwelling) — Other resources
- **Comprehensive Paths** — Complete cobblestone coverage throughout the village, flush with the ground for seamless walking
- **Beacon Towers** — Per-resource beacons with color-coded stained glass matching Aspire dashboard colors (blue/purple/cyan). Beam turns red when resource fails
- **Redstone Dependency Graph** — Visual L-shaped redstone wires connecting dependent resources, showing your system's dependency architecture
- **Service Switches** — Interactive levers positioned to the right of each building entrance. Lever up + glowing lamp = healthy, lever down + dark lamp = unhealthy (display-only)
- **Fence & Gate** — Oak fence perimeter with a gated south entrance, 4-block clearance from buildings
- **Health Indicators** — Glowstone blocks embedded in front walls turn to redstone lamps when resources become unhealthy

### 📊 Health Monitoring

- **Boss Bar** — Persistent bar at the top showing fleet health percentage (green/yellow/red). Title is configurable via `WithBossBar(title)`, defaults to "Aspire Status"
- **Weather Effects** — Clear skies when all healthy, rain when degraded, thunderstorms when majority down
- **World Border Pulse** — Border shrinks from 200→100 blocks with red tint when >50% of services are unhealthy
- **Particle Effects** — Smoke and flame particles at crashed resources, happy villager particles on recovery
- **Guardian Mobs** — Iron golems protect healthy resources; zombies spawn at unhealthy ones (disable with `WithPeacefulMode()`)

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
- **Persistent World** — `WithPersistentWorld()` uses a named Docker volume to keep world data across restarts (default: ephemeral, fresh world each run)
- **Peaceful Mode** — `WithPeacefulMode()` eliminates hostile mobs (zombies, skeletons, creepers) for distraction-free monitoring
- **BlueMap** — `WithBlueMap()` adds an interactive 3D web map exposed as a clickable "world-map" endpoint in the Aspire dashboard
- **OpenTelemetry** — `WithOpenTelemetry()` injects the OTEL Java agent for automatic JVM metrics (heap, GC, threads, CPU)
- **RCON Debug Logging** — `WithRconDebugLogging()` enables debug-level logging of every command sent to the server, visible in Aspire dashboard logs
- **Startup Optimization** — Tuned view distance (6), simulation distance (4), and disabled mob spawning for fast container boot (~30 seconds)

### 🔧 Using a server.properties File

You can configure the Minecraft server using a standard `server.properties` file instead of individual method calls:

**1. Create a `server.properties` file in your AppHost project:**

```properties
# server.properties
motd=Welcome to Aspire Fleet Monitor
difficulty=easy
gamemode=creative
max-players=20
view-distance=8
simulation-distance=6
pvp=false
spawn-protection=0
enable-command-block=true
level-seed=minecraft
```

**2. Load it in your AppHost:**

```csharp
builder.AddMinecraftServer("minecraft")
    .WithServerPropertiesFile("server.properties")  // Load all properties from file
    .WithPersistentWorld()
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    // Individual properties can override file values
    .WithMaxPlayers(10)  // This overrides the file's max-players=20
    .WithMonitoredResource(api);
```

**Note:** The file is read at build time, and properties become environment variables on the container. Properties set via code (like `WithMaxPlayers()`) will override file values. The file path is relative to the AppHost project directory.

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

## 🔧 Troubleshooting

### World Not Resetting
If you make changes to the worker code that affect world generation (paths, structures, etc.) and need a fresh world:

**Without Persistent World (Default):**
```bash
# Stop AppHost, delete the unnamed Docker volume
docker volume ls | grep minecraft
docker volume rm <volume-id>
```

**With WithPersistentWorld():**
```bash
# Stop AppHost, delete the named volume
docker volume rm minecraft-data
```

The Minecraft server creates its world on first startup and won't regenerate unless the volume is cleared. This is normal Docker behavior for stateful containers.

### Village Not Appearing
- Fly to coordinates **~10, -60, 0** to find the village
- Ensure you've called `.WithMonitoredResource()` on at least one resource
- Check worker logs in Aspire dashboard for RCON errors

### Structures Glitching
If buildings are flickering/rebuilding every 10 seconds, this is a bug (should be fixed in v0.2.1+). Ensure you're on the latest version.

### Performance Issues
- Reduce monitored resource count (<10 recommended)
- Disable BlueMap if only using in-game display
- Check `minecraft.rcon.latency_ms` metric — should be <10ms

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