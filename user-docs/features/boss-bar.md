# Boss Bar

The boss bar displays overall fleet health as a persistent percentage bar at the top of the screen.

## What It Does

Shows a persistent bar with:
- **Health percentage** (0-100%) based on healthy/total resources
- **Color coding** (green/yellow/red) based on health level
- **Custom title** (configurable)

The bar stays visible at all times, providing constant health awareness.

## How to Enable

```csharp
// Default title ("Aspire Fleet Health")
builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithBossBar()
    .WithMonitoredResource(api);

// Custom title
builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithBossBar("Production Fleet")
    .WithMonitoredResource(api);
```

## Configuration

### Title

**Default:** "Aspire Fleet Health"

**Custom:**
```csharp
.WithBossBar("My Custom Title")
```

**Environment variable:** `ASPIRE_BOSSBAR_TITLE` (set automatically by extension method)

## What You'll See in Minecraft

### Position

**Location:** Top center of screen, below player coordinates (if F3 is on)

### Display Components

**Title:** Text label (e.g., "Aspire Fleet Health")  
**Bar:** Visual fill indicator  
**Percentage:** Implicit in bar fill level (0-100%)

### Color Coding

**100% healthy:**
```
[████████████████████] Green bar
```

**50-99% healthy:**
```
[██████████          ] Yellow bar
```

**0-49% healthy:**
```
[████                ] Red bar
```

### Updates

Bar updates every health check cycle (5-10 seconds):
- Fill level changes smoothly
- Color transitions at thresholds
- Title remains constant

## Health Calculation

**Formula:**
```
health_percentage = (healthy_resources / total_resources) × 100
```

**Examples:**
- 4/4 healthy → 100% (green)
- 3/4 healthy → 75% (yellow)
- 2/4 healthy → 50% (yellow)
- 1/4 healthy → 25% (red)
- 0/4 healthy → 0% (red)

## Use Cases

### Constant Awareness

Monitor health while:
- Exploring the Minecraft world
- Building structures
- Working away from village

The boss bar is always visible.

### Presentations

Show aggregate health for audiences:
- Clear, high-level metric
- Color-coded for quick understanding
- Professional appearance

### Multi-Resource Monitoring

Track overall health of many resources:
- Individual structures may be off-screen
- Boss bar shows aggregate state
- Single glance assessment

### Alerts

Immediate feedback:
- Bar drops → some service failed
- Color changes → health threshold crossed
- No need to check dashboard

## Technical Details

### Implementation

**Minecraft command:** `/bossbar create|set` commands  
**Update frequency:** Every health check cycle  
**Persistence:** Remains visible until worker stops

### Boss Bar ID

**Internal ID:** `aspire:fleet_health`  
**Scope:** All players see the same bar  
**Removal:** Automatically removed when worker shuts down

## Combining with Other Features

### With Heartbeat

Visual + audio health indication:

```csharp
.WithBossBar()          // Visual percentage
.WithHeartbeat()        // Audio tempo/pitch
```

**Result:** See and hear fleet health simultaneously.

### With Title Alerts

Aggregate + event-driven alerts:

```csharp
.WithBossBar()          // Continuous health percentage
.WithTitleAlerts()      // Event-driven state changes
```

**Result:**
- **Boss bar:** Always showing current state
- **Title alerts:** Flash on transitions

### With Weather Effects

Multi-sensory feedback:

```csharp
.WithBossBar()          // Numerical health
.WithWeatherEffects()   // Atmospheric health
```

**Result:** Both data and ambiance reflect health.

### Minimal Setup

Boss bar alone provides high value:

```csharp
.WithBossBar("My Fleet")
```

**Use when:** You want simple, unobtrusive monitoring.

## Visual Reference

### Example Timeline

```
Time  | Health | Boss Bar Display
------|--------|--------------------------------------------------
 0:00 | 100%   | [████████████████████] Green "Production Fleet"
 1:30 |  75%   | [███████████████     ] Yellow "Production Fleet"
 3:00 |  50%   | [██████████          ] Yellow "Production Fleet"
 4:30 |  25%   | [█████               ] Red "Production Fleet"
 6:00 |   0%   | [                    ] Red "Production Fleet"
 7:30 | 100%   | [████████████████████] Green "Production Fleet"
```

## Customization

### Title Examples

**Environment-specific:**
```csharp
.WithBossBar("DEV Environment")
.WithBossBar("STAGING Fleet")
.WithBossBar("PRODUCTION")
```

**Descriptive:**
```csharp
.WithBossBar("Aspire Resources")
.WithBossBar("System Health")
.WithBossBar("Infrastructure Status")
```

**Fun:**
```csharp
.WithBossBar("Fleet Commander")
.WithBossBar("Mission Control")
.WithBossBar("Starfleet Status")
```

## Performance

**Overhead:** Single RCON command per health check cycle

**Updates:** Only when health percentage changes (not every cycle if unchanged)

**Recommended:** Works well with any number of resources

## Troubleshooting

### Boss bar not appearing

**Cause:** Feature not enabled or worker not started

**Solution:**
1. Verify `.WithBossBar()` is called
2. Check worker logs for errors
3. Ensure worker is running (child of Minecraft resource in dashboard)

### Boss bar shows wrong percentage

**Cause:** Polling delay or calculation issue

**Solution:**
1. Wait 5-10 seconds for next health check cycle
2. Verify all monitored resources are registered
3. Check worker logs for health check results

### Boss bar stuck at 0% or 100%

**Cause:** All resources in same state

**Solution:** This is expected behavior. Bar only changes when resource health changes.

### Multiple boss bars visible

**Cause:** Multiple Minecraft servers with boss bars

**Solution:** Each server creates its own boss bar. This is expected if running multiple instances.

## Code Example

```csharp
using Aspire.Hosting.Minecraft;

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("cache");
var postgres = builder.AddPostgres("db-host");
var api = builder.AddProject<Projects.MyApi>("api")
    .WithReference(redis)
    .WithReference(postgres);
var web = builder.AddProject<Projects.MyWeb>("web")
    .WithReference(api);

builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithBossBar("Demo Fleet")
    .WithMonitoredResource(api)
    .WithMonitoredResource(web)
    .WithMonitoredResource(redis)
    .WithMonitoredResource(postgres);

builder.Build().Run();
```

**Experience:**
- **All running:** Green bar at 100%
- **Stop Redis:** Yellow bar drops to 75%
- **Stop Postgres:** Yellow bar at 50%
- **Stop API:** Red bar at 25%
- **Stop Web:** Red bar at 0%
- **Restart all:** Bar gradually fills back to 100%, turns green

## Integration

Boss bar integrates with:
- **[Heartbeat](heartbeat.md)** — Audio complements visual
- **[Weather Effects](health-monitoring.md)** — Weather matches color zones
- **[World Border Pulse](world-border-pulse.md)** — Both trigger on critical health
- **[Action Bar Ticker](health-monitoring.md)** — Detailed metrics complement aggregate
