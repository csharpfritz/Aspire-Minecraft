# World Border Pulse

The world border pulse feature shrinks the playable area when your fleet health becomes critical, creating a sense of urgency.

## What It Does

Dynamically adjusts the Minecraft world border based on fleet health:
- **Normal state:** Border at 200 blocks diameter (comfortable play area)
- **Critical trigger:** >50% of resources unhealthy
- **Critical state:** Border shrinks to 100 blocks with red warning tint over 10 seconds
- **Recovery:** Border expands back to 200 blocks over 5 seconds

This creates visceral feedback for critical infrastructure failures.

## How to Enable

```csharp
builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithWorldBorderPulse()
    .WithMonitoredResource(api);
```

## Configuration

No additional configuration. The feature uses a fixed threshold:

**Trigger threshold:** >50% of resources unhealthy

**Example:**
- 4 resources: Triggers when 3+ are unhealthy
- 3 resources: Triggers when 2+ are unhealthy
- 2 resources: Triggers when 2 are unhealthy

## What You'll See in Minecraft

### Normal State

**Border size:** 200 blocks diameter (±100 from center)  
**Visual:** No visible border effects  
**Player experience:** Full freedom of movement

### Transition to Critical

When >50% resources become unhealthy:

1. **Red tint** appears at screen edges
2. **Border shrinks** from 200→100 blocks over 10 seconds
3. **Warning sound** plays (world border warning)
4. **Movement restricted** to smaller area

**Visual effect:** Pulsing red vignette at screen edges, intensifying as you approach the border.

### Critical State

**Border size:** 100 blocks diameter (±50 from center)  
**Visual:** Continuous red tint at edges  
**Player experience:** Confined to village area

### Recovery

When resources recover (≤50% unhealthy):

1. **Red tint fades**
2. **Border expands** from 100→200 blocks over 5 seconds
3. **Freedom restored**

**Visual effect:** Red tint gradually disappears, border recedes.

## Use Cases

### High-Stakes Monitoring

Create urgency for critical failures:
- Team feels pressure to resolve issues
- Visceral feedback for serious problems
- Memorable demo moment

### Presentations

Demonstrate consequences of failures:
- Audience sees immediate impact
- Visual metaphor for "system under stress"
- Dramatic effect for storytelling

### Training

Teach the importance of monitoring:
- Failures have consequences (even in Minecraft)
- Critical thresholds matter
- Recovery brings relief

### Personal Motivation

Gamify infrastructure work:
- "Escape the shrinking border"
- Race to restore services
- Celebrate recovery

## Technical Details

### Border Sizes

**Normal:** 200 blocks diameter  
**Critical:** 100 blocks diameter

**Center point:** World spawn (X=0, Z=0 by default)

**Village location:** X=10, Z=0 (well within critical border)

### Timing

**Shrink duration:** 10 seconds (slow, ominous)  
**Expand duration:** 5 seconds (faster recovery)

**Implementation:** Uses Minecraft `/worldborder` command with `set` parameter and time duration.

### Threshold Logic

**Check:** Every health check cycle  
**Trigger:** When `(unhealthy_count / total_count) > 0.5`  
**Reset:** When `(unhealthy_count / total_count) ≤ 0.5`

### Effects

**Warning tint:** Automatic Minecraft behavior when near border  
**Damage:** Not enabled (players don't take damage at border)  
**Sound:** Minecraft plays warning sound automatically

## Combining with Other Features

### With Boss Bar

Visual + spatial feedback:

```csharp
.WithBossBar()              // Numerical health
.WithWorldBorderPulse()     // Spatial urgency
```

**Result:**
- Boss bar shows percentage
- Border pulse shows critical threshold

### With Weather Effects

Layered atmospheric effects:

```csharp
.WithWeatherEffects()       // Weather reflects health
.WithWorldBorderPulse()     // Border reflects critical state
```

**Result:**
- **>50% healthy:** Rain + normal border
- **≤50% healthy:** Thunder + shrinking border

### With Heartbeat

Audio + spatial feedback:

```csharp
.WithHeartbeat()            // Audio tempo slows
.WithWorldBorderPulse()     // World shrinks
```

**Result:** Multi-sensory critical state indication.

### Standalone

World border pulse is effective alone:

```csharp
.WithWorldBorderPulse()
```

**Use when:** You want dramatic, low-configuration feedback.

## Visual Reference

### Example Timeline

```
Time  | Health | Border State
------|--------|--------------------------------
 0:00 | 100%   | 200 blocks (normal)
 1:00 |  75%   | 200 blocks (normal)
 2:00 |  50%   | 200 blocks (normal) — threshold
 3:00 |  25%   | Shrinking to 100 (red tint)
 4:00 |  25%   | 100 blocks (confined, red)
 5:00 |  75%   | Expanding to 200 (tint fading)
 6:00 | 100%   | 200 blocks (normal) — recovered
```

## Player Experience

### Critical Event

Player at village (X=10, Z=0):

1. **Sees:** Red vignette at screen edges
2. **Hears:** Warning sound
3. **Feels:** Sense of urgency
4. **Action:** Checks dashboard, investigates failures

### Recovery

After resolving issues:

1. **Sees:** Red tint fades
2. **Hears:** Warning sound stops
3. **Feels:** Relief, freedom restored
4. **Action:** Continues monitoring

## Performance

**Overhead:** Single RCON command per state transition

**Updates:** Only on critical threshold crossings (not every health check)

**Recommended:** Works well with any number of resources

## Troubleshooting

### Border not shrinking

**Cause:** Not meeting >50% unhealthy threshold or feature not enabled

**Solution:**
1. Verify `.WithWorldBorderPulse()` is called
2. Ensure >50% of monitored resources are unhealthy
3. Check worker logs for errors

### Border stuck small

**Cause:** Majority of resources remain unhealthy

**Solution:** This is expected behavior. Restore resources to >50% healthy to expand border.

### Red tint always visible

**Cause:** Player position near world border or border still at 100 blocks

**Solution:**
1. Check health percentage in Aspire dashboard
2. Verify resources are recovering
3. Wait for health to exceed 50% threshold

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
    .WithWorldBorderPulse()
    .WithMonitoredResource(api)
    .WithMonitoredResource(web)
    .WithMonitoredResource(redis)
    .WithMonitoredResource(postgres);

builder.Build().Run();
```

**Experience:**
- **All running:** Normal 200-block border
- **Stop Redis:** Still normal (75% healthy)
- **Stop Postgres:** Still normal (50% healthy — threshold)
- **Stop API:** Border shrinks, red tint (25% healthy — CRITICAL)
- **Restart Redis:** Border expands, tint fades (50% healthy)
- **Restart all:** Full 200-block border restored

## Integration

World border pulse integrates with:
- **[Boss Bar](boss-bar.md)** — Both show health level
- **[Weather Effects](health-monitoring.md)** — Layered atmospheric effects
- **[Heartbeat](heartbeat.md)** — Audio + spatial urgency
- **[Title Alerts](health-monitoring.md)** — Event notification + spatial feedback
