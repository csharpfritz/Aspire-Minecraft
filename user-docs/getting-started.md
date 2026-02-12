# Getting Started

This guide walks you through installing and running your first Minecraft-visualized Aspire application.

## Prerequisites

Before you begin, ensure you have:

- **.NET 10.0 SDK** or later ([download](https://dotnet.microsoft.com/download))
- **Docker Desktop** ([download](https://www.docker.com/products/docker-desktop))
- **Minecraft Java Edition** client ([buy](https://www.minecraft.net/store/minecraft-java-bedrock-edition-pc))

## Installation

### 1. Add the NuGet Package

Add the package to your Aspire AppHost project:

```bash
cd YourProject.AppHost
dotnet add package Fritz.Aspire.Hosting.Minecraft
```

### 2. Basic Setup

Open your `Program.cs` and add the Minecraft server:

```csharp
using Aspire.Hosting.Minecraft;

var builder = DistributedApplication.CreateBuilder(args);

// Add your services
var api = builder.AddProject<Projects.MyApi>("api");

// Add Minecraft server with minimal configuration
builder.AddMinecraftServer("minecraft")
    .WithPersistentWorld()
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithMonitoredResource(api);

builder.Build().Run();
```

**Note:** You need the worker project reference. See [Using the Worker Service](#using-the-worker-service) below.

### 3. Using the Worker Service

The in-world visualization requires a worker service. The easiest approach is to reference the bundled worker:

```xml
<!-- In your AppHost.csproj -->
<ItemGroup>
  <ProjectReference Include="..\path\to\Aspire.Hosting.Minecraft.Worker\Aspire.Hosting.Minecraft.Worker.csproj" />
</ItemGroup>
```

Then use it in your AppHost:

```csharp
.WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
```

**Alternative:** If you cloned the repository, the sample demonstrates this setup.

## First Run

### 1. Start the Application

```bash
dotnet run
```

The Aspire dashboard opens at `http://localhost:15000` (or your configured port). You'll see:
- Your Minecraft server resource
- The worker service (child of Minecraft resource)
- Your monitored resources

### 2. Wait for Startup

The Minecraft server takes ~30-60 seconds to start:
1. Downloads Paper server JAR
2. Generates superflat world
3. Installs plugins (BlueMap, DecentHolograms if configured)
4. Starts RCON interface

Watch the logs in the Aspire dashboard to track progress. Look for `"Done! Server started"` in the Minecraft logs.

### 3. Connect with Minecraft

1. Open **Minecraft Java Edition**
2. Go to **Multiplayer** → **Add Server**
3. Server name: `Aspire Demo` (or any name)
4. Server address: `localhost:25565`
5. Click **Done**, then **Join Server**

### 4. Find the Village

Once in the game:
1. Set game mode to Creative: Press `T` to open chat, type `/gamemode creative`, press Enter
2. Fly to the village: Press `F3` to show coordinates, fly to `X=10, Y=-60, Z=0`
3. You'll see structures representing your resources

**Tip:** The default spawn point is random. Use coordinates to navigate directly to the village.

## Minimal Example

Here's the absolute minimum to see the integration in action:

```csharp
using Aspire.Hosting.Minecraft;

var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.MyApi>("api");

builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithMonitoredResource(api);

builder.Build().Run();
```

This gives you:
- ✅ Minecraft server at `localhost:25565`
- ✅ In-world village with one structure for "api"
- ✅ Basic health monitoring
- ❌ No persistent world (fresh world each run)
- ❌ No additional features (weather, boss bar, etc.)

## Recommended Setup

For the full experience, add these features:

```csharp
builder.AddMinecraftServer("minecraft")
    .WithPersistentWorld()              // Keep world across restarts
    .WithPeacefulMode()                 // No hostile mobs
    .WithBlueMap()                      // Web map at port 8100
    .WithOpenTelemetry()                // JVM metrics
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithBossBar()                      // Health percentage bar
    .WithWeatherEffects()               // Weather reflects health
    .WithHeartbeat()                    // Audio pulse
    .WithMonitoredResource(api);
```

## What You'll See

After connecting to the server:

### In Minecraft

- **Village** at coordinates `~10, -60, 0`
- **Structures** representing each monitored resource
- **Health indicators** on building fronts (glowstone/redstone lamps)
- **Boss bar** at the top showing fleet health (if enabled)
- **Weather** matching system state (if enabled)

### In Aspire Dashboard

- **Minecraft resource** with endpoints:
  - `game` (25565) — Minecraft game port
  - `rcon` (25575) — RCON management port
  - `world-map` (8100) — BlueMap web UI (if enabled)
- **Worker service** logging health checks and RCON activity
- **Metrics** from OpenTelemetry (if enabled):
  - `minecraft.tps` — Server ticks per second
  - `minecraft.mspt` — Milliseconds per tick
  - `jvm.memory.used` — JVM heap usage

## Next Steps

- **[Configuration Guide](configuration.md)** — Learn all available configuration options
- **[Feature Guides](features/)** — Explore each feature in detail
- **[Examples](examples.md)** — See complete working examples
- **[Troubleshooting](troubleshooting.md)** — Solve common issues

## Common First-Time Issues

### "Can't connect to server"

**Cause:** Server not fully started yet.

**Solution:** Wait for `"Done! Server started"` in Minecraft logs in Aspire dashboard.

### "Village not found"

**Cause:** Navigating to wrong coordinates.

**Solution:** Press `F3` in Minecraft to show coordinates. Fly to `X=10, Y=-60, Z=0`. The village is small with default setup (one structure per resource).

### "Fresh world every time"

**Cause:** Not using `.WithPersistentWorld()`.

**Solution:** Add `.WithPersistentWorld()` to keep world data across restarts.

### "Worker service crash"

**Cause:** RCON connection failed or worker not properly referenced.

**Solution:**
1. Check that `WithAspireWorldDisplay<T>()` uses the correct worker project type
2. Ensure worker project is referenced in AppHost.csproj
3. Check worker logs in Aspire dashboard for RCON errors
