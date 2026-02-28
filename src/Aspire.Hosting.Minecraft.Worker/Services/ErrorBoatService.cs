using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Spawns boats carrying creeper passengers when resources transition to Unhealthy.
/// Boats appear in the canal water behind the erroring building and slide toward the trunk canal on blue ice.
/// Anti-pileup caps limit per-resource and global boat counts.
/// Requires CanalService to have built canals before spawning.
/// </summary>
internal sealed class ErrorBoatService(
    RconService rcon,
    AspireResourceMonitor monitor,
    ILogger<ErrorBoatService> logger,
    CanalService? canals = null)
{
    private const int MaxBoatsPerResource = 3;
    private const int MaxTotalBoats = 20;
    private static readonly TimeSpan SpawnCooldown = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CountResetInterval = TimeSpan.FromSeconds(30);

    private readonly Dictionary<string, int> _boatsPerResource = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _lastSpawnTime = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ResourceStatusChange> _pendingChanges = new();
    private int _totalBoats;
    private DateTime _lastCountReset = DateTime.UtcNow;

    /// <summary>
    /// One-time initialization. Records initial state (currently a no-op).
    /// </summary>
    public Task InitializeAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Error boat service initialized");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Spawns an error boat for a specific resource triggered by an OpenTelemetry error-level log entry.
    /// Applies the same rate limiting and canal readiness gating as health-based spawns.
    /// </summary>
    public async Task SpawnBoatForErrorLogAsync(string resourceName, CancellationToken ct = default)
    {
        // Gate on canal readiness
        if (canals is null || canals.CanalPositions.Count == 0)
        {
            logger.LogInformation("Buffering error boat for {Resource} (canals not ready)", resourceName);
            _pendingChanges.Add(new ResourceStatusChange(resourceName, "unknown", ResourceStatus.Healthy, ResourceStatus.Unhealthy));
            return;
        }

        await SpawnBoatCoreAsync(resourceName, ct);
    }

    /// <summary>
    /// Spawns error boats for resources that transitioned to Unhealthy.
    /// Boats are only spawned when canals have been built (CanalPositions populated).
    /// If canals aren't ready yet, changes are buffered and replayed when canals become available.
    /// </summary>
    public async Task SpawnBoatsForChangesAsync(IReadOnlyList<ResourceStatusChange> changes, CancellationToken ct = default)
    {
        if (changes.Count == 0) return;

        // Gate on canal readiness — if canals aren't built yet, buffer the changes for later
        if (canals is null || canals.CanalPositions.Count == 0)
        {
            // Buffer unhealthy transitions so we can spawn boats once canals are ready
            foreach (var change in changes)
            {
                if (change.NewStatus == ResourceStatus.Unhealthy)
                {
                    // Only buffer if not already in the pending list
                    if (!_pendingChanges.Any(pc => pc.Name.Equals(change.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        _pendingChanges.Add(change);
                        logger.LogInformation("Buffering error boat spawn for {Resource} (canals not ready yet)", change.Name);
                    }
                }
            }
            return;
        }

        // Canals are ready! Process both new changes and any buffered changes
        var allChanges = new List<ResourceStatusChange>(_pendingChanges);
        allChanges.AddRange(changes);
        _pendingChanges.Clear();

        foreach (var change in allChanges)
        {
            if (change.NewStatus != ResourceStatus.Unhealthy) continue;
            await SpawnBoatCoreAsync(change.Name, ct);
        }
    }

    private async Task SpawnBoatCoreAsync(string resourceName, CancellationToken ct)
    {
        var orderedNames = VillageLayout.ReorderByDependency(monitor.Resources);
        var index = orderedNames.IndexOf(resourceName);
        if (index < 0) return;

        // Per-resource cooldown
        if (_lastSpawnTime.TryGetValue(resourceName, out var lastSpawn)
            && DateTime.UtcNow - lastSpawn < SpawnCooldown)
            return;

        // Per-resource cap
        _boatsPerResource.TryGetValue(resourceName, out var resourceBoats);
        if (resourceBoats >= MaxBoatsPerResource) return;

        // Global cap
        if (_totalBoats >= MaxTotalBoats) return;

        var (cx, cy, cz) = VillageLayout.GetCanalEntrance(resourceName, index);

        // Atomic spawn: boat + creeper passenger in single command to avoid RCON timing issues
        // Motion set at spawn time gets one physics tick before dampening; high velocity needed for water friction
        await rcon.SendCommandAsync(
            $"summon minecraft:oak_boat {cx} {cy} {cz} {{Rotation:[270f,0f],Motion:[-5.0,0.0,0.5],Passengers:[{{id:\"minecraft:creeper\",NoAI:1b,Silent:1b}}],Tags:[\"error_boat\"]}}",
            CommandPriority.Normal, ct);

        _boatsPerResource[resourceName] = resourceBoats + 1;
        _totalBoats++;
        _lastSpawnTime[resourceName] = DateTime.UtcNow;

        logger.LogInformation("Error boat spawned at ({X},{Y},{Z}) for {Resource}",
            cx, cy, cz, resourceName);
    }

    /// <summary>
    /// Despawns boats near the lake and periodically resets tracking counters.
    /// </summary>
    public async Task CleanupBoatsAsync(CancellationToken ct = default)
    {
        if (_totalBoats <= 0 && _boatsPerResource.Count == 0) return;

        try
        {
            var resourceCount = monitor.Resources.Count;
            if (resourceCount == 0) return;

            var (lakeX, _, lakeZ) = VillageLayout.GetLakePosition(resourceCount);
            var lakeCenterX = lakeX + VillageLayout.LakeWidth / 2;
            var lakeCenterZ = lakeZ + VillageLayout.LakeLength / 2;

            // Kill error boats near the lake using the "error_boat" tag for reliable targeting
            await rcon.SendCommandAsync(
                $"kill @e[type=minecraft:boat,tag=error_boat,x={lakeCenterX},y={VillageLayout.SurfaceY},z={lakeCenterZ},distance=..15]",
                CommandPriority.Low, ct);

            // Periodically reset counters to avoid stale tracking
            if (DateTime.UtcNow - _lastCountReset > CountResetInterval)
            {
                _boatsPerResource.Clear();
                _totalBoats = 0;
                _lastCountReset = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cleaning up error boats");
        }
    }
}
