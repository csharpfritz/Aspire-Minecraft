# Usage Examples

Real-world usage patterns and complete code samples for Fritz.Aspire.Hosting.Minecraft.

## Basic Examples

### Minimal Setup

The absolute minimum to visualize one service:

```csharp
using Aspire.Hosting.Minecraft;

var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.MyApi>("api");

builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithMonitoredResource(api);

builder.Build().Run();
```

**Result:**
- Minecraft server at `localhost:25565`
- Single watchtower structure for "api"
- Basic health monitoring

### Persistent World

Keep world data across restarts:

```csharp
builder.AddMinecraftServer("minecraft")
    .WithPersistentWorld()
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithMonitoredResource(api);
```

### With Visual Feedback

Add visual health indicators:

```csharp
builder.AddMinecraftServer("minecraft")
    .WithPersistentWorld()
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithBossBar()
    .WithWeatherEffects()
    .WithMonitoredResource(api);
```

## Multi-Service Examples

### API + Database

Typical web API with database:

```csharp
using Aspire.Hosting.Minecraft;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("db-host");
var db = postgres.AddDatabase("db");

var api = builder.AddProject<Projects.MyApi>("api")
    .WithReference(db);

builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithRedstoneDependencyGraph()  // Show API → DB dependency
    .WithServiceSwitches()          // Visual state indicators
    .WithMonitoredResource(api)
    .WithMonitoredResource(postgres);

builder.Build().Run();
```

**Result:**
- Watchtower (API) + Cottage (Postgres)
- Redstone wire from Postgres to API
- Switches showing health state

### Full Stack Application

Web frontend, API, cache, and database:

```csharp
using Aspire.Hosting.Minecraft;

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("cache");
var postgres = builder.AddPostgres("db-host");
var db = postgres.AddDatabase("db");

var api = builder.AddProject<Projects.MyApi>("api")
    .WithReference(redis)
    .WithReference(db);

var web = builder.AddProject<Projects.MyWeb>("web")
    .WithReference(api)
    .WithExternalHttpEndpoints();

builder.AddMinecraftServer("minecraft")
    .WithPersistentWorld()
    .WithBlueMap()
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithBossBar("My Application")
    .WithWeatherEffects()
    .WithBeaconTowers()
    .WithRedstoneDependencyGraph()
    .WithMonitoredResource(api)
    .WithMonitoredResource(web)
    .WithMonitoredResource(redis)
    .WithMonitoredResource(postgres);

builder.Build().Run();
```

**Result:**
- 4 structures in village grid
- Redstone wires showing dependencies
- Beacon towers for distance visibility
- Boss bar showing aggregate health
- Weather reflecting system state
- BlueMap web UI at port 8100

## Feature Showcase Examples

### Demo/Presentation Mode

All visual and audio features enabled:

```csharp
using Aspire.Hosting.Minecraft;

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("cache");
var api = builder.AddProject<Projects.MyApi>("api")
    .WithReference(redis);

builder.AddMinecraftServer("minecraft", gamePort: 25565)
    .WithMaxPlayers(10)
    .WithMotd("Aspire Fleet Monitor")
    .WithPeacefulMode()
    .WithBlueMap(port: 8100)
    .WithOpenTelemetry()
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    
    // Core monitoring
    .WithBossBar("Demo Fleet")
    .WithTitleAlerts()
    .WithActionBarTicker()
    
    // Visual effects
    .WithWeatherEffects()
    .WithWorldBorderPulse()
    .WithParticleEffects()
    .WithBeaconTowers()
    .WithFireworks()
    .WithDeploymentFanfare()
    
    // Audio effects
    .WithHeartbeat()
    .WithSoundEffects()
    
    // Gamification
    .WithAchievements()
    .WithGuardianMobs()
    
    // Architecture
    .WithServiceSwitches()
    .WithRedstoneDependencyGraph()
    
    // Resources
    .WithMonitoredResource(api)
    .WithMonitoredResource(redis);

builder.Build().Run();
```

