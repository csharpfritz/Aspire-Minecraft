# Health Monitoring

Health monitoring features provide real-time visual feedback about your Aspire resources' health status directly in Minecraft.

## Overview

Multiple mechanisms work together to show resource health:
- **Health lamps** — Glowstone (healthy) or redstone lamp (unhealthy)
- **Beacon towers** — Color-coded beacons visible from afar
- **Particle effects** — Smoke/flame on crash, happy particles on recovery
- **Guardian mobs** — Iron golems (healthy) or zombies (unhealthy)

## Health Lamps (Built-in)

**Enabled by default** with village visualization.

### How It Works

Each structure has a lamp embedded in its front wall:
- **Position:** Front wall, Y+5 (above door), horizontally centered
- **Healthy:** Glowing glowstone block (yellow)
- **Unhealthy:** Redstone lamp (dark/red)

### Configuration

No configuration needed — automatically included with village.

### What You See

**Healthy resource:**
```
Structure with glowing yellow lamp in front wall
```

**Unhealthy resource:**
```
Structure with dark/red lamp in front wall
```

**Transition:**
When a resource health changes, the lamp updates within 5-10 seconds (polling interval).

## Beacon Towers

**Enable with:** `.WithBeaconTowers()`

### What It Does

Adds a beacon tower above each resource structure:
- **Base:** 3×3 iron block pyramid
- **Beacon:** Active beacon on top
- **Glass:** 3×3 stained glass cap
- **Height:** Extends from structure roof upward

### How to Enable

```csharp
builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithBeaconTowers()
    .WithMonitoredResource(api);
```

### Glass Colors

**Healthy resources:**
- **Blue glass** — Most resources
- **Purple glass** — Database resources
- **Cyan glass** — Cache resources

Colors match Aspire dashboard resource colors.

**Unhealthy resources:**
- **Red glass** — All unhealthy resources

### What You See

**From a distance:**
- Colorful beacon beams shooting into the sky
- Beams visible from 256 blocks away
- Beam color changes when resource health changes

**Up close:**
- Iron pyramid base
- Beacon block emitting beam
- Colored stained glass cap

### Use Cases

- **Long-distance monitoring** — See health from spawn point
- **Dashboard presentation** — Impressive visual effect
- **Multi-player scenarios** — Team members see health from anywhere

### Technical Details

**Materials:**
- Iron blocks: 9 per tower (3×3 base)
- Beacon: 1 per tower
- Stained glass: 9 per tower (3×3 cap)

**Placement:**
- Base: Placed on structure roof
- Height: 4 blocks above structure
- Beam render distance: 256 blocks (Minecraft limit)

## Particle Effects

**Enable with:** `.WithParticleEffects()`

### What It Does

Spawns particle effects at resource structures on health transitions.

### How to Enable

```csharp
builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithParticleEffects()
    .WithMonitoredResource(api);
```

### Effect Types

**On crash (healthy → unhealthy):**
- **`large_smoke`** — Grey smoke rising
- **`flame`** — Fire particles
- **Location:** Structure center, Y+2 (roof level)
- **Duration:** Continuous until recovery

**On recovery (unhealthy → healthy):**
- **`happy_villager`** — Green sparkles
- **Location:** Structure center
- **Duration:** Brief burst (3-5 seconds)

### What You See

**Service crash:**
```
Dark smoke and flames rising from the building
Visual indicator of distress
```

**Service recovery:**
```
Green sparkly particles burst
Celebratory effect
```

### Use Cases

- **Event notification** — Immediate visual feedback on state change
- **Ambient awareness** — See failures without watching dashboard
- **Recording demos** — Clear visual effects for videos/presentations

## Guardian Mobs

**Enable with:** `.WithGuardianMobs()`

### What It Does

Spawns protective or hostile mobs representing resource health.

### How to Enable

```csharp
builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithGuardianMobs()
    .WithMonitoredResource(api);
```

**Important:** Consider using with `.WithPeacefulMode()` to disable mob attacks:

```csharp
.WithGuardianMobs()
.WithPeacefulMode()  // Visual mobs without combat
```

### Mob Types

