# 🎮 Aspire.Hosting.Minecraft

[![NuGet](https://img.shields.io/nuget/v/Fritz.Aspire.Hosting.Minecraft.svg)](https://www.nuget.org/packages/Fritz.Aspire.Hosting.Minecraft) [![NuGet Downloads](https://img.shields.io/nuget/dt/Fritz.Aspire.Hosting.Minecraft.svg)](https://www.nuget.org/packages/Fritz.Aspire.Hosting.Minecraft) [![GitHub Release](https://img.shields.io/github/v/release/csharpfritz/Aspire-Minecraft)](https://github.com/csharpfritz/Aspire-Minecraft/releases/latest) [![Build](https://github.com/csharpfritz/Aspire-Minecraft/actions/workflows/build.yml/badge.svg)](https://github.com/csharpfritz/Aspire-Minecraft/actions/workflows/build.yml) [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A .NET Aspire integration for Minecraft servers— featuring OpenTelemetry instrumentation, BlueMap web maps, and live in-world visualization of your Aspire resources.

![Aspire resources visualized in Minecraft — emerald block structures with health signs, floating hologram dashboard, scoreboard sidebar, and player chat alerts](img/sample-1.png)

## ✨ Features

- **Minecraft Server as an Aspire Resource** — `builder.AddMinecraftServer("minecraft")` with full lifecycle management
- **OpenTelemetry Instrumentation** — JVM metrics (memory, GC, threads) + game metrics (TPS, MSPT, players) in the Aspire dashboard
- **BlueMap Web Map** — Interactive 3D map exposed as a clickable endpoint in the dashboard
- **In-World Aspire Display** — Hologram dashboards, scoreboards, and torch-topped structures showing service health
- **Player Message Audit Trail** — Every system→player message logged as structured OTEL events
- **Boss Bar Health Meter** — Persistent bar showing fleet health percentage (green/yellow/red)
- **Title Screen Alerts** — Full-screen alerts when resources go down or recover
- **Sound Effects** — Audio cues on health state transitions (wither ambient on failure, level-up on recovery)
- **Weather = System Health** — Clear skies when healthy, rain when degraded, thunderstorms when critical
- **Particle Effects** — Smoke/flame on crash, happy villager particles on recovery
- **Action Bar Ticker** — Rotating HUD metrics (TPS, MSPT, healthy count, RCON latency) above the hotbar
- **Beacon Towers** — Per-resource iron-base beacons with green/red stained glass reflecting health
- **Fireworks** — Celebratory fireworks when all resources recover to healthy after a failure
- **Guardian Mobs** — Iron golems guard healthy resources; zombies spawn at unhealthy ones
- **Deployment Fanfare** — Lightning, fireworks, and title announcements when a resource finishes starting
- **World Border Pulse** — World border shrinks with red tint when fleet health is critical, expands back on recovery
- **Heartbeat** — Note block pulse whose tempo reflects fleet health: fast when healthy, slow when degraded, silent when dead
- **Achievements** — Infrastructure milestone awards ("First Service Online", "Full Fleet Healthy", "Survived a Crash")
- **Server Startup Optimization** — Tuned view distance, simulation distance, and world settings for fast container boot

## 🚀 Quick Start

### Prerequisites

- .NET 10.0 SDK
- Docker Desktop
- A Minecraft Java Edition client (for connecting to the server)

### Run the Demo

```bash
cd samples/MinecraftAspireDemo/MinecraftAspireDemo.AppHost
dotnet run
```

This starts:
- A **Paper Minecraft server** (port 25565) with BlueMap and DecentHolograms plugins
- A **sample API service** and **web frontend** as sibling Aspire resources
- A **Redis cache** instance
- A **worker service** that renders Aspire state inside the Minecraft world

### Connect to the Server

1. Open Minecraft Java Edition
2. Add server: `localhost:25565`
3. Join and explore the Aspire dashboard near spawn!

## 📦 Usage in Your Own Project

```csharp
// In your AppHost Program.cs
using Aspire.Hosting.Minecraft;

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("cache");
var api = builder.AddProject<Projects.MyApi>("api");

var mc = builder.AddMinecraftServer("minecraft", gamePort: 25565, rconPort: 25575)
    .WithBlueMap(port: 8100)           // Adds BlueMap web map
    .WithOpenTelemetry()               // Injects OTEL Java agent for JVM telemetry
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    // Sprint 1: Core feedback
    .WithParticleEffects()             // Smoke/flame on crash, happy particles on recovery
    .WithTitleAlerts()                 // Full-screen alerts on resource state changes
    .WithWeatherEffects()              // Weather reflects fleet health
    .WithBossBar()                     // Persistent health bar
    .WithSoundEffects()                // Audio cues on transitions
    // Sprint 2: Atmosphere & delight
    .WithActionBarTicker()             // Rotating HUD metrics
    .WithBeaconTowers()                // Per-resource beacon towers
    .WithFireworks()                   // Celebrate all-green recovery
    .WithGuardianMobs()                // Iron golems / zombies per resource
    .WithDeploymentFanfare()           // Lightning + fireworks on deploy
    // Sprint 3: Showstopper
    .WithWorldBorderPulse()            // World border shrinks on critical health
    .WithHeartbeat()                   // Note block pulse = fleet heartbeat
    .WithAchievements()                // Infrastructure milestone awards
    .WithMonitoredResource(api)        // Each monitored resource gets a cube,
    .WithMonitoredResource(redis);     // hologram line, and scoreboard entry

builder.Build().Run();
```

The worker service is created internally by `WithAspireWorldDisplay` — it appears as a child of the Minecraft resource in the Aspire dashboard. Add as many `.WithMonitoredResource()` calls as you like; each one dynamically gets its own in-world representation.

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
  ├── Minecraft Worker Service (.NET)
  │     ├── RCON connection to server
  │     ├── Game metrics polling → OTEL
  │     ├── Hologram dashboards
  │     ├── Scoreboards
  │     ├── Torch structures per resource
  │     └── Player message audit logging
  │
  └── Your Services (API, Web, Redis, etc.)
        └── Visualized in-world!
```

## 🔧 Configuration

The `AddMinecraftServer` method accepts optional parameters:

```csharp
builder.AddMinecraftServer("minecraft",
    gamePort: 25565,    // Minecraft game port
    rconPort: 25575);   // RCON console port
```

### World Persistence

By default, each `dotnet run` starts with a **fresh Minecraft world** — no leftover structures or state from previous sessions. This is ideal for development and demos.

To keep world data across restarts, opt in with `WithPersistentWorld()`:

```csharp
builder.AddMinecraftServer("minecraft")
    .WithPersistentWorld();   // Named Docker volume persists /data
```

## 📁 Project Structure

```
src/
  Aspire.Hosting.Minecraft/        # Hosting library (NuGet package — includes RCON client)
  Aspire.Hosting.Minecraft.Rcon/   # RCON protocol client library (embedded in hosting package)
  Aspire.Hosting.Minecraft.Worker/ # Worker service for in-world display (separate project, not packaged)
samples/
  MinecraftAspireDemo/             # Demo application
```

## License

MIT
