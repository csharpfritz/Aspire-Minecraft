# Configuration Reference

Complete reference for all configuration options in Fritz.Aspire.Hosting.Minecraft.

## Core Server Setup

### AddMinecraftServer

Entry point for adding a Minecraft server to your Aspire application.

```csharp
IResourceBuilder<MinecraftServerResource> AddMinecraftServer(
    this IDistributedApplicationBuilder builder,
    string name,
    int? gamePort = null,
    int? rconPort = null)
```

**Parameters:**
- `name` — Unique resource name (appears in Aspire dashboard)
- `gamePort` — External port for Minecraft client connections (default: auto-assigned, target: 25565)
- `rconPort` — External port for RCON management (default: auto-assigned, target: 25575)

**Example:**

```csharp
// Auto-assign ports (recommended for development)
builder.AddMinecraftServer("minecraft")

// Fixed ports (useful for known server address)
builder.AddMinecraftServer("minecraft", gamePort: 25565, rconPort: 25575)
```

**Default Configuration:**
- Paper server (latest)
- Creative mode
- Superflat world
- Seed: "aspire2026"
- RCON enabled with auto-generated password
- View distance: 6 chunks (96 blocks)
- Simulation distance: 4 chunks
- No structure generation
- No mob spawning

### WithPersistentWorld

Persists world data across container restarts using a Docker volume.

```csharp
.WithPersistentWorld()
```

**Behavior:**
- Without this: Fresh world every run (ephemeral)
- With this: World data saved in volume `{name}-data`

**Use when:** You want to keep builds, changes, and world state across Aspire restarts.

**Example:**

```csharp
builder.AddMinecraftServer("minecraft")
    .WithPersistentWorld()
```

**To reset world:**

```bash
# Stop AppHost, then delete volume
docker volume rm minecraft-data
```

### WithBlueMap

Adds BlueMap plugin for 3D web-based world viewing.

```csharp
.WithBlueMap(int? port = null)
```

**Parameters:**
- `port` — External port for web UI (default: auto-assigned, target: 8100)

**What you get:**
- 3D interactive web map
- Real-time world updates
- Clickable "world-map" endpoint in Aspire dashboard

**Example:**

```csharp
builder.AddMinecraftServer("minecraft")
    .WithBlueMap(port: 8100)
```

Then open `http://localhost:8100` in your browser.

### WithOpenTelemetry

Enables OpenTelemetry instrumentation for JVM metrics.

```csharp
.WithOpenTelemetry()
```

**What you get:**
- JVM heap/non-heap memory metrics
- Garbage collection counts and duration
- Thread counts
- CPU utilization
- Metrics appear in Aspire dashboard

**Example:**

```csharp
builder.AddMinecraftServer("minecraft")
    .WithOpenTelemetry()
```

**Metrics exported:**
- `jvm.memory.used` — Memory usage by pool
- `jvm.gc.collections.count` — GC invocations
- `jvm.gc.collections.duration` — GC pause time
- `jvm.threads.live` — Thread count
- `process.cpu.utilization` — CPU usage

## Worker and Resource Monitoring

### WithAspireWorldDisplay

Registers the worker service that renders Aspire state in Minecraft. **Required for all in-world features.**

```csharp
.WithAspireWorldDisplay<TWorkerProject>()
where TWorkerProject : IProjectMetadata, new()
```

**Parameters:**
- `TWorkerProject` — Worker project type (typically `Projects.Aspire_Hosting_Minecraft_Worker`)

**What it does:**
- Creates internal worker service
- Establishes RCON connection to server
- Enables in-world visualization features
- Appears as child resource in Aspire dashboard

**Example:**

```csharp
builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
```

**Important:** Call this **before** `WithMonitoredResource()` or any `With*` feature methods.

### WithMonitoredResource

Adds an Aspire resource to be monitored and visualized in-world.

```csharp
// For resources with endpoints (HTTP/TCP health checks)
.WithMonitoredResource(
    IResourceBuilder<IResourceWithEndpoints> resource,
    params string[] dependsOn)

// For resources without endpoints (display only)
.WithMonitoredResource(
    IResourceBuilder<IResource> resource,
    string resourceType,
    params string[] dependsOn)
```

**Parameters:**
- `resource` — The resource to monitor
- `resourceType` — Display label (for endpoint-less resources)
- `dependsOn` — Resource names this resource depends on (optional)

**Example:**

```csharp
var redis = builder.AddRedis("cache");
var api = builder.AddProject<Projects.MyApi>("api")
    .WithReference(redis);

builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithMonitoredResource(api)          // Automatically detects HTTP endpoint
    .WithMonitoredResource(redis);       // Automatically detects TCP endpoint
```

**With dependencies:**

```csharp
.WithMonitoredResource(api, "cache")    // api depends on cache
```

