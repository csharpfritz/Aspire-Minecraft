# Aspire.Hosting.Minecraft User Documentation

Welcome to the **Fritz.Aspire.Hosting.Minecraft** user documentation. This integration brings your .NET Aspire distributed application to life in Minecraft, visualizing resources as a village where each service becomes a themed building.

## 📚 Documentation Structure

- **[Getting Started](getting-started.md)** — Quick start guide, installation, and first steps
- **[Configuration](configuration.md)** — Complete reference for all extension methods and options
- **[Examples](examples.md)** — Real-world usage patterns and complete code samples
- **[Troubleshooting](troubleshooting.md)** — Common issues and solutions

### Feature Guides

Detailed guides for each feature:

- **[Village Visualization](features/village-visualization.md)** — Resource village with themed buildings
- **[Health Monitoring](features/health-monitoring.md)** — Visual health indicators (lamps, beacons, particles)
- **[Service Switches](features/service-switches.md)** — Interactive levers showing service state
- **[Redstone Dependencies](features/redstone-dependencies.md)** — Visual dependency graph with redstone circuits
- **[Achievements](features/achievements.md)** — Infrastructure milestone achievements
- **[Heartbeat](features/heartbeat.md)** — Rhythmic audio pulse reflecting fleet health
- **[Boss Bar](features/boss-bar.md)** — Persistent health percentage bar
- **[World Border Pulse](features/world-border-pulse.md)** — Border shrinks on critical failures
- **[RCON Debug Logging](features/rcon-debug.md)** — Debug logging for troubleshooting

## 🎯 What is This?

This integration adds a Minecraft Paper server to your .NET Aspire application with:

- **In-world visualization** of your distributed system as a village
- **Real-time health monitoring** through visual and audio effects
- **OpenTelemetry instrumentation** for JVM and game metrics
- **BlueMap web interface** for 3D world viewing
- **Zero configuration required** — works out of the box

## 🚀 Quick Start

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.MyApi>("api");

builder.AddMinecraftServer("minecraft")
    .WithPersistentWorld()
    .WithBlueMap()
    .WithOpenTelemetry()
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithBossBar()
    .WithWeatherEffects()
    .WithMonitoredResource(api);

builder.Build().Run();
```

Connect with Minecraft Java Edition to `localhost:25565` and fly to coordinates `~10, -60, 0`.

## 📖 Key Concepts

### Resource Village

Each Aspire resource becomes a building in a 2-column village grid:
- **Watchtower** (stone brick, tall) — .NET Projects
- **Warehouse** (iron block, cargo bay) — Docker Containers
- **Workshop** (oak planks, peaked roof) — Executables
- **Cottage** (cobblestone, humble) — Other resources

### Health Visualization

Resources display their health through multiple mechanisms:
- **Glowstone lamps** turn red when unhealthy
- **Beacon beams** change color (green → red)
- **Levers and redstone lamps** reflect service state
- **Weather** changes with fleet health

### Opt-In Features

Every feature is opt-in. If you don't call a `With*()` method, that feature doesn't run. Zero overhead for disabled features.

## 🔧 Prerequisites

- .NET 10.0 SDK
- Docker Desktop
- Minecraft Java Edition client

## 📦 Installation

```bash
dotnet add package Fritz.Aspire.Hosting.Minecraft
```

See [Getting Started](getting-started.md) for detailed installation instructions.

## 🎮 In-Game Experience

- **Clear skies** when all services healthy
- **Rain** when some services degraded
- **Thunderstorms** when majority down
- **Fireworks** when all services recover
- **Title alerts** on resource state changes
- **Sound effects** for failures and recoveries

## 🔗 Additional Resources

- [GitHub Repository](https://github.com/csharpfritz/Aspire-Minecraft)
- [NuGet Package](https://www.nuget.org/packages/Fritz.Aspire.Hosting.Minecraft)
- [Main README](../README.md) — Project overview and architecture
- [API Surface Documentation](../docs/api-surface.md) — Complete API reference

## 💡 Getting Help

If you encounter issues:
1. Check the [Troubleshooting](troubleshooting.md) guide
2. Review the [Examples](examples.md) for working code samples
3. [Open an issue](https://github.com/csharpfritz/Aspire-Minecraft/issues) on GitHub
