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

        var orderedNames = VillageLayout.ReorderByDependency(monitor.Resources);

        foreach (var change in allChanges)
        {
            if (change.NewStatus != ResourceStatus.Unhealthy) continue;

            var index = orderedNames.IndexOf(change.Name);
            if (index < 0) continue;

            // Per-resource cooldown
            if (_lastSpawnTime.TryGetValue(change.Name, out var lastSpawn)
                && DateTime.UtcNow - lastSpawn < SpawnCooldown)
                continue;

            // Per-resource cap
            _boatsPerResource.TryGetValue(change.Name, out var resourceBoats);
            if (resourceBoats >= MaxBoatsPerResource) continue;

            // Global cap
            if (_totalBoats >= MaxTotalBoats) continue;

            var (cx, cy, cz) = VillageLayout.GetCanalEntrance(change.Name, index);

            // Summon with westward motion toward the trunk canal; blue ice floor keeps them sliding
            await rcon.SendCommandAsync(
                $"summon minecraft:oak_boat {cx} {cy} {cz} {{Motion:[-0.5,0.0,0.0],Passengers:[{{id:\"minecraft:creeper\",NoAI:1b,Silent:1b}}]}}",
                CommandPriority.Normal, ct);

            _boatsPerResource[change.Name] = resourceBoats + 1;
            _totalBoats++;
            _lastSpawnTime[change.Name] = DateTime.UtcNow;

            logger.LogInformation("Error boat spawned at ({X},{Y},{Z}) for {Resource}",
                cx, cy, cz, change.Name);
        }
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

            await rcon.SendCommandAsync(
                $"kill @e[type=minecraft:boat,x={lakeCenterX},y={VillageLayout.SurfaceY},z={lakeCenterZ},distance=..15]",
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