**What you get:**
- Themed structure in village
- Health monitoring (HTTP or TCP)
- Visual health indicators
- Dependency tracking

## Health & Monitoring Features

### WithBossBar

Adds persistent boss bar showing fleet health percentage.

```csharp
.WithBossBar(string? title = null)
```

**Parameters:**
- `title` — Custom title (default: "Aspire Fleet Health")

**What you see:**
- Persistent bar at top of screen
- Value: 0-100% based on healthy/total resource count
- Color: Green (100%), yellow (50-99%), red (0-49%)

**Example:**

```csharp
.WithBossBar("Production Fleet")
```

### WithWeatherEffects

Links weather to fleet health.

```csharp
.WithWeatherEffects()
```

**Behavior:**
- **100% healthy** → Clear skies
- **50-99% healthy** → Rain
- **0-49% healthy** → Thunderstorm

**Changes:** Only on state transitions, not every poll cycle.

### WithWorldBorderPulse

Shrinks world border on critical failures.

```csharp
.WithWorldBorderPulse()
```

**Behavior:**
- **Trigger:** >50% of resources unhealthy
- **Effect:** Border shrinks 200→100 blocks over 10 seconds with red tint
- **Recovery:** Border expands 100→200 blocks over 5 seconds

### WithParticleEffects

Adds particle effects at resource structures.

```csharp
.WithParticleEffects()
```

**Effects:**
- **On crash:** `large_smoke` + `flame` particles
- **On recovery:** `happy_villager` particles

### WithBeaconTowers

Adds beacon towers with health-colored glass.

```csharp
.WithBeaconTowers()
```

**What you get:**
- Iron block base with beacon
- Stained glass on top
- **Healthy:** Cyan/blue/purple glass (matches Aspire dashboard colors)
- **Unhealthy:** Red glass
- Visible from 256 blocks away

## Audio & Effects

### WithHeartbeat

Enables rhythmic note block pulse reflecting fleet health.

```csharp
.WithHeartbeat()
```

**Behavior:**
- **100% healthy:** Fast tempo, high pitch
- **Degraded:** Slower tempo, lower pitch
- **0% healthy:** Silence (flatline)

**Technical:** Runs on independent timing loop.

### WithSoundEffects

Enables sound cues on health transitions.

```csharp
.WithSoundEffects()
```

**Sounds:**
- **Service down:** `entity.wither.ambient`
- **Service recovery:** `entity.player.levelup`
- **All green after failure:** `ui.toast.challenge_complete`

### WithFireworks

Launches fireworks on full fleet recovery.

```csharp
.WithFireworks()
```

**Trigger:** All resources return to healthy after at least one was unhealthy.

**Effect:** Multiple fireworks at various positions around the village.

### WithDeploymentFanfare

Celebrates deployments with dramatic effects.

```csharp
.WithDeploymentFanfare()
```

**Trigger:** Resource transitions from Starting → Running

**Effects:**
- Lightning bolt at structure
- Fireworks
- Title announcement

## Gamification Features

### WithAchievements

Enables infrastructure milestone achievements.

```csharp
.WithAchievements()
```

**Achievements:**
- **"First Blood"** — First resource becomes unhealthy
- **"Clean Sweep"** — All resources healthy simultaneously
- **"The Village"** — Village construction completed
- **"Night Shift"** — Monitoring at night (in-game time)

**Display:** Title popup + sound effect (granted once per session)

### WithTitleAlerts

Enables full-screen title alerts on health transitions.

```csharp
.WithTitleAlerts()
```

**Alerts:**
- **Service down:** Large red "⚠ SERVICE DOWN"
- **Service recovery:** Large green "✅ BACK ONLINE"

### WithActionBarTicker

Displays rotating metrics on HUD above hotbar.

```csharp
.WithActionBarTicker()
```

**Metrics:**
- Server TPS (ticks per second)
- MSPT (milliseconds per tick)
- Healthy resource count
- RCON latency

**Rotation:** Changes every poll cycle

## Village Features

### WithServiceSwitches

Adds levers and lamps to structures showing service state.

```csharp
.WithServiceSwitches()
```

**Placement:**
- Right side of structure entrance
- Lever + redstone lamp combination

**State:**
- **Healthy:** Lever ON (up), lamp lit
- **Unhealthy:** Lever OFF (down), lamp dark

**Important:** Display-only. Flipping levers manually doesn't control services.

### WithRedstoneDependencyGraph

Visualizes resource dependencies with redstone circuits.

```csharp
.WithRedstoneDependencyGraph()
```

**What you see:**
- L-shaped redstone wire connecting dependent resources
- Repeaters every 15 blocks
- Redstone lamps at structure entrances
- Wire breaks when resource becomes unhealthy

**Example:** If API depends on Redis, you'll see redstone wire from Redis structure to API structure.

