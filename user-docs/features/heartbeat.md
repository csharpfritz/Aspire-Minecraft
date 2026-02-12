# Heartbeat

The heartbeat feature provides an audible, rhythmic pulse that reflects your fleet's overall health through tempo and pitch.

## What It Does

Creates a note block pulse with:
- **Tempo** changes based on health percentage (fast → slow)
- **Pitch** changes based on health percentage (high → low)
- **Silence** when all resources are unhealthy (flatline)

The heartbeat runs on its own timing loop, independent of visual updates.

## How to Enable

```csharp
builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithHeartbeat()
    .WithMonitoredResource(api);
```

## What You'll Hear

### 100% Healthy

**Tempo:** Fast, steady rhythm (~1 beat per second)  
**Pitch:** High note (note block pitch 24)  
**Feel:** Energetic, positive

### 75% Healthy

**Tempo:** Moderate rhythm (~1.5 seconds per beat)  
**Pitch:** Medium-high note (note block pitch 18)  
**Feel:** Steady, stable

### 50% Healthy

**Tempo:** Slower rhythm (~2 seconds per beat)  
**Pitch:** Medium note (note block pitch 12)  
**Feel:** Labored, concerning

### 25% Healthy

**Tempo:** Slow rhythm (~3 seconds per beat)  
**Pitch:** Low note (note block pitch 6)  
**Feel:** Critical, struggling

### 0% Healthy

**Tempo:** Silence (no beats)  
**Pitch:** N/A  
**Feel:** Flatline, system down

## Configuration

No additional configuration. The feature uses health percentage to calculate tempo and pitch dynamically.

**Health percentage:** `(healthy resources / total resources) × 100`

## Use Cases

### Ambient Awareness

Monitor system health while:
- Working in other Minecraft areas
- Building or exploring
- Not looking at the screen

The heartbeat provides background awareness without requiring visual attention.

### Multi-Tasking

Keep Minecraft running in the background:
- Audio cue alerts you to changes
- No need to watch dashboard
- Responsive feedback loop

### Presentations

Add dramatic audio to demos:
- Heartbeat slows as services fail
- Silence on total failure
- Heartbeat accelerates on recovery

### Mood Setting

Create atmosphere for monitoring:
- Healthy system = upbeat rhythm
- Failing system = ominous slowdown
- Flatline = crisis mode

## Technical Details

### Implementation

**Note block placement:** At village center (fixed coordinates)  
**Sound:** `note.harp` (default note block sound)  
**Timing:** Independent background service (not tied to health check cycle)

### Timing Loop

**100% healthy:** ~1000ms between beats  
**50% healthy:** ~2000ms between beats  
**0% healthy:** No beats (silence)

**Formula:**
```
interval_ms = 1000 + (1000 × (1 - health_percentage))
```

### Pitch Calculation

**100% healthy:** Pitch 24 (high note)  
**0% healthy:** Pitch 0 (lowest note)

**Formula:**
```
pitch = (int)(24 × health_percentage)
```

## Combining with Other Features

### With Sound Effects

Layer audio feedback:

```csharp
.WithHeartbeat()        // Continuous pulse
.WithSoundEffects()     // Event-driven sounds
```

**Result:**
- **Heartbeat:** Continuous background rhythm
- **Sound effects:** Punctuated health change alerts

### With Weather Effects

Multi-sensory feedback:

```csharp
.WithHeartbeat()        // Audio
.WithWeatherEffects()   // Visual (weather)
```

**Result:** Both audio and visual cues reflect health.

### With Boss Bar

Visual + audio health indication:

```csharp
.WithHeartbeat()        // Audio pulse
.WithBossBar()          // Visual percentage
```

**Result:** See and hear fleet health simultaneously.

## Audio Reference

### Example Session

1. **Startup:** Fast, high-pitched heartbeat (all healthy)
2. **Service fails:** Tempo slows, pitch drops
3. **Another service fails:** Further slowdown, lower pitch
4. **All services fail:** Silence (flatline)
5. **Recovery begins:** Heartbeat returns, slow and low
6. **Full recovery:** Fast, high-pitched heartbeat restored

### Volume

**In-game volume:** Respects client's "Blocks" volume setting

**Audible range:** ~64 blocks from note block (standard Minecraft)

**Adjustment:** Players can adjust volume in Audio Settings → Blocks

## Performance

**Overhead:** Minimal — single RCON command per beat

**Background service:** Runs on separate thread, no impact on health checks

**Recommended:** Works well with any number of resources

## Troubleshooting

### No heartbeat sound

**Cause:** Client volume settings or note block not placed

**Solution:**
1. Check Audio Settings → Blocks volume
2. Verify `.WithHeartbeat()` is called
3. Check worker logs for errors

### Heartbeat not changing

**Cause:** All resources at same health level

**Solution:** Wait for health change. Heartbeat only changes when fleet health percentage changes.

### Heartbeat too fast/slow

**Cause:** By design — reflects health percentage

**Solution:** This is expected behavior. Fast = healthy, slow = unhealthy.

## Code Example

```csharp
using Aspire.Hosting.Minecraft;

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("cache");
var api = builder.AddProject<Projects.MyApi>("api")
    .WithReference(redis);
var web = builder.AddProject<Projects.MyWeb>("web")
    .WithReference(api);

builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithHeartbeat()
    .WithMonitoredResource(api)
    .WithMonitoredResource(web)
    .WithMonitoredResource(redis);

builder.Build().Run();
```

**Experience:**
- **All healthy:** Fast, high heartbeat
- **Stop Redis:** Slower, lower heartbeat (66% health)
- **Stop Web:** Even slower, lower (33% health)
- **Stop API:** Silence (0% health)
- **Restart all:** Heartbeat gradually speeds up and rises in pitch

## Integration

Heartbeat integrates with:
- **[Boss Bar](boss-bar.md)** — Visual percentage matches audio tempo
- **[Weather Effects](health-monitoring.md)** — Multi-sensory feedback
- **[Sound Effects](health-monitoring.md)** — Layered audio cues
