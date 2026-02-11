# RCON Debug Logging

RCON debug logging enables detailed visibility into all commands sent to the Minecraft server, useful for troubleshooting world-building issues.

## What It Does

Enables debug-level logging for the `RconService`:
- **Every RCON command** logged with full details
- **Server responses** logged
- **Timing information** included
- **Visible in Aspire dashboard** (worker logs)

## How to Enable

```csharp
builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithRconDebugLogging()
    .WithMonitoredResource(api);
```

## Configuration

Sets the log level environment variable:

```
Logging__LogLevel__Aspire.Hosting.Minecraft.Worker.Services.RconService=Debug
```

This is done automatically by the extension method.

## What You'll See

### In Aspire Dashboard

Navigate to the **Minecraft worker** logs (child of Minecraft resource):

**Log entries:**
```
[DBG] Sending RCON command: /fill 10 -60 0 16 -60 6 minecraft:cobblestone
[DBG] RCON response: Filled 42 blocks
[DBG] Command latency: 12ms

[DBG] Sending RCON command: /setblock 13 -55 1 minecraft:glowstone
[DBG] RCON response: Block placed
[DBG] Command latency: 8ms
```

### Information Included

**Per command:**
- Full command text
- Server response
- Execution time (latency in milliseconds)
- Timestamp

## Use Cases

### Troubleshooting World Building

When structures aren't appearing correctly:

1. Enable debug logging
2. Watch logs in Aspire dashboard
3. Identify failed commands or errors
4. Fix issues (coordinates, block types, etc.)

### Verifying Command Execution

Confirm that features are sending correct commands:
- Check structure placement coordinates
- Verify block types
- Confirm command syntax

### Performance Analysis

Identify slow commands:
- Commands taking >100ms may indicate rate limiting
- Batch command performance
- Network latency issues

### Development

When building new features:
- Verify RCON commands are correct
- Debug command order
- Test edge cases

## Technical Details

### Log Level

**Default:** `Information` (only shows high-level operations)  
**Debug mode:** `Debug` (shows all RCON commands)

### Performance Impact

**Minimal overhead:**
- Logging itself adds <1ms per command
- Disk I/O handled asynchronously
- No impact on gameplay

### Log Volume

**Typical session:**
- Initial build: 50-100 commands
- Runtime: 5-20 commands per health check cycle

**Storage:** Logs stored by Aspire dashboard (temporary unless configured otherwise)

## Examples

### Village Build Sequence

```
[DBG] Sending RCON command: /fill 10 -60 0 16 -60 6 minecraft:stone_bricks
[DBG] RCON response: Filled 49 blocks
[DBG] Command latency: 15ms

[DBG] Sending RCON command: /fill 11 -59 1 15 -50 5 minecraft:air
[DBG] RCON response: Filled 270 blocks
[DBG] Command latency: 18ms

[DBG] Sending RCON command: /setblock 13 -55 1 minecraft:glowstone
[DBG] RCON response: Block placed
[DBG] Command latency: 9ms
```

**Analysis:** Structure building progressing normally, good latency.

### Rate Limiting

```
[DBG] Sending RCON command: /fill 10 -60 0 16 -60 6 minecraft:cobblestone
[DBG] Rate limit reached, queuing command
[DBG] Command sent after 250ms delay
[DBG] RCON response: Filled 42 blocks
[DBG] Command latency: 262ms
```

**Analysis:** Rate limiting active, commands queued.

### Error Case

```
[DBG] Sending RCON command: /setblock 13 -55 1 minecraft:invalid_block
[ERR] RCON error: Unknown block type 'invalid_block'
[DBG] Command latency: 11ms
```

**Analysis:** Invalid block type, command rejected.

## Combining with Other Features

Debug logging doesn't interfere with any features. Use it alongside anything:

```csharp
.WithRconDebugLogging()
.WithBeaconTowers()
.WithServiceSwitches()
.WithRedstoneDependencyGraph()
```

**Result:** All feature commands logged for inspection.

## Troubleshooting

### Too much log output

**Cause:** Debug logging is very verbose

**Solution:** 
1. Enable only when needed for troubleshooting
2. Filter logs in Aspire dashboard by log level
3. Remove `.WithRconDebugLogging()` after debugging

### Not seeing debug logs

**Cause:** Feature not enabled or log level filtering

**Solution:**
1. Verify `.WithRconDebugLogging()` is called
2. Check Aspire dashboard log level filter (ensure "Debug" is visible)
3. Check worker is running

## When to Use

### ✅ Use debug logging when:
- Structures not appearing correctly
- Commands seem to be failing
- Developing new features
- Reporting bugs to repository

### ❌ Don't use debug logging when:
- Everything works correctly
- In production deployments
- Log volume is a concern

## Code Example

```csharp
using Aspire.Hosting.Minecraft;

var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.MyApi>("api");

builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithRconDebugLogging()  // Enable for troubleshooting
    .WithBeaconTowers()
    .WithServiceSwitches()
    .WithMonitoredResource(api);

builder.Build().Run();
```

**Result:**
- All RCON commands logged to Aspire dashboard
- Full visibility into world-building process
- Easy troubleshooting of issues

## Performance Notes

**Command rate:** ~10 commands/second (rate-limited)  
**Logging overhead:** <1% of command execution time  
**Disk usage:** ~100-500 KB per session (temporary)

## Integration

RCON debug logging is a developer tool that integrates with all features:
- **[Village Visualization](village-visualization.md)** — See structure build commands
- **[Service Switches](service-switches.md)** — See lever/lamp placement
- **[Redstone Dependencies](redstone-dependencies.md)** — See wire placement
- **[Beacon Towers](health-monitoring.md)** — See beacon build commands

Use it to understand and troubleshoot any feature's RCON activity.
