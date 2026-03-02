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
    /// </summary>
    public async Task SpawnBoatForErrorLogAsync(string resourceName, CancellationToken ct = default)
    {
        // Gate on canal readiness
        if (canals is null || canals.CanalPositions.Count == 0)
        {
            logger.LogInformation("Buffering error minecart for {Resource} (canals not ready)", resourceName);
            _pendingChanges.Add(new ResourceStatusChange(resourceName, "unknown", ResourceStatus.Healthy, ResourceStatus.Unhealthy));
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
        if (changes.Count == 0) return;

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

        // Canals are ready! Process both new changes and any buffered changes
        var allChanges = new List<ResourceStatusChange>(_pendingChanges);
        allChanges.AddRange(changes);
        _pendingChanges.Clear();

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
        if (index < 0) return;

        // Per-resource cooldown
        if (_lastSpawnTime.TryGetValue(resourceName, out var lastSpawn)
            && DateTime.UtcNow - lastSpawn < SpawnCooldown)
            return;

        // Per-resource cap
        _cartsPerResource.TryGetValue(resourceName, out var resourceCarts);
        if (resourceCarts >= MaxCartsPerResource) return;

        // Global cap
        if (_totalCarts >= MaxTotalCarts) return;

        var (cx, cy, cz) = VillageLayout.GetCanalEntrance(resourceName, index);

        // Spawn minecart on the powered rail — rails provide propulsion, no Motion needed.
        // Creeper passenger rides through all curves including the trunk junction.
        await rcon.SendCommandAsync(
            $"summon minecraft:minecart {cx} {cy} {cz} {{Passengers:[{{id:\"minecraft:creeper\",NoAI:1b,Silent:1b}}],Tags:[\"error_cart\"]}}",
            CommandPriority.Normal, ct);

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
                $"kill @e[type=minecraft:creeper,nbt={{NoAI:1b,Silent:1b}},x={lakeCenterX},y={VillageLayout.SurfaceY},z={lakeCenterZ},distance=..200]",
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
