# Sprint 3.1: Your Village Actually Shows Up Now

> **Author:** Jeffrey T. Fritz
> **Date:** February 2026
> **Package:** [Fritz.Aspire.Hosting.Minecraft](https://www.nuget.org/packages/Fritz.Aspire.Hosting.Minecraft)
> **Repo:** [csharpfritz/Aspire-Minecraft](https://github.com/csharpfritz/Aspire-Minecraft)

---

Sprint 3 shipped the resource village — themed buildings, redstone dependency graphs, service switches, heartbeat pulses, the works. It was the showstopper sprint. One problem: the village didn't always show up.

**Sprint 3.1 fixes that.** This is the "it actually works every time now" release. No new features, just the kind of quality work that turns a demo into a product.

![Resource village with watchtower structures, glowstone health indicators, boss bar, and Aspire Status scoreboard](../img/glow-block.png)

---

## The Big Fix: Chunk Force-Loading

Here's a fun Minecraft fact most server admins know but RCON developers learn the hard way: **block placement commands silently fail on unloaded chunks.**

In Minecraft, the world is divided into 16×16 block regions called chunks. Chunks only load when a player is nearby. If you send an RCON `setblock` or `fill` command to a chunk that no player has visited? The server says "OK" and does nothing. No error. No warning. Just... nothing.

This was the root cause of the "invisible village" bug. On a fresh server start — before any player joins — every RCON command to build structures was hitting unloaded chunks. The worker would happily report "village built!" while the world remained an empty flat plain.

The fix is one line:

```
forceload add -10 -10 80 80
```

This tells the Minecraft server to keep a generous area around the village permanently loaded, regardless of player proximity. The worker now runs this command immediately after RCON connects, before any structure placement begins.

**Result:** The village appears reliably on every startup, even if no player has joined yet. No more "fly around and hope it renders" debugging.

---

## Dynamic Terrain Detection

Sprint 3 hardcoded `Y=-60` for structure placement — the grass surface level on a superflat world. This meant the village would build underground on normal worlds, in mid-air on custom worlds, and basically anywhere *except* where it should be on non-superflat terrain.

Sprint 3.1 introduces `TerrainProbeService`, which runs a binary search at startup to find the actual ground level. It works by probing with `setblock X Y Z yellow_wool keep`:

- **"Changed the block"** → That position was air. Clean up the wool and keep searching lower.
- **"Could not set the block"** → Something solid is there. The surface is at or above this Y.

The binary search covers Y=100 down to Y=-64 and finds the surface in **~8 RCON commands**. Once detected, `VillageLayout.SurfaceY` is set and every service uses it.

Structures now build at `SurfaceY + 1` — sitting *on top* of the terrain instead of replacing the grass block. Fences, paths, and buildings are all properly elevated.

---

## What Else Shipped

### Superflat World Fix

Paper servers have a quirk: setting `LEVEL_TYPE=flat` alone isn't enough. Without `GENERATOR_SETTINGS=""` explicitly set, Paper ignores the flat world type. One environment variable, hours of debugging.

### Health Indicator Alignment

The glowstone health lamps on each building front wall had a positioning bug. On watchtower and warehouse structures (which have 3-block-tall doors), the lamp sat at `y+3` — right where the door frame is. Now:

- **Watchtower & Warehouse** → lamp at `y+4` (above the 3-tall door)
- **Cottage & Workshop** → lamp at `y+3` (above the 2-tall door)

Removed a duplicate glowstone placement in `ServiceSwitchService` that was redundant with the `StructureBuilder` health indicator.

### BlueMap "World Map" Link

The Aspire dashboard used to show the raw BlueMap URL as the clickable link text. Now it shows **"World Map"** — a small polish that makes the dashboard cleaner.

### Aspire SDK Update

Bumped from .NET Aspire SDK 13.1.0 to 13.1.1.

---

## The Full API

Here's the complete fluent API with every Sprint 3 and 3.1 feature enabled — this is copy-paste ready:

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
    .WithBossBar("Minecraft Demo")
    .WithWeatherEffects()
    .WithWorldBorderPulse()
    .WithParticleEffects()
    .WithGuardianMobs()
    .WithBeaconTowers()
    .WithServiceSwitches()
    .WithRedstoneDependencyGraph()
    // Audio & effects
    .WithHeartbeat()
    .WithSoundEffects()
    .WithFireworks()
    .WithDeploymentFanfare()
    // Gamification
    .WithAchievements()
    .WithTitleAlerts()
    .WithActionBarTicker()
    // Monitored resources
    .WithMonitoredResource(api)
    .WithMonitoredResource(web)
    .WithMonitoredResource(redis)
    .WithMonitoredResource(pg);

builder.Build().Run();
```

The terrain detection and chunk force-loading happen automatically — no configuration needed. Just run it and the village appears.

---

## Sprint 3 Features (In Case You Missed Them)

Sprint 3.1 builds on the Sprint 3 features that are already live:

- **`WithPersistentWorld()`** — Docker volume persistence across restarts
- **`WithWorldBorderPulse()`** — Border shrinks with red tint when services fail
- **`WithHeartbeat()`** — Rhythmic note block pulse reflecting fleet health
- **`WithAchievements()`** — Infrastructure milestones as in-game achievements
- **`WithServiceSwitches()`** — Lever + lamp health indicators per building
- **`WithRedstoneDependencyGraph()`** — Redstone wires showing service dependencies
- **RCON rate limiting** — Token bucket with command priority to prevent server overload

---

## What's Next

Azure resource group support is on the workbench — a separate `Fritz.Aspire.Hosting.Minecraft.Azure` package that elevates cloud resources into the village as a citadel structure. Think of it as "The Pan" — zooming out from the local village to the cloud fortress on the horizon.

We're also looking at the conference demo circuit. If you're presenting .NET Aspire and want an audience to *lean forward*, a Minecraft world reacting to your live deployment failures is how you do it.

---

**Install it:**

```shell
dotnet add package Fritz.Aspire.Hosting.Minecraft
```

- 📦 [NuGet](https://www.nuget.org/packages/Fritz.Aspire.Hosting.Minecraft)
- 🐙 [GitHub](https://github.com/csharpfritz/Aspire-Minecraft)
- 📖 [Behind the Build (Architecture Deep-Dive)](behind-the-build.md)

> *Break some services. Watch the village react. This time, the village will actually be there.*
