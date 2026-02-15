# What If Your Distributed System Lived Inside Minecraft?

> **Author:** Jeffrey T. Fritz  
> **Date:** February 2026  
> **Package:** [Fritz.Aspire.Hosting.Minecraft](https://www.nuget.org/packages/Fritz.Aspire.Hosting.Minecraft)  
> **Repo:** [csharpfritz/Aspire-Minecraft](https://github.com/csharpfritz/Aspire-Minecraft)

---

Picture this: you're standing in a Minecraft village. Not just any village — *your* village. Each building represents a service in your distributed system. A stone brick watchtower for your .NET API. An iron warehouse for your Redis container. A smooth stone cylinder for your Postgres database. A boss bar stretches across the top of your screen, glowing green: **100% Healthy**.

Then someone pushes a bad config. Your API goes down.

The sky darkens. Thunder rolls in. A full-screen red alert flashes: **⚠ API DOWN**. The wither's growl echoes through the world. Smoke billows from the watchtower. The heartbeat slows. The world border contracts with a menacing red tint. A zombie spawns at the base of the tower. The redstone dependency graph goes dark.

You fix the config. The service recovers. The sun comes out. Fireworks explode. A level-up chime plays. An achievement pops: **"Clean Sweep — All resources healthy."** The village is alive again.

**This is real.** It's a NuGet package called `Fritz.Aspire.Hosting.Minecraft`. It ships today. And it turns .NET Aspire into the most dramatic monitoring system you've ever seen.

![Resource village in Minecraft with themed structures, beacons, cobblestone paths, fence perimeter, boss bar showing fleet health, and Aspire Status scoreboard](../img/glow-block.png)

---

## Why Minecraft?

Here's the thing about observability tools: they're powerful, but they're flat. Grafana dashboards are great for post-incident analysis. Application Insights is indispensable for production. But neither of them makes your heart rate spike when a service goes down.

Minecraft does.

- **238 million people** know how to walk around a Minecraft world. Your monitoring just became approachable to everyone in the room.
- **.NET Aspire** already models your distributed app as composable resources — this integration extends that model into a living 3D world.
- **Conference demos** will never be the same. I've done hundreds of live coding sessions. Nothing — *nothing* — gets an audience to lean forward like a Minecraft world reacting to code in real time.
- **Learning tool:** junior developers can *see* what "a service went unhealthy" looks like, sounds like, and feels like. It's not an abstract concept anymore.

This isn't a toy. It uses the same RCON protocol that Minecraft server admins use in production. It exports real OpenTelemetry metrics. It runs in Docker alongside your actual services. It just happens to also be a game.

---

## Quick Start: Zero to Minecraft Dashboard

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- Minecraft Java Edition (to connect and see the world)

### 1. Install the package

```shell
dotnet add package Fritz.Aspire.Hosting.Minecraft
```

### 2. Add it to your AppHost `Program.cs`

```csharp
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
    .WithMonitoredResource(api);

builder.Build().Run();
```

That's it. A Minecraft server appears in the Aspire dashboard, a boss bar tracks your API's health, and the weather reflects your system state. Every `.With*()` method is opt-in — start minimal, layer on drama as you like.

### 3. Run and connect

```shell
dotnet run
```

Open Minecraft Java Edition → Add Server → `localhost:25565` → join and explore.

> **Tip:** The first launch downloads the Paper server image (~1 GB) and generates the world. Give it 60–90 seconds on the first boot.

---

## The Full Feature Tour

Here's everything the integration can do. Four sprints of work, one village of features.

### 🏗️ World Building — Your Architecture, in Blocks

Every Aspire resource you monitor becomes a physical building in the Minecraft world, laid out in a tidy 2×N grid village with cobblestone paths, an oak fence perimeter, and a gated entrance.

The building type is chosen automatically based on your resource type:

| Building | Material | Assigned To |
|---|---|---|
| **Watchtower** | Stone brick, 10 blocks tall | .NET Projects |
| **Warehouse** | Iron block, cargo bay style | Docker Containers |
| **Workshop** | Oak planks, peaked roof + chimney | Executables |
| **Cylinder** | Smooth stone, domed roof | Databases (Postgres, Redis, SQL Server, MongoDB…) |
| **Azure-Themed** | Light blue concrete, blue glass roof + banner | Azure resources (Service Bus, Key Vault…) |
| **Cottage** | Cobblestone, humble dwelling | Everything else |

Azure resources that aren't databases get a distinctive blue-themed building with a rooftop banner. Azure resources that *are* databases get the cylinder shape — because even in Minecraft, a database should look like a database.

Each building also gets:

- **Beacon towers** with color-coded stained glass matching the Aspire dashboard palette. The beam turns red when the resource fails — visible from anywhere in the world.
- **Service switches** — levers next to each entrance that flip up/down based on health status, with a glowstone lamp that lights or darkens.
- **Redstone dependency graph** — L-shaped redstone wires connecting dependent resources, showing your system's architecture as a physical circuit.
- **Redstone dashboard** — a wall west of the village showing scrolling health history using redstone lamps. Each row is a resource, each column is a time slot. Lit = healthy, dark = unhealthy. It's a time-series chart made of blocks.

```csharp
.WithBeaconTowers()
.WithServiceSwitches()
.WithRedstoneDependencyGraph()
.WithRedstoneDashboard()
```

### 📊 Health Monitoring — Feel Your Fleet

The real magic is how the world *reacts* to your system's health:

- **Boss Bar** — A persistent bar at the top of the screen showing fleet health as a percentage. Green (≥75%), yellow (25–74%), red (<25%). The title is configurable: `.WithBossBar("My Fleet")`.
- **Weather Effects** — Clear skies when everything's healthy. Rain when services degrade. Thunderstorms when the majority is down. Weather only changes on state transitions — the sky stays stable, not flickering.
- **World Border Pulse** — When more than half your services are unhealthy, the world border contracts from 200 to 100 blocks with a menacing red tint. Fix your services and the border relaxes.
- **Particle Effects** — Smoke and flame billow from crashed resources. Happy villager particles dance above recovered ones.
- **Guardian Mobs** — Iron golems guard healthy resources. Zombies spawn at unhealthy ones. Fix the service to despawn the threat. (Use `.WithPeacefulMode()` if you prefer a calmer experience.)

```csharp
.WithBossBar("Aspire Status")
.WithWeatherEffects()
.WithWorldBorderPulse()
.WithParticleEffects()
.WithGuardianMobs()
```

### 🔊 Audio & Effects — Hear Your System

Distributed systems are usually silent. Not anymore:

- **Heartbeat** — A rhythmic note block pulse whose tempo and pitch reflect fleet health. Fast and high when everything's green. Slow and low when degraded. Flatline silence at 0%.
- **Sound Effects** — The wither's ambient growl when a service fails. A level-up chime when it recovers.
- **Fireworks** — All services recover to green after a failure? The sky lights up with celebratory fireworks.
- **Deployment Fanfare** — A lightning bolt, fireworks, and a title announcement when a resource finishes starting up. Every deployment gets the entrance it deserves.

```csharp
.WithHeartbeat()
.WithSoundEffects()
.WithFireworks()
.WithDeploymentFanfare()
```

### 🎮 Gamification — Achievements Unlocked

Because infrastructure milestones should feel like progress:

- **Achievements** — In-game advancement popups for infrastructure events:
  - 🩸 *First Blood* — first resource goes unhealthy
  - 🧹 *Clean Sweep* — all resources back to healthy
  - 🌙 *Night Shift* — monitoring at night
  - 🏘️ *The Village* — village construction complete
- **Title Alerts** — Full-screen "⚠ SERVICE DOWN" (red) and "✅ BACK ONLINE" (green) on health transitions. Impossible to miss. That's the point.
- **Action Bar Ticker** — Rotating HUD metrics above the hotbar: TPS, milliseconds per tick, healthy resource count, RCON latency.

```csharp
.WithAchievements()
.WithTitleAlerts()
.WithActionBarTicker()
```

### ⚙️ Configuration — Make It Yours

The Minecraft server itself is fully configurable through the fluent API:

```csharp
builder.AddMinecraftServer("minecraft")
    // Server settings
    .WithMaxPlayers(10)
    .WithMotd("Aspire Fleet Monitor")
    .WithGameMode(GameMode.Creative)
    .WithDifficulty(Difficulty.Peaceful)
    .WithWorldSeed("my-seed")
    .WithPvp(false)
    
    // Or load from a file
    .WithServerPropertiesFile("server.properties")
    
    // World persistence
    .WithPersistentWorld()    // Keep world across restarts
    .WithPeacefulMode()      // No hostile mobs
    
    // Integrations
    .WithBlueMap(port: 8100)  // 3D web map in the Aspire dashboard
    .WithOpenTelemetry()      // JVM metrics flowing into Aspire
```

`.WithBlueMap()` deserves a special mention — it adds a full 3D interactive web map that appears as a clickable endpoint in the Aspire dashboard. Fly around your village from your browser.

`.WithOpenTelemetry()` injects the OTEL Java agent into the Minecraft server container, so JVM metrics (heap, GC, threads, CPU) flow into the Aspire dashboard alongside your .NET services. Game metrics like TPS and MSPT are polled via RCON and exported as custom metrics.

---

## The Full Demo: Everything Enabled

Here's the complete sample AppHost with every feature turned on:

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
    .WithAllFeatures()  // Enables every opt-in feature at once
    .WithMonitoredResource(api)
    .WithMonitoredResource(web)
    .WithMonitoredResource(redis)
    .WithMonitoredResource(pg);

builder.Build().Run();
```

Don't want to list every feature? `.WithAllFeatures()` enables everything in a single call — boss bar, weather, heartbeat, fireworks, achievements, redstone graphs, the dashboard, guardian mobs, the works. Perfect for demos.

---

## Under the Hood

The integration is built on three components:

**1. Minecraft Server Container** — `AddMinecraftServer()` spins up a [Paper](https://papermc.io/) server via the trusted [`itzg/minecraft-server`](https://hub.docker.com/r/itzg/minecraft-server) Docker image. It appears in the Aspire dashboard like any other resource, with game, RCON, and BlueMap endpoints. Other services can `.WaitFor()` it, just like a database.

**2. Worker Service** — `WithAspireWorldDisplay<TWorker>()` registers a .NET `BackgroundService` as a child resource of the Minecraft server. This worker:
- Connects to the server via **RCON** (Remote Console) — the same protocol production Minecraft admins use
- Discovers monitored resources from environment variables injected by `.WithMonitoredResource()`
- Polls resource health via HTTP and TCP endpoints
- Translates health state into RCON commands (`/bossbar`, `/weather`, `/title`, `/particle`, `/playsound`, `/setblock`)
- Builds the village, places redstone, manages beacons, spawns mobs — all through RCON

**3. Feature Flags** — Each `.With*()` method sets an environment variable on the worker. The worker checks these at startup and only activates the requested features. If you don't call `.WithWeatherEffects()`, zero weather commands are sent. No overhead for disabled features.

State tracking prevents command spam — the worker remembers the last weather state, boss bar value, and per-resource health, only sending RCON commands when something actually changes. A token bucket rate limiter prevents overwhelming the server during cascading failures.

---

## What's Next: Grand Village

Sprint 5 is on the workbench, and it's ambitious: the **Grand Village**. Think larger, walk-in buildings with interior details. Ornate towers with spiral staircases. Minecart rails connecting resources to show data flow. The village becomes a place you want to explore, not just observe from above.

And Azure resource group support is coming in a separate `Fritz.Aspire.Hosting.Minecraft.Azure` package — elevating cloud resources into the village as a citadel structure. Picture a fortress on the horizon representing your Azure infrastructure, connected to the local village by a bridge. We call it "The Pan" — the conference demo moment when you zoom out from your local services to the cloud.

---

## Try It

```shell
dotnet add package Fritz.Aspire.Hosting.Minecraft
```

Or clone the repo and run the demo:

```shell
git clone https://github.com/csharpfritz/Aspire-Minecraft.git
cd Aspire-Minecraft/samples/MinecraftAspireDemo/MinecraftAspireDemo.AppHost
dotnet run
```

Open Minecraft, connect to `localhost:25565`, and fly to your village.

- 📦 **NuGet:** [Fritz.Aspire.Hosting.Minecraft](https://www.nuget.org/packages/Fritz.Aspire.Hosting.Minecraft)
- 🐙 **GitHub:** [csharpfritz/Aspire-Minecraft](https://github.com/csharpfritz/Aspire-Minecraft)
- 📖 **Architecture Deep-Dive:** [Behind the Build](behind-the-build.md)
- 📄 **License:** MIT — use it, fork it, extend it

This project is open source and thrives on creative contributions. Found a bug? Have a wild idea for an in-world feature? [Open an issue](https://github.com/csharpfritz/Aspire-Minecraft/issues). Want to add a new building type or monitoring effect? PRs are welcome.

If you're giving a conference talk about .NET Aspire, distributed systems, or observability — this is how you make the audience gasp. I've been there. It works.

> _Break some services. Watch the village react. Your distributed system has never been this alive._
