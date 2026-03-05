using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Spawns minecarts carrying creeper passengers when resources transition to Unhealthy.
/// Minecarts spawn on powered rails behind the erroring building and ride the rail network
/// through curved junctions to the trunk canal and into the lake.
/// Anti-pileup caps limit per-resource and global minecart counts.
/// Requires CanalService to have built canals (with rails) before spawning.
/// </summary>
internal sealed class ErrorBoatService(
    RconService rcon,
    AspireResourceMonitor monitor,
    ILogger<ErrorBoatService> logger,
    CanalService? canals = null)
{
    private const int MaxCartsPerResource = 3;
    private const int MaxTotalCarts = 20;
    private static readonly TimeSpan SpawnCooldown = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CountResetInterval = TimeSpan.FromSeconds(30);

    private readonly Dictionary<string, int> _cartsPerResource = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _lastSpawnTime = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ResourceStatusChange> _pendingChanges = new();
    private readonly object _lock = new();
    private int _totalCarts;
    private DateTime _lastCountReset = DateTime.UtcNow;

    /// <summary>
    /// One-time initialization. Records initial state (currently a no-op).
    /// </summary>
    public Task InitializeAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Error minecart service initialized");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Spawns an error minecart for a specific resource triggered by an OpenTelemetry error-level log entry.
    /// Applies the same rate limiting and canal readiness gating as health-based spawns.
    /// Thread-safe: may be called from the HTTP endpoint concurrently with the worker loop.
    /// </summary>
    public async Task SpawnBoatForErrorLogAsync(string resourceName, CancellationToken ct = default)
    {
        // Gate on canal readiness
        if (canals is null || canals.CanalPositions.Count == 0)
        {
            lock (_lock)
            {
                logger.LogInformation("Buffering error minecart for {Resource} (canals not ready)", resourceName);
                _pendingChanges.Add(new ResourceStatusChange(resourceName, "unknown", ResourceStatus.Healthy, ResourceStatus.Unhealthy));
            }
            return;
        }

        await SpawnCartCoreAsync(resourceName, ct);
    }

    /// <summary>
    /// Spawns error minecarts for resources that transitioned to Unhealthy.
    /// Minecarts are only spawned when canals have been built (CanalPositions populated).
    /// If canals aren't ready yet, changes are buffered and replayed when canals become available.
    /// </summary>
    public async Task SpawnBoatsForChangesAsync(IReadOnlyList<ResourceStatusChange> changes, CancellationToken ct = default)
    {
        List<ResourceStatusChange> allChanges;

        lock (_lock)
        {
            if (changes.Count == 0 && _pendingChanges.Count == 0) return;

            // Gate on canal readiness — if canals aren't built yet, buffer the changes for later
            if (canals is null || canals.CanalPositions.Count == 0)
            {
                foreach (var change in changes)
                {
                    if (change.NewStatus == ResourceStatus.Unhealthy)
                    {
                        if (!_pendingChanges.Any(pc => pc.Name.Equals(change.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            _pendingChanges.Add(change);
                            logger.LogInformation("Buffering error minecart spawn for {Resource} (canals not ready yet)", change.Name);
                        }
                    }
                }
                return;
            }

            // Canals are ready! Snapshot both new changes and any buffered changes, then clear buffer
            allChanges = new List<ResourceStatusChange>(_pendingChanges);
            allChanges.AddRange(changes);
            _pendingChanges.Clear();
        }

        foreach (var change in allChanges)
        {
            if (change.NewStatus != ResourceStatus.Unhealthy) continue;
            await SpawnCartCoreAsync(change.Name, ct);
        }
    }

    private async Task SpawnCartCoreAsync(string resourceName, CancellationToken ct)
    {
        var orderedNames = VillageLayout.ReorderByDependency(monitor.Resources);
        var index = orderedNames.IndexOf(resourceName);
        if (index < 0)
        {
            logger.LogWarning("Cannot spawn error minecart: resource '{Resource}' not found in ordered names [{Names}]",
                resourceName, string.Join(", ", orderedNames));
            return;
        }

        // Per-resource cooldown
        if (_lastSpawnTime.TryGetValue(resourceName, out var lastSpawn)
            && DateTime.UtcNow - lastSpawn < SpawnCooldown)
        {
            logger.LogDebug("Error minecart cooldown active for {Resource}", resourceName);
            return;
        }

        // Per-resource cap
        _cartsPerResource.TryGetValue(resourceName, out var resourceCarts);
        if (resourceCarts >= MaxCartsPerResource)
        {
            logger.LogDebug("Error minecart per-resource cap reached for {Resource} ({Count}/{Max})",
                resourceName, resourceCarts, MaxCartsPerResource);
            return;
        }

        // Global cap
        if (_totalCarts >= MaxTotalCarts)
        {
            logger.LogDebug("Error minecart global cap reached ({Count}/{Max})", _totalCarts, MaxTotalCarts);
            return;
        }

        var (cx, cy, cz) = VillageLayout.GetCanalEntrance(resourceName, index);

        // Spawn minecart with initial westward Motion so it rides the E-W branch rail
        // to the N-S trunk line and then south to the lake. Powered rails on flat ground
        // do NOT push stationary carts — they only accelerate existing motion.
        // PersistenceRequired prevents distance-based despawning of the creeper passenger.
        var cmd = $"summon minecraft:minecart {cx} {cy} {cz} " +
            $"{{Motion:[-1.0d,0.0d,0.0d],Passengers:[{{id:\"minecraft:creeper\",NoAI:1b,Silent:1b,PersistenceRequired:1b,Tags:[\"error_creeper\"]}}],Tags:[\"error_cart\"]}}";
        logger.LogInformation("Summoning error minecart: {Command}", cmd);
        await rcon.SendCommandAsync(cmd, CommandPriority.Normal, ct);

        _cartsPerResource[resourceName] = resourceCarts + 1;
        _totalCarts++;
        _lastSpawnTime[resourceName] = DateTime.UtcNow;

        logger.LogInformation("Error minecart spawned at ({X},{Y},{Z}) for {Resource}",
            cx, cy, cz, resourceName);
    }

    /// <summary>
    /// Minecarts follow rails autonomously including curves — no redirect logic needed.
    /// This method is kept as a no-op stub to preserve the call site in the worker loop.
    /// </summary>
    public Task MoveBoatsAsync(CancellationToken ct = default) => Task.CompletedTask;

    /// <summary>
    /// Despawns minecarts near the lake and periodically resets tracking counters.
    /// </summary>
    public async Task CleanupBoatsAsync(CancellationToken ct = default)
    {
        if (_totalCarts <= 0 && _cartsPerResource.Count == 0) return;

        try
        {
            var resourceCount = monitor.Resources.Count;
            if (resourceCount == 0) return;

            var (lakeX, _, lakeZ) = VillageLayout.GetLakePosition(resourceCount);
            var lakeCenterX = lakeX + VillageLayout.LakeWidth / 2;
            var lakeCenterZ = lakeZ + VillageLayout.LakeLength / 2;

            // Kill error minecarts near the lake
            await rcon.SendCommandAsync(
                $"kill @e[type=minecraft:minecart,tag=error_cart,x={lakeCenterX},y={VillageLayout.SurfaceY},z={lakeCenterZ},distance=..15]",
                CommandPriority.Low, ct);

            // Clean up orphaned NoAI creepers ejected when minecarts despawn
            await rcon.SendCommandAsync(
                $"kill @e[type=minecraft:creeper,tag=error_creeper,x={lakeCenterX},y={VillageLayout.SurfaceY},z={lakeCenterZ},distance=..200]",
                CommandPriority.Low, ct);

            // Periodically reset counters to avoid stale tracking
            if (DateTime.UtcNow - _lastCountReset > CountResetInterval)
            {
                _cartsPerResource.Clear();
                _totalCarts = 0;
                _lastCountReset = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cleaning up error minecarts");
        }
    }
}