### WithGuardianMobs

Spawns mobs representing resource health.

```csharp
.WithGuardianMobs()
```

**Mobs:**
- **Healthy resource:** Iron golem (protective)
- **Unhealthy resource:** Zombie (hostile)
- Named after their resource

**Note:** Use with `.WithPeacefulMode()` to disable hostile mobs while keeping visual effects.

### WithPeacefulMode

Eliminates hostile mobs for distraction-free monitoring.

```csharp
.WithPeacefulMode()
```

**Effect:** Sets difficulty to Peaceful via `/difficulty peaceful` command.

**Result:** No zombies, skeletons, creepers. Passive mobs (cows, pigs) remain.

## Sprint 4 Features

### WithRedstoneDashboard

Adds a Redstone Dashboard wall west of the village showing health history over time.

```csharp
.WithRedstoneDashboard()
```

**What you get:**
- Physical wall of redstone lamps west of the village
- Each row = one resource (labeled with signs)
- Each column = one time slot, scrolling left over time
- Lit lamp = healthy, dark lamp = unhealthy, sea lantern = unknown

**Auto-sizing:** Grid scales with resource count (10 columns for ≤8 resources, 8 for ≤16, 6 for 17+).

**Example:**

```csharp
builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithRedstoneDashboard()
    .WithMonitoredResource(api)
    .WithMonitoredResource(redis);
```

See [Sprint 4 Features Guide](sprint-4-features.md) for details.

### WithAllFeatures

Convenience method that enables every opt-in feature at once.

```csharp
.WithAllFeatures()
```

**What it enables:** All health monitoring, audio/effects, gamification, village, and dashboard features. Equivalent to calling every individual `With*()` method.

**Requires:** `WithAspireWorldDisplay()` must be called first.

**Example:**

```csharp
builder.AddMinecraftServer("minecraft")
    .WithPersistentWorld()
    .WithBlueMap()
    .WithOpenTelemetry()
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithAllFeatures()
    .WithMonitoredResource(api)
    .WithMonitoredResource(redis);
```

**Best for:** Demos, presentations, and exploring the full feature set.

See [Sprint 4 Features Guide](sprint-4-features.md) for details.

## Server Configuration

### WithServerProperty

Sets arbitrary `server.properties` values.

```csharp
// String property name
.WithServerProperty(string propertyName, string value)

// Enum property
.WithServerProperty(ServerProperty property, string value)
```

**Example:**

```csharp
.WithServerProperty("view-distance", "10")
.WithServerProperty(ServerProperty.MaxPlayers, "20")
```

### WithServerProperties

Sets multiple properties at once.

```csharp
.WithServerProperties(Dictionary<string, string> properties)
```

**Example:**

```csharp
var props = new Dictionary<string, string>
{
    ["view-distance"] = "10",
    ["max-players"] = "20",
    ["difficulty"] = "easy"
};

builder.AddMinecraftServer("minecraft")
    .WithServerProperties(props)
```

### WithServerPropertiesFile

Loads properties from a file.

```csharp
.WithServerPropertiesFile(string filePath)
```

**Parameters:**
- `filePath` — Path to `server.properties` file (relative to AppHost directory)

**Example:**

Create `server.properties`:

```properties
# My server config
max-players=20
motd=Aspire Fleet Monitor
difficulty=easy
view-distance=8
pvp=false
```

Load it:

```csharp
builder.AddMinecraftServer("minecraft")
    .WithServerPropertiesFile("server.properties")
    .WithMaxPlayers(10)  // Override file value
```

**Note:** Properties set via code override file values.

### WithGameMode

Sets game mode.

```csharp
// String
.WithGameMode(string mode)

// Enum
.WithGameMode(MinecraftGameMode mode)
```

**Values:** `survival`, `creative`, `adventure`, `spectator`

**Example:**

```csharp
.WithGameMode(MinecraftGameMode.Creative)
```

### WithDifficulty

Sets difficulty level.

```csharp
// String
.WithDifficulty(string difficulty)

// Enum
.WithDifficulty(MinecraftDifficulty difficulty)
```

**Values:** `peaceful`, `easy`, `normal`, `hard`

**Example:**

```csharp
.WithDifficulty(MinecraftDifficulty.Easy)
```

### WithMaxPlayers

Sets maximum player count.

```csharp
.WithMaxPlayers(int maxPlayers)
```

**Example:**

```csharp
.WithMaxPlayers(10)
```

### WithMotd

Sets message of the day (shown in server browser).

```csharp
.WithMotd(string motd)
```

**Example:**

```csharp
.WithMotd("Aspire Fleet Monitor")
```

### WithWorldSeed

Sets world generation seed.

```csharp
.WithWorldSeed(string seed)
```

**Example:**

```csharp
.WithWorldSeed("minecraft")
```

