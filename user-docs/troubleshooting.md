# Troubleshooting Guide

Solutions to common issues when using Fritz.Aspire.Hosting.Minecraft.

## Installation Issues

### Package Not Found

**Symptom:** `dotnet add package Fritz.Aspire.Hosting.Minecraft` fails

**Solutions:**
1. Check package name spelling (case-sensitive on some systems)
2. Ensure NuGet.org is in your package sources: `dotnet nuget list source`
3. Try with explicit version: `dotnet add package Fritz.Aspire.Hosting.Minecraft --version 0.3.0`

### Worker Project Not Found

**Symptom:** `Projects.Aspire_Hosting_Minecraft_Worker` doesn't resolve

**Solutions:**
1. Add project reference to AppHost.csproj:
   ```xml
   <ProjectReference Include="..\path\to\Aspire.Hosting.Minecraft.Worker\Aspire.Hosting.Minecraft.Worker.csproj" />
   ```
2. Rebuild solution: `dotnet build`
3. Check namespace is correct in `Program.cs`

## Server Startup Issues

### Server Won't Start

**Symptom:** Minecraft resource shows "Unhealthy" in Aspire dashboard

**Solutions:**
1. Check Docker Desktop is running
2. Check logs in Aspire dashboard for error messages
3. Verify ports 25565/25575 are not already in use:
   ```powershell
   netstat -ano | findstr "25565"
   ```
4. Try auto-assigned ports instead of fixed:
   ```csharp
   .AddMinecraftServer("minecraft")  // No port arguments
   ```

### Slow Startup

**Symptom:** Minecraft takes 2-5 minutes to start

**Causes:**
- First-time Paper server download
- Docker image pull
- Plugin downloads (BlueMap, DecentHolograms)

**Solutions:**
- Wait for initial download (one-time)
- Subsequent starts will be faster (~30-60 seconds)
- Check Docker Desktop for download progress

### RCON Health Check Failing

**Symptom:** Health check fails continuously

**Solutions:**
1. Wait for full server startup (watch for "Done! Server started" in logs)
2. Check RCON password is set correctly (auto-generated, should work automatically)
3. Verify RCON port is accessible
4. Check worker logs for connection errors

## World Generation Issues

### Village Not Appearing

**Symptom:** Connected to server but no structures visible

**Solutions:**
1. Navigate to correct coordinates: **X=10, Y=-60, Z=0**
   - Press `F3` to show coordinates
   - Fly to the village location
2. Verify `WithMonitoredResource()` was called for at least one resource
3. Check worker logs for world-building commands
4. Enable debug logging: `.WithRconDebugLogging()`

### Structures Incomplete or Glitching

**Symptom:** Buildings flickering, rebuilding repeatedly

**Solutions:**
1. Ensure you're on v0.2.1+ (bug fixed in this version)
2. Use `.WithPersistentWorld()` to keep world state
3. Check RCON rate limiting in worker logs
4. Reduce monitored resource count if >20 resources

### Fresh World Every Time

**Symptom:** World resets on every AppHost restart

**Cause:** Persistent world not enabled (default behavior)

**Solution:**
```csharp
.WithPersistentWorld()  // Add this
```

### Need to Reset World

**Symptom:** Changes to worker code not reflected in world

**Solutions:**

**Without `.WithPersistentWorld()` (ephemeral):**
1. Stop AppHost
2. Find unnamed volume:
   ```powershell
   docker volume ls | Select-String "minecraft"
   ```
3. Delete volume:
   ```powershell
   docker volume rm <volume-id>
   ```

**With `.WithPersistentWorld()` (named volume):**
```powershell
docker stop $(docker ps -q --filter "name=minecraft")
docker volume rm minecraft-data
```

## Feature Issues

### Features Not Working

**Symptom:** Called `With*()` method but feature doesn't activate

