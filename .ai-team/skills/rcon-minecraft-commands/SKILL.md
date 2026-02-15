# SKILL: RCON Minecraft Commands

> Patterns for sending Minecraft RCON commands from the Worker service to create in-game effects.

## When to Use

When implementing new in-world features that send RCON commands to the Minecraft server — particles, sounds, titles, boss bars, weather, entity spawning, scoreboard updates, etc.

## Command Format Reference

### Particle Effects
```
particle minecraft:{type} {x} {y} {z} {dx} {dy} {dz} {speed} {count} force
```
- `force` makes particles visible from far away (important for demo visibility)
- `dx dy dz` control the spread area (1 1 1 for a 2-block cube)
- Coordinates should align with `StructureBuilder` layout: `BaseX=10, BaseY=-60, BaseZ=0, Spacing=6`
- Particle center is at structure center: `(BaseX + index*Spacing + 1, BaseY + 2, BaseZ + 1)`

### Title Screen Text
```
title @a times {fadeIn} {stay} {fadeOut}     // in ticks (20 ticks = 1 second)
title @a title {json}                        // main title (large text)
title @a subtitle {json}                     // subtitle (smaller text below)
title @a actionbar {json}                    // action bar (above hotbar)
title @a clear                               // clear current title
```
- JSON text format: `{"text":"message","color":"red","bold":true}`
- Set `times` BEFORE `title`/`subtitle` — order matters
- Colors: `red`, `green`, `gold`, `gray`, `white`, etc.

### Weather
```
weather clear
weather rain
weather thunder
```
- No duration = persists until changed
- Only one weather state at a time — track last state to avoid redundant commands

### Boss Bar
```
bossbar add {namespace:id} {json}            // create with display name
bossbar set {id} max {int}                   // max value (typically 100)
bossbar set {id} value {int}                 // current value (0 to max)
bossbar set {id} players @a                  // show to all players
bossbar set {id} visible true                // toggle visibility
bossbar set {id} color green|yellow|red|blue|pink|purple|white
bossbar set {id} name {json}                 // update display name
bossbar remove {id}                          // remove
```
- ID must be `namespace:name` format (e.g., `aspire:fleet_health`)
- ID must be lowercase
- Create once, then update value/color/name as needed

### Sound Effects
```
playsound minecraft:{sound} {source} {selector} {x} {y} {z} {volume} {pitch}
playsound minecraft:{sound} {source} {selector}   // short form, plays at player location
```
- `~ ~ ~` for relative coordinates (plays at each player's location)
- Source channel: `master`, `music`, `record`, `weather`, `block`, `hostile`, `neutral`, `player`, `ambient`, `voice`
- Volume: float, 1.0 = normal
- Pitch: 0.5–2.0 (0.5 = half speed, 2.0 = double speed)

## Architecture Pattern: Opt-in Features

### Builder Extension Method (Hosting side)
```csharp
public static IResourceBuilder<MinecraftServerResource> WithFeatureName(
    this IResourceBuilder<MinecraftServerResource> builder)
{
    var workerBuilder = builder.Resource.WorkerBuilder
        ?? throw new InvalidOperationException(
            "WithFeatureName() requires WithAspireWorldDisplay() to be called first.");

    workerBuilder.WithEnvironment("ASPIRE_FEATURE_FEATURENAME", "true");
    return builder;
}
```

### Conditional Registration (Worker Program.cs)
```csharp
if (!string.IsNullOrEmpty(builder.Configuration["ASPIRE_FEATURE_FEATURENAME"]))
    builder.Services.AddSingleton<FeatureNameService>();
```

### Optional Injection (Worker BackgroundService)
```csharp
file sealed class MinecraftWorldWorker(
    // ... required services ...
    FeatureNameService? featureName = null) : BackgroundService
```

### Service Pattern
```csharp
public sealed class FeatureNameService(
    RconService rcon,
    AspireResourceMonitor monitor,      // if aggregate feature
    ILogger<FeatureNameService> logger)
{
    private SomeState _lastState;       // track for transition-only logic

    public async Task DoSomethingAsync(IReadOnlyList<ResourceStatusChange> changes, CancellationToken ct)
    {
        // Only act when state changed (transition-only)
        if (newState == _lastState) return;

        await rcon.SendCommandAsync("command here", ct);
        _lastState = newState;
    }
}
```

## Key Rules

1. **Minimize RCON commands** — track state, only send on transitions
2. **Use `force` for particles** — demos need visibility from spawn
3. **Set title `times` before content** — Minecraft processes them in order
4. **Boss bar IDs are `namespace:name`** — lowercase, colon-separated
5. **Per-resource features** (particles, titles, sounds) iterate changes list
6. **Aggregate features** (weather, boss bar) read `monitor.HealthyCount/TotalCount`
7. **All services are opt-in** — never break backward compatibility
8. **Structure visual depth** — layer fills in order: base shell (`fill hollow`), then overlay contrasting blocks (buttresses, weathering), then decorative elements (stairs, glass, iron bars). Later fills overwrite earlier ones, creating material variety without `replace` commands.
9. **Block palette for depth** — use 3+ block types per structure face: a primary (stone_bricks), a dark accent (deepslate_bricks), weathering (cracked/mossy), and decorative (chiseled, stairs, walls). Contrast creates perceived depth even on flat surfaces.
10. **RCON budget accounting** — count commands in the method AND all village overhead (fence, paths, health indicator, sign, BlueMap). The test asserts total < 100.
