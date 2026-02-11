# Service Switches

Service switches provide a visual, at-a-glance representation of service state using Minecraft levers and redstone lamps.

## What It Does

Places a lever and redstone lamp combination on each resource structure:
- **Lever position** indicates service state (UP = healthy, DOWN = unhealthy)
- **Lamp state** reinforces the signal (LIT = healthy, DARK = unhealthy)

**Important:** This is **display-only**. Flipping levers manually does not control Aspire services.

## How to Enable

```csharp
builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithServiceSwitches()
    .WithMonitoredResource(api);
```

## Configuration

No additional configuration. The feature is controlled by the `ASPIRE_FEATURE_SWITCHES` environment variable (set automatically).

## What You'll See in Minecraft

### Placement

**Location:** Right side of each structure's front entrance

**Components:**
- **Lever:** Mounted on wall at Y+2 (waist height)
- **Redstone lamp:** Mounted on wall at Y+3 (head height), directly above lever

### Healthy Service

```
[Redstone Lamp] ← LIT (glowing orange)
      ↓
    [Lever] ← UP position
```

**Visual:** Orange glowing lamp above an upward-facing lever

### Unhealthy Service

```
[Redstone Lamp] ← DARK (unlit)
      ↓
    [Lever] ← DOWN position
```

**Visual:** Dark lamp above a downward-facing lever

### State Changes

When a service health changes:
1. **Lever position updates** (smooth animation in-game)
2. **Lamp state updates** (instant light change)
3. Updates occur within 5-10 seconds (polling interval)

## Use Cases

### Quick Status Check

Walk down the village boulevard and glance at switches to see which services are healthy.

**Healthy village:**
```
All levers UP, all lamps LIT
```

**One service down:**
```
One lever DOWN, one lamp DARK
Others UP and LIT
```

### Presentations

Clear visual metaphor for technical audiences:
- **Lever = switch/control**
- **Lamp = indicator light**
- Matches real-world control panel aesthetics

### Team Collaboration

In multi-player scenarios, team members can:
- See service state at a glance
- Identify which service is down
- Navigate to that structure for details

### Screenshots/Videos

Provides clear before/after visuals for documentation or demos.

## Technical Details

### Placement Logic

**Lever coordinates:**
```
X: Structure X + 5 (right edge)
Y: Structure Y + 2 (waist height)
Z: Structure Z + 1 (front wall)
Facing: WEST (toward structure)
```

**Lamp coordinates:**
```
X: Structure X + 5 (right edge)
Y: Structure Y + 3 (above lever)
Z: Structure Z + 1 (front wall)
```

### Redstone Mechanics

**Lever ON (healthy):**
- Lever provides redstone power
- Lamp receives power and lights up

**Lever OFF (unhealthy):**
- Lever provides no power
- Lamp goes dark

This mirrors real-world electrical switches and indicators.

## Display-Only Behavior

### Why Display-Only?

Service switches reflect Aspire resource state but **do not control** services. This is by design:

1. **Safety:** Prevents accidental service shutdowns
2. **Separation of concerns:** Minecraft is a visualization layer, not a control plane
3. **Realistic representation:** Shows actual state from health checks, not player actions

### What Happens If You Flip a Lever?

If you manually flip a lever in creative mode:
1. **Lever position changes** (client-side)
2. **Lamp state may change** (powered by redstone)
3. **Worker resets it** on next health check cycle (5-10 seconds)

The worker continuously synchronizes switch state with actual service health.

### Decision Record

From team decision:

> Service switches are display-only and continuously reset to match health check state. This prevents confusion and maintains accurate visualization. User interactions with levers are overridden on the next sync cycle.

## Combining with Other Features

### With Redstone Dependencies

Shows both service state and dependencies:

```csharp
.WithServiceSwitches()
.WithRedstoneDependencyGraph()
```

**Result:** Levers show state, redstone wires show connections.

### With Beacon Towers

Multi-level health indication:

```csharp
.WithServiceSwitches()
.WithBeaconTowers()
```

**Result:** Beacons visible from afar, switches show detail up close.

### With Health Lamps

Redundant but clear messaging:

```csharp
.WithServiceSwitches()
// Health lamps are built-in
```

**Result:** Both front-wall lamp and side switch show state.

## Visual Reference

### Example Village (4 Services)

```
Structure 1 (API)         Structure 2 (Redis)
[Lamp:LIT] [Lever:UP]    [Lamp:LIT] [Lever:UP]

Structure 3 (Web)         Structure 4 (Postgres)
[Lamp:DARK] [Lever:DOWN] [Lamp:LIT] [Lever:UP]
```

**Interpretation:** API, Redis, and Postgres healthy. Web service down.

## Common Patterns

### All Healthy

```
All levers: UP position
All lamps: LIT (orange glow)
Visual: Uniform, positive state
```

### One Service Down

```
One lever: DOWN position
One lamp: DARK
Others: UP and LIT
Visual: Clear outlier, easy to spot
```

### Majority Down

```
Most levers: DOWN position
Most lamps: DARK
Few remaining: UP and LIT
Visual: Obvious crisis state
```

## Accessibility

Service switches provide:
- **Color-independent signaling** — Lever position visible even to colorblind players
- **Physical metaphor** — Intuitive "switch" concept
- **Dual indication** — Both lever (position) and lamp (light) reinforce state

## Performance

**Minimal overhead:**
- 2 RCON commands per resource (set lever, set lamp)
- Updates only on health state changes (not every poll cycle)
- Rate-limited with other visual updates

**Recommended capacity:** Works well with up to 50 resources.

## Troubleshooting

### Switches not appearing

**Cause:** Feature not enabled or village not built

**Solution:**
1. Verify `.WithServiceSwitches()` is called
2. Check worker logs for errors
3. Ensure village structures are built

### Switches flickering/resetting

**Cause:** Player manually flipping levers

**Solution:** This is expected behavior. Don't manually interact with levers — they reflect actual service state.

### Wrong state shown

**Cause:** Health check polling delay

**Solution:** Wait 5-10 seconds for next health check cycle. The worker polls resources at intervals.

## Integration with Other Features

Service switches work seamlessly with:
- **[Village Visualization](village-visualization.md)** — Placed on structures
- **[Redstone Dependencies](redstone-dependencies.md)** — Visual separation (switches vs. wires)
- **[Health Monitoring](health-monitoring.md)** — Complements health lamps
- **[Boss Bar](boss-bar.md)** — Switches show individual state, boss bar shows aggregate

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
    .WithServiceSwitches()
    .WithMonitoredResource(api)
    .WithMonitoredResource(web)
    .WithMonitoredResource(redis);

builder.Build().Run();
```

**Result:**
- 3 structures in village
- Each has lever + lamp on right side
- State reflects actual service health
- Player cannot control services via levers