**Solutions:**
1. Verify `WithAspireWorldDisplay()` is called **before** feature methods
2. Check worker is running (appears as child in Aspire dashboard)
3. Review worker logs for errors
4. Confirm environment variable is set (visible in worker's environment section)

### Beacons Not Visible

**Symptom:** Beacon towers built but beams not showing

**Solutions:**
1. Fly closer to village (render distance limit is 256 blocks)
2. Increase view distance:
   ```csharp
   .WithServerProperty("view-distance", "10")
   ```
3. Check client render distance in Minecraft Video Settings

### Particle Effects Not Showing

**Symptom:** No particles at structures

**Solutions:**
1. Check Minecraft Video Settings → Particles (set to "All")
2. Verify feature is enabled: `.WithParticleEffects()`
3. Wait for health transition event (particles appear on state changes)

### Sound Effects Not Playing

**Symptom:** No audio feedback

**Solutions:**
1. Check Minecraft Audio Settings → Blocks volume
2. Verify feature is enabled: `.WithSoundEffects()`
3. Wait for health transition event (sounds play on state changes)

### Guardian Mobs Disappearing

**Symptom:** Mobs spawn but vanish quickly

**Cause:** Minecraft's natural mob despawn behavior

**Solutions:**
- Expected behavior — mobs respawn on next health check cycle (5-10 seconds)
- Use `.WithPeacefulMode()` to avoid hostile mob issues

## Health Monitoring Issues

### Health Indicators Not Updating

**Symptom:** Lamps/beacons don't change when service fails

**Solutions:**
1. Wait 5-10 seconds for next health check cycle
2. Verify resource has health endpoint (HTTP or TCP)
3. Check worker logs for health check results
4. Confirm resource is actually failing (check Aspire dashboard)

### Wrong Health State Shown

**Symptom:** Minecraft shows healthy but Aspire dashboard shows unhealthy

**Solutions:**
1. Wait for next health check cycle (slight delay is normal)
2. Check worker logs for health check errors
3. Verify resource endpoint configuration:
   - HTTP resources: Worker can access the URL
   - TCP resources: Worker can reach host:port

### Boss Bar Stuck at 0% or 100%

**Symptom:** Boss bar doesn't move

**Cause:** All resources in same health state

**Solution:** This is expected behavior. Bar changes only when resource health changes.

## Connection Issues

### Can't Connect to Server

**Symptom:** Minecraft client shows "Can't connect" or times out

**Solutions:**
1. Verify server is running (green in Aspire dashboard)
2. Check server address: `localhost:25565` or custom port
3. Ensure Minecraft Java Edition (not Bedrock)
4. Check firewall isn't blocking port 25565
5. Verify server logs show "Done! Server started"

### Connection Lost During Play

**Symptom:** Disconnected after initial connection

**Solutions:**
1. Check AppHost didn't crash (Aspire dashboard)
2. Check Docker Desktop is running
3. Review Minecraft server logs for errors
4. Check system resources (CPU/memory)

## Performance Issues

### High CPU Usage

**Symptom:** Docker container using excessive CPU

**Solutions:**
1. Reduce monitored resource count (<10 recommended)
2. Disable visual features (beacons, particles, guardians)
3. Reduce view distance:
   ```csharp
   .WithServerProperty("view-distance", "4")
   ```

### Slow World Building

**Symptom:** Initial village takes >30 seconds to build

**Cause:** RCON rate limiting (10 commands/second)

**Solutions:**
- Expected behavior for many resources
- Consider reducing monitored resources
- Check RCON latency in metrics: `minecraft.rcon.latency_ms`

### High Memory Usage

**Symptom:** Docker container using >2GB RAM

**Solutions:**
1. Reduce view/simulation distance
2. Disable BlueMap if not needed:
   ```csharp
   // Remove .WithBlueMap()
   ```
3. Use `.WithPeacefulMode()` to reduce mob overhead

## BlueMap Issues

### BlueMap Not Accessible

**Symptom:** Can't open `http://localhost:8100`

**Solutions:**
1. Verify `.WithBlueMap()` is called
2. Check port 8100 is not in use
3. Wait for BlueMap plugin to fully initialize (1-2 minutes after server start)
4. Check Minecraft logs for BlueMap errors
5. Verify endpoint appears in Aspire dashboard

### BlueMap Not Updating

**Symptom:** Map shows old world state

**Cause:** BlueMap updates on a schedule, not real-time

**Solutions:**
- Wait for next update cycle (configurable in BlueMap settings)
- Refresh browser page
- Check BlueMap plugin is running (logs in Minecraft server)

## Networking Issues

### Worker Can't Reach Resources

**Symptom:** Worker logs show connection errors to monitored resources

**Solutions:**
1. Verify resources are running (Aspire dashboard)
2. Check resource endpoints are correct:
   - Use `host.docker.internal` instead of `localhost` for resources on host
   - Verify port numbers
3. Check Docker network configuration

### RCON Connection Failures

**Symptom:** Worker logs: "RCON connection failed"

**Solutions:**
1. Verify Minecraft server is fully started
2. Check RCON port (25575) is accessible
3. Check RCON password is set (auto-generated)
4. Wait for server readiness (health check passes)

## Common Error Messages

### "WithAspireWorldDisplay() has not been called first"

**Cause:** Feature methods require worker service

**Solution:**
```csharp
builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()  // Add this FIRST
    .WithBossBar()  // Then feature methods
```

### "server.properties file not found"

**Cause:** `WithServerPropertiesFile()` path is incorrect

**Solutions:**
1. Use path relative to AppHost directory
2. Verify file exists:
   ```csharp
   .WithServerPropertiesFile("server.properties")  // In AppHost project root
   ```
3. Use absolute path if needed

### "Modrinth plugin download failed"

**Cause:** Network issue downloading plugin

**Solutions:**
1. Check internet connection
2. Retry (stop and restart AppHost)
3. Verify Docker can access external networks

## Getting Help

If you can't resolve an issue:

1. **Enable debug logging:**
   ```csharp
   .WithRconDebugLogging()
   ```

2. **Collect information:**
   - Aspire dashboard logs (Minecraft + Worker resources)
   - Docker Desktop logs
   - Steps to reproduce the issue

3. **Search existing issues:**
   [GitHub Issues](https://github.com/csharpfritz/Aspire-Minecraft/issues)

4. **Report a bug:**
   [Create new issue](https://github.com/csharpfritz/Aspire-Minecraft/issues/new)
   Include:
   - Fritz.Aspire.Hosting.Minecraft version
   - .NET SDK version
   - Operating system
   - Full error logs
   - Minimal reproduction code

## Advanced Troubleshooting

### Check RCON Manually

Test RCON connection directly:

```bash
# Install mcrcon (example tool)
docker run --rm -it --network=host \
  itzg/mc-rcon \
  -H localhost -P 25575 -p <password> \
  "/help"
```

### Inspect Docker Container

Connect to running container:

```powershell
docker exec -it <container-id> bash
cd /data
ls -la
```

### Check Environment Variables

View worker environment:

1. Open Aspire dashboard
2. Navigate to worker resource
3. View "Environment" tab
4. Verify feature flags are set correctly

### Monitor Metrics

Check OpenTelemetry metrics (if enabled):

- `minecraft.tps` — Should be ~20.0
- `minecraft.mspt` — Should be <50ms
- `minecraft.rcon.latency_ms` — Should be <10ms

High values indicate performance issues.