**Use case:** Conference demos, team presentations, product showcases.

### Architecture Visualization

Focus on system dependencies:

```csharp
using Aspire.Hosting.Minecraft;

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("cache");
var postgres = builder.AddPostgres("db-host");
var db = postgres.AddDatabase("db");

var api = builder.AddProject<Projects.MyApi>("api")
    .WithReference(redis)
    .WithReference(db);

var web = builder.AddProject<Projects.MyWeb>("web")
    .WithReference(api);

var worker = builder.AddProject<Projects.Worker>("worker")
    .WithReference(redis)
    .WithReference(db);

builder.AddMinecraftServer("minecraft")
    .WithBlueMap()  // Top-down view of architecture
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithRedstoneDependencyGraph()  // Visual DAG
    .WithServiceSwitches()          // State indicators
    .WithMonitoredResource(api)
    .WithMonitoredResource(web)
    .WithMonitoredResource(worker)
    .WithMonitoredResource(redis)
    .WithMonitoredResource(postgres);

builder.Build().Run();
```

**Use case:** Architecture documentation, system design reviews, teaching distributed systems.

### Ambient Monitoring

Low-distraction background monitoring:

```csharp
using Aspire.Hosting.Minecraft;

var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.MyApi>("api");

builder.AddMinecraftServer("minecraft")
    .WithPersistentWorld()
    .WithPeacefulMode()
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithBossBar("Production")
    .WithHeartbeat()  // Audio awareness without visuals
    .WithMonitoredResource(api);

builder.Build().Run();
```

**Use case:** Keep Minecraft running in background, audio heartbeat alerts to failures.

## Configuration Examples

### Using server.properties File

Centralized server configuration:

**Create `server.properties` in AppHost project:**

```properties
# Server settings
max-players=20
motd=Aspire Infrastructure Monitor
difficulty=easy
view-distance=8
simulation-distance=6

# World settings
level-seed=aspire2026
generate-structures=false

# Gameplay
pvp=false
spawn-protection=0
enable-command-block=true
```

**Load in Program.cs:**

```csharp
builder.AddMinecraftServer("minecraft")
    .WithServerPropertiesFile("server.properties")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithMonitoredResource(api);
```

### Custom Game Settings

Creative mode, high visibility:

```csharp
builder.AddMinecraftServer("minecraft")
    .WithGameMode(MinecraftGameMode.Creative)
    .WithDifficulty(MinecraftDifficulty.Peaceful)
    .WithMaxPlayers(20)
    .WithMotd("Come monitor with us!")
    .WithServerProperty("view-distance", "10")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithMonitoredResource(api);
```

### Fixed Ports

Use specific ports instead of auto-assigned:

```csharp
builder.AddMinecraftServer("minecraft", gamePort: 25565, rconPort: 25575)
    .WithBlueMap(port: 8100)
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithMonitoredResource(api);
```

**Use case:** Server address needs to be known/documented.

## Advanced Examples

### Environment-Specific Configuration

Different setups per environment:

```csharp
using Aspire.Hosting.Minecraft;

var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.MyApi>("api");

var minecraft = builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithMonitoredResource(api);

// Development: All features
if (builder.Environment.IsDevelopment())
{
    minecraft
        .WithBlueMap()
        .WithOpenTelemetry()
        .WithBossBar("Development")
        .WithWeatherEffects()
        .WithHeartbeat()
        .WithBeaconTowers()
        .WithRedstoneDependencyGraph()
        .WithServiceSwitches()
        .WithAchievements()
        .WithRconDebugLogging();
}
// Production: Minimal, performance-focused
else
{
    minecraft
        .WithPersistentWorld()
        .WithBossBar("Production")
        .WithTitleAlerts();
}

builder.Build().Run();
```

### Multi-Player Monitoring Team

Setup for team collaboration:

