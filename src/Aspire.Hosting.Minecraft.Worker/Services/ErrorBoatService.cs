using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Spawns boats carrying creeper passengers when resources transition to Unhealthy.
/// Boats appear at the resource's canal entrance and float toward the shared lake on blue ice.
/// Anti-pileup caps limit per-resource and global boat counts.
/// </summary>
internal sealed class ErrorBoatService(
    RconService rcon,
    AspireResourceMonitor monitor,
    ILogger<ErrorBoatService> logger)
{
    private const int MaxBoatsPerResource = 3;
    private const int MaxTotalBoats = 20;
    private static readonly TimeSpan SpawnCooldown = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CountResetInterval = TimeSpan.FromSeconds(30);

    private readonly Dictionary<string, int> _boatsPerResource = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _lastSpawnTime = new(StringComparer.OrdinalIgnoreCase);
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
    /// </summary>
    public async Task SpawnBoatsForChangesAsync(IReadOnlyList<ResourceStatusChange> changes, CancellationToken ct = default)
    {
        if (changes.Count == 0) return;

        var orderedNames = VillageLayout.ReorderByDependency(monitor.Resources);

        foreach (var change in changes)
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

            var (cx, _, cz) = VillageLayout.GetCanalEntrance(index);
            var spawnY = VillageLayout.SurfaceY;

            await rcon.SendCommandAsync(
                $"summon minecraft:oak_boat {cx} {spawnY} {cz} {{Passengers:[{{id:\"minecraft:creeper\",NoAI:1b,Silent:1b}}]}}",
                CommandPriority.Normal, ct);

            _boatsPerResource[change.Name] = resourceBoats + 1;
            _totalBoats++;
            _lastSpawnTime[change.Name] = DateTime.UtcNow;

            logger.LogInformation("Error boat spawned at ({X},{Y},{Z}) for {Resource}",
                cx, spawnY, cz, change.Name);
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
