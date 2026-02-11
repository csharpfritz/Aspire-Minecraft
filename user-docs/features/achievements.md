# Achievements

Infrastructure milestone achievements provide gamification and feedback for monitoring activities.

## What It Does

Grants Minecraft-style achievements for infrastructure milestones:
- **First service health change**
- **All services healthy**
- **Night shift monitoring**
- **Village construction**

Each achievement appears as a title popup with sound effect, granted once per session.

## How to Enable

```csharp
builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithAchievements()
    .WithMonitoredResource(api);
```

## Available Achievements

### "First Blood"

**Trigger:** First resource becomes unhealthy

**Display:**
```
🏆 Achievement Unlocked!
First Blood
```

**Sound:** `entity.player.levelup`

**Meaning:** Your monitoring is working — the first failure was detected.

### "Clean Sweep"

**Trigger:** All resources are healthy simultaneously

**Display:**
```
🏆 Achievement Unlocked!
Clean Sweep
```

**Sound:** `ui.toast.challenge_complete`

**Meaning:** Full fleet health achieved.

### "The Village"

**Trigger:** Village construction completed

**Display:**
```
🏆 Achievement Unlocked!
The Village
```

**Sound:** `entity.player.levelup`

**Meaning:** In-world visualization is ready.

### "Night Shift"

**Trigger:** Monitoring during Minecraft night (in-game time)

**Display:**
```
🏆 Achievement Unlocked!
Night Shift
```

**Sound:** `entity.player.levelup`

**Meaning:** You're watching the system 24/7.

## What You'll See

**Title Display:**
- Large text at center of screen
- Yellow "Achievement Unlocked!" text
- Achievement name below
- Fades after 3-5 seconds

**Sound Effect:**
- Plays when achievement granted
- Audible even if music is off

**Chat Message:**
- Also appears in chat log
- Persists for review

## Use Cases

### Team Morale

Celebrate infrastructure wins:
- "Clean Sweep" after fixing issues
- "Night Shift" for on-call engineers

### Demos/Presentations

Add engagement to technical demos:
- Achievements provide natural "moments"
- Audience sees system milestones

### Training

Teach monitoring concepts:
- "First Blood" reinforces that failures are detected
- "Clean Sweep" shows recovery is tracked

### Gamification

Make monitoring fun:
- Track how many achievements team collects
- Compete for fastest "Clean Sweep" after deployment

## Technical Details

**Grant frequency:** Once per session per achievement

**Session:** From worker startup to shutdown. Restarting the AppHost resets achievement state.

**Implementation:** Uses Minecraft `/title` command with `title` and `subtitle` parameters.

## Combining with Other Features

### With Sound Effects

Double audio feedback:

```csharp
.WithAchievements()     // Achievement sounds
.WithSoundEffects()     // Health transition sounds
```

**Result:** Layered audio cues for different event types.

### With Title Alerts

Mix milestone and health alerts:

```csharp
.WithAchievements()     // Milestone achievements
.WithTitleAlerts()      // Health state alerts
```

**Result:** Achievements (yellow) vs. alerts (red/green).

### With Fireworks

Celebrate with effects:

```csharp
.WithAchievements()     // "Clean Sweep" achievement
.WithFireworks()        // Fireworks on all-healthy recovery
```

**Result:** Achievement + fireworks on full recovery.

## Code Example

```csharp
using Aspire.Hosting.Minecraft;

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("cache");
var api = builder.AddProject<Projects.MyApi>("api")
    .WithReference(redis);

builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithAchievements()
    .WithMonitoredResource(api)
    .WithMonitoredResource(redis);

builder.Build().Run();
```

**Experience:**
1. Village builds → "The Village" achievement
2. First health check passes → (no achievement yet)
3. Stop Redis → "First Blood" achievement
4. Restart Redis → "Clean Sweep" achievement
5. Monitor at night → "Night Shift" achievement

## Troubleshooting

### Achievements not appearing

**Cause:** Feature not enabled or already granted this session

**Solution:**
1. Verify `.WithAchievements()` is called
2. Restart AppHost to reset achievement state
3. Check worker logs for errors

### Duplicate achievements

**Cause:** Multiple players on server

**Solution:** Achievements are granted to all online players. This is expected behavior.

## Integration

Achievements work seamlessly with:
- **[Sound Effects](health-monitoring.md)** — Audio feedback
- **[Title Alerts](health-monitoring.md)** — Title system
- **[Fireworks](health-monitoring.md)** — Visual celebration