### WithPvp

Enables/disables player-versus-player combat.

```csharp
.WithPvp(bool enabled = true)
```

**Example:**

```csharp
.WithPvp(false)  // Disable PvP
```

## Developer Tools

### WithRconDebugLogging

Enables debug-level logging for all RCON commands.

```csharp
.WithRconDebugLogging()
```

**What you see:**
- Every RCON command sent
- Server responses
- Timing information
- Appears in Aspire dashboard logs (worker service)

**Use when:** Troubleshooting world building issues or verifying command execution.

**Example:**

```csharp
builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithRconDebugLogging()
```

## Feature Combinations

### Full Feature Set

```csharp
builder.AddMinecraftServer("minecraft")
    // Core
    .WithPersistentWorld()
    .WithPeacefulMode()
    .WithBlueMap()
    .WithOpenTelemetry()
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithAllFeatures()  // Enables all opt-in features at once
    // Resources
    .WithMonitoredResource(api)
    .WithMonitoredResource(redis);
```

Or, if you prefer to list features individually:
    
    // Health monitoring
    .WithBossBar("My Fleet")
    .WithWeatherEffects()
    .WithWorldBorderPulse()
    .WithParticleEffects()
    .WithBeaconTowers()
    
    // Audio & effects
    .WithHeartbeat()
    .WithSoundEffects()
    .WithFireworks()
    .WithDeploymentFanfare()
    
    // Gamification
    .WithAchievements()
    .WithTitleAlerts()
    .WithActionBarTicker()
    
    // Village features
    .WithServiceSwitches()
    .WithRedstoneDependencyGraph()
    .WithGuardianMobs()
    
    // Resources
    .WithMonitoredResource(api)
    .WithMonitoredResource(redis);
```

### Minimal Setup

```csharp
builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithMonitoredResource(api);
```

### Production-Friendly

```csharp
builder.AddMinecraftServer("minecraft")
    .WithPersistentWorld()
    .WithPeacefulMode()
    .WithOpenTelemetry()
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithBossBar("Production")
    .WithTitleAlerts()
    .WithActionBarTicker()
    .WithMonitoredResource(api)
    .WithMonitoredResource(redis);
```

### Demo/Presentation Mode

```csharp
builder.AddMinecraftServer("minecraft")
    .WithBlueMap()
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithBossBar("Demo Fleet")
    .WithWeatherEffects()
    .WithHeartbeat()
    .WithFireworks()
    .WithDeploymentFanfare()
    .WithAchievements()
    .WithBeaconTowers()
    .WithServiceSwitches()
    .WithRedstoneDependencyGraph()
    .WithMonitoredResource(api);
```

## Environment Variables

All feature flags are controlled via environment variables on the worker service:

| Environment Variable | Method | Description |
|---------------------|--------|-------------|
| `ASPIRE_FEATURE_TITLE_ALERTS` | `WithTitleAlerts()` | Full-screen alerts |
| `ASPIRE_FEATURE_WEATHER` | `WithWeatherEffects()` | Weather effects |
| `ASPIRE_FEATURE_BOSSBAR` | `WithBossBar()` | Boss bar |
| `ASPIRE_FEATURE_SOUNDS` | `WithSoundEffects()` | Sound effects |
| `ASPIRE_FEATURE_PARTICLES` | `WithParticleEffects()` | Particle effects |
| `ASPIRE_FEATURE_ACTIONBAR` | `WithActionBarTicker()` | Action bar ticker |
| `ASPIRE_FEATURE_BEACONS` | `WithBeaconTowers()` | Beacon towers |
| `ASPIRE_FEATURE_FIREWORKS` | `WithFireworks()` | Fireworks |
| `ASPIRE_FEATURE_GUARDIANS` | `WithGuardianMobs()` | Guardian mobs |
| `ASPIRE_FEATURE_FANFARE` | `WithDeploymentFanfare()` | Deployment fanfare |
| `ASPIRE_FEATURE_WORLDBORDER` | `WithWorldBorderPulse()` | World border pulse |
| `ASPIRE_FEATURE_HEARTBEAT` | `WithHeartbeat()` | Heartbeat |
| `ASPIRE_FEATURE_ACHIEVEMENTS` | `WithAchievements()` | Achievements |
| `ASPIRE_FEATURE_REDSTONE_GRAPH` | `WithRedstoneDependencyGraph()` | Redstone dependencies |
| `ASPIRE_FEATURE_SWITCHES` | `WithServiceSwitches()` | Service switches |
| `ASPIRE_FEATURE_PEACEFUL` | `WithPeacefulMode()` | Peaceful mode |
| `ASPIRE_FEATURE_REDSTONE_DASHBOARD` | `WithRedstoneDashboard()` | Redstone dashboard |

You typically don't set these manually — the extension methods set them automatically.
