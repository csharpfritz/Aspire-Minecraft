# Launch: Monitor Your .NET Aspire Infrastructure — In Minecraft

[![NuGet](https://img.shields.io/nuget/v/Fritz.Aspire.Hosting.Minecraft.svg)](https://www.nuget.org/packages/Fritz.Aspire.Hosting.Minecraft)

What if you could *feel* your distributed system? Not stare at a dashboard, but watch the weather darken as your Redis cache crashes. Hear a heartbeat slow down when services fail. See fireworks celebrate when everything recovers.

Today, we're launching **Fritz.Aspire.Hosting.Minecraft** — a NuGet package that connects your .NET Aspire application to a Minecraft server, turning your infrastructure into a living, reactive 3D world.

## Why Minecraft?

Dashboards are passive. They show you numbers, graphs, and status badges. But when you're building distributed systems, you want to *know* when something breaks — viscerally, immediately, without needing to tab over to a browser.

Minecraft gives you that. It's real-time 3D visualization, collaborative monitoring (your whole team can join the server), and honestly, it's just more fun than staring at Grafana at 2 AM.

## What You Get

With five lines of code, you add a Minecraft server to your Aspire AppHost and start monitoring your services in-world:

```csharp
using Aspire.Hosting.Minecraft;

var builder = DistributedApplication.CreateBuilder(args);
var api = builder.AddProject<Projects.MyApi>("api");

builder.AddMinecraftServer("minecraft")
    .WithPersistentWorld()
    .WithBlueMap()
    .WithOpenTelemetry()
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithBossBar()
    .WithWeatherEffects()
    .WithHeartbeat()
    .WithMonitoredResource(api);

builder.Build().Run();
```

Run `dotnet run`, connect with your Minecraft client, and fly to the village. Each Aspire resource gets a themed building — watchtowers for .NET projects, warehouses for Docker containers, workshops for executables. Beacon towers shoot colored beams into the sky, matching the Aspire dashboard palette.

Everything's running smoothly? Clear skies, steady heartbeat, green boss bar at 100%.

Then your API crashes.

The weather darkens. Rain starts falling. A giant red "⚠ SERVICE DOWN" alert fills your screen. The wither's growl echoes. Smoke and flames erupt from the API's watchtower. The beacon turns red. The heartbeat slows to a crawl.

You *feel* it.

## Features That Make You Lean Forward

Every feature is opt-in via a fluent API. Here's what you can enable:

**World Building**
- **Resource village** with themed structures arranged by dependency order
- **Beacon towers** with health-colored beams visible from hundreds of blocks
- **Redstone dependency graphs** showing the flow between services
- **Service switches** — levers on building fronts that flip when services go down

**Health Monitoring**
- **Boss bar** showing fleet health percentage at the top of your screen
- **Weather effects** that shift from clear to rain to thunderstorms as services fail
- **World border pulse** that shrinks with a red tint when >50% of services are down
- **Particle effects** — smoke on failure, happy villager sparkles on recovery

**Audio & Effects**
- **Heartbeat** — a rhythmic note block pulse whose tempo reflects fleet health
- **Sound effects** — wither ambient on crashes, level-up chime on recovery
- **Fireworks** celebrating all-green fleet status after a failure
- **Deployment fanfare** — lightning and fireworks when a resource finishes starting

**Gamification**
- **Achievements** for infrastructure milestones: "First Blood" (first failure), "Clean Sweep" (all healthy), "Night Shift" (monitoring at night)
- **Title alerts** with full-screen notifications
- **Action bar ticker** cycling through live metrics (TPS, MSPT, healthy count, RCON latency)

## Zero Overhead, Maximum Control

Every feature is backed by an environment variable. If you don't call `.WithWeatherEffects()`, no weather commands are sent. The worker service only registers enabled features, giving you zero overhead for disabled functionality.

The Minecraft server runs in Docker using the `itzg/minecraft-server` image with Paper, BlueMap (3D web map), and OpenTelemetry instrumentation. The worker connects via RCON — the Remote Console protocol — and polls your resources every 10 seconds. HTTP health checks for web services, TCP socket checks for databases and caches.

Want to dive deeper? Read our [Behind the Build](behind-the-build.md) architecture post or check out the [conference demo guide](conference-demo-guide.md).

## What's Coming

This is v0.1.0. We're already working on Azure integration — imagine a citadel rising next to your village, visualizing your Azure Resource Groups alongside your local Aspire resources. Resource Groups as districts, VMs and App Services as buildings, the whole thing queryable via ARM and rendered in blocks.

The village structures could become interactive — flip a Minecraft lever to restart a service, redstone circuits that trigger alerts, achievements that track operational milestones.

## Try It Now

Install the package:

```bash
dotnet add package Fritz.Aspire.Hosting.Minecraft
```

Run the sample:

```bash
git clone https://github.com/csharpfritz/Aspire-Minecraft.git
cd Aspire-Minecraft/samples/GrandVillageDemo/GrandVillageDemo.AppHost
dotnet run
```

Connect your Minecraft client to `localhost:25565` and fly to coordinates `~10, -60, 0` to find the village.

**Explore:**
- 📦 [NuGet Package](https://www.nuget.org/packages/Fritz.Aspire.Hosting.Minecraft)
- 💻 [GitHub Repository](https://github.com/csharpfritz/Aspire-Minecraft)
- 📖 [Behind the Build](behind-the-build.md)

---

*Fritz.Aspire.Hosting.Minecraft is MIT-licensed and available today. Your distributed system just got a lot more interesting.*