```csharp
builder.AddMinecraftServer("minecraft", gamePort: 25565)
    .WithMaxPlayers(10)
    .WithMotd("Team Monitoring - All Welcome!")
    .WithPersistentWorld()
    .WithPeacefulMode()
    .WithBlueMap()
    .WithOpenTelemetry()
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    
    // Team-friendly features
    .WithBossBar("Team Dashboard")
    .WithTitleAlerts()       // Everyone sees alerts
    .WithAchievements()      // Shared celebrations
    .WithWeatherEffects()    // Shared atmosphere
    .WithBeaconTowers()      // Long-distance visibility
    .WithServiceSwitches()   // Clear state indicators
    
    .WithMonitoredResource(api);
```

**Setup:**
1. Run AppHost on a team server
2. Share server address with team
3. Team members connect to monitor together

### Azure Resource Group Integration

Prepare for large-scale monitoring (conceptual):

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Add only high-priority Azure resources
var appService = builder.AddProject<Projects.WebApp>("webapp");
var sqlDb = builder.AddSqlServer("sql-server");
var redis = builder.AddRedis("redis-cache");

builder.AddMinecraftServer("minecraft")
    .WithServerProperty("view-distance", "10")  // Larger area for more resources
    .WithPersistentWorld()
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithBossBar("Azure Resources")
    .WithBeaconTowers()           // Essential for large villages
    .WithRedstoneDependencyGraph() // Architecture visibility
    
    // Limit features for performance
    // .WithParticleEffects()      // Skip for many resources
    // .WithGuardianMobs()         // Skip for many resources
    
    .WithMonitoredResource(appService)
    .WithMonitoredResource(sqlDb)
    .WithMonitoredResource(redis);
    // ... add up to ~20-30 resources max

builder.Build().Run();
```

**Note:** See [Troubleshooting](troubleshooting.md) for scaling considerations with >20 resources.

### Local Development Workflow

Quick setup for dev loop:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.MyApi>("api");

// No persistent world — fresh start each run (fast iteration)
builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithBossBar("Dev Mode")
    .WithRconDebugLogging()  // Troubleshoot world-building
    .WithMonitoredResource(api);

builder.Build().Run();
```

**Workflow:**
1. Make code changes
2. Stop AppHost
3. Restart → Fresh world with changes
4. Test and iterate

## Testing Examples

### Failure Scenarios

Test monitoring behavior with intentional failures:

```csharp
using Aspire.Hosting.Minecraft;

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("cache");
var api = builder.AddProject<Projects.MyApi>("api")
    .WithReference(redis);

builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithBossBar("Test Scenario")
    .WithWeatherEffects()
    .WithWorldBorderPulse()
    .WithTitleAlerts()
    .WithHeartbeat()
    .WithFireworks()
    .WithMonitoredResource(api)
    .WithMonitoredResource(redis);

builder.Build().Run();
```

**Test scenarios:**
1. Stop Redis → Watch weather change to rain, heartbeat slow
2. Stop API → Watch boss bar drop to 0%, world border shrink
3. Restart both → Watch fireworks, weather clear, heartbeat speed up

## Integration Examples

### With Existing Aspire App

Add to existing Aspire application:

```csharp
// Your existing Aspire setup
var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");
var db = builder.AddPostgres("postgres").AddDatabase("db");
var api = builder.AddProject<Projects.Api>("api")
    .WithReference(cache)
    .WithReference(db);
var web = builder.AddProject<Projects.Web>("web")
    .WithReference(api)
    .WithExternalHttpEndpoints();

// Add Minecraft visualization (non-invasive)
builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithBossBar()
    .WithMonitoredResource(api)
    .WithMonitoredResource(web)
    .WithMonitoredResource(cache)
    .WithMonitoredResource(db);

builder.Build().Run();
```

**Result:** Existing app unchanged, Minecraft visualization added as bonus feature.

## Next Steps

- **[Configuration Reference](configuration.md)** — Explore all available options
- **[Feature Guides](features/)** — Deep-dive into specific features
- **[Troubleshooting](troubleshooting.md)** — Solve common issues