**Healthy resources:**
- **Iron Golem** — Protective guardian
- **Named:** After resource (e.g., "api")
- **Behavior:** Stands near structure entrance

**Unhealthy resources:**
- **Zombie** — Hostile mob
- **Named:** After resource
- **Behavior:** Wanders near structure

### What You See

**All healthy:**
```
Iron golems standing guard at each building
Peaceful village atmosphere
```

**Service down:**
```
Zombie appears at the affected building
Other structures still have golems
```

### Use Cases

- **Playful monitoring** — Gamified health representation
- **Team building** — Fun way to present infrastructure
- **Education** — Teaching distributed systems concepts

### Important Notes

**Mob despawning:** Minecraft may despawn mobs over time. The worker respawns them on the next health check cycle.

**Peaceful mode:** Use `.WithPeacefulMode()` to keep visual effect without hostile mob attacks:

```csharp
.WithGuardianMobs()
.WithPeacefulMode()
```

## Health Check Mechanism

All health monitoring relies on the worker's health check system:

### HTTP Resources

Projects, web apps with HTTP endpoints:
- **Method:** HTTP GET to `/health` or root endpoint
- **Frequency:** Every 5-10 seconds (configurable)
- **Success:** HTTP 200-299 response
- **Failure:** Timeout, connection error, non-2xx response

### TCP Resources

Redis, databases, other TCP services:
- **Method:** TCP socket connection test
- **Frequency:** Every 5-10 seconds
- **Success:** Connection established
- **Failure:** Connection timeout or refused

### Health States

- **Healthy:** Resource responding successfully
- **Unhealthy:** Resource failing health checks
- **Starting:** Resource is starting up (not yet checked)
- **Unknown:** No endpoint information available

## Combining Features

### Comprehensive Health Monitoring

```csharp
builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithBeaconTowers()      // Long-distance visibility
    .WithParticleEffects()   // Event notifications
    .WithGuardianMobs()      // Playful representation
    .WithPeacefulMode()      // No hostile attacks
    .WithMonitoredResource(api);
```

### Minimal but Effective

```csharp
builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithBeaconTowers()      // Beacons are highly visible
    .WithMonitoredResource(api);
```

### Demo-Friendly

```csharp
builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithBeaconTowers()
    .WithParticleEffects()
    .WithSoundEffects()      // Audio feedback
    .WithTitleAlerts()       // Screen alerts
    .WithMonitoredResource(api);
```

## Visual Reference

### Healthy State (All Features)

```
- Glowstone lamp glowing yellow
- Blue/cyan/purple beacon beam
- Iron golem standing guard
- No particle effects
- Clear weather (if WithWeatherEffects enabled)
```

### Unhealthy State (All Features)

```
- Redstone lamp dark/red
- Red beacon beam
- Zombie at structure
- Smoke and flame particles rising
- Storm weather (if WithWeatherEffects enabled)
```

## Performance Considerations

**Low overhead:** All features use RCON commands, minimal server load.

**Network bandwidth:** Particle effects and mob spawning add minimal network traffic.

**Recommended:**
- **< 10 resources:** All features perform well
- **10-20 resources:** Consider selective features
- **> 20 resources:** Use beacons only for visibility

## Troubleshooting

### Beacons not visible

**Cause:** Too far away or not in render distance

**Solution:**
- Fly closer to village
- Increase view distance: `.WithServerProperty("view-distance", "10")`

### Particle effects not appearing

**Cause:** Particle settings in Minecraft client

**Solution:** Check Video Settings → Particles (set to "All")

### Mobs despawning

**Cause:** Normal Minecraft mob despawn behavior

**Solution:** Mobs respawn on next health check cycle (5-10 seconds)

### Health lamps not updating

**Cause:** Polling interval or RCON rate limiting

**Solution:** Wait 5-10 seconds for next health check cycle

## Integration with Other Features

Health monitoring integrates with:
- **[Boss Bar](boss-bar.md)** — Shows aggregate health percentage
- **[Weather Effects](health-monitoring.md)** — Weather reflects fleet health
- **[Title Alerts](health-monitoring.md)** — Full-screen alerts on transitions
- **[Sound Effects](health-monitoring.md)** — Audio feedback
