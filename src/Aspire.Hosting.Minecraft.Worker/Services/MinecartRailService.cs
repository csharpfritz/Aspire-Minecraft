using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Builds powered minecart rail connections between dependent Aspire resources.
/// Draws L-shaped rail paths with powered rails every 8 blocks, detector rails at stations,
/// and spawns chest minecarts for visual cargo transport. Health-reactive: disables powered
/// rails when a parent resource goes unhealthy and restores them on recovery.
/// Called by MinecraftWorldWorker after RCON is connected and resources are discovered.
/// </summary>
internal sealed class MinecartRailService(
    RconService rcon,
    AspireResourceMonitor monitor,
    ILogger<MinecartRailService> logger,
    CanalService? canals = null)
{
    private bool _railsBuilt;
    private readonly Dictionary<string, bool> _connectionState = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ResourceStatus> _lastKnownStatus = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Represents a rail connection between two dependent resources.
    /// </summary>
    private record RailConnection(
        string FromResource,
        string ToResource,
        List<(int x, int y, int z)> RailPositions,
        List<(int x, int y, int z)> PoweredRailPositions,
        (int x, int y, int z) StartPos,
        (int x, int y, int z) EndPos);

    private readonly List<RailConnection> _connections = new();

    /// <summary>
    /// One-time initialization: builds the rail network. Called after DiscoverResources.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_railsBuilt) return;

        logger.LogInformation("Minecart rail service initializing rails...");
        await BuildRailNetworkAsync(ct);
        _railsBuilt = true;
        logger.LogInformation("Minecart rail service initialized");
    }

    /// <summary>
    /// Updates rail health state based on current resource status. Called each worker cycle.
    /// </summary>
    public async Task UpdateAsync(CancellationToken ct = default)
    {
        try
        {
            await UpdateRailHealthAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating minecart rail network");
        }
    }

    private async Task BuildRailNetworkAsync(CancellationToken ct)
    {
        var resources = monitor.Resources;
        var orderedNames = VillageLayout.ReorderByDependency(resources);
        var nameToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < orderedNames.Count; i++)
            nameToIndex[orderedNames[i]] = i;

        foreach (var (name, info) in resources)
        {
            if (info.Dependencies.Count == 0) continue;
            if (!nameToIndex.TryGetValue(name, out var childIndex)) continue;

            foreach (var dep in info.Dependencies)
            {
                var depLower = dep.ToLowerInvariant();
                if (!nameToIndex.TryGetValue(depLower, out var parentIndex)) continue;

                var connection = CalculateRailConnection(name, depLower, childIndex, parentIndex);
                _connections.Add(connection);
                _connectionState[$"{depLower}->{name}"] = true;

                await PlaceRailConnectionAsync(connection, ct);
                logger.LogInformation("Minecart rail placed: {Parent} -> {Child}", depLower, name);
            }
        }

        logger.LogInformation("Minecart rail network built with {Count} connections", _connections.Count);
    }

    private static RailConnection CalculateRailConnection(string childName, string parentName, int childIndex, int parentIndex)
    {
        var (px, _, pz) = VillageLayout.GetStructureOrigin(parentIndex);
        var (cx, _, cz) = VillageLayout.GetStructureOrigin(childIndex);

        var railY = VillageLayout.SurfaceY + 1;
        var entranceOffset = VillageLayout.StructureSize / 2;

        // Start/end at entrance X, z - 2 (one block further out than redstone at z - 1)
        var startX = px + entranceOffset;
        var startZ = pz - 2;
        var endX = cx + entranceOffset;
        var endZ = cz - 2;

        var startPos = (startX, railY, startZ);
        var endPos = (endX, railY, endZ);

        // L-shaped path: X first, then Z
        var railPositions = new List<(int x, int y, int z)>();
        var poweredRailPositions = new List<(int x, int y, int z)>();

        var dx = startX < endX ? 1 : -1;
        var dz = startZ < endZ ? 1 : -1;
        var blockCount = 0;

        // Phase 1: walk along X axis
        var currentX = startX;
        while (currentX != endX)
        {
            var pos = (currentX, railY, startZ);
            railPositions.Add(pos);

            if (blockCount > 0 && blockCount % 8 == 0)
                poweredRailPositions.Add(pos);

            blockCount++;
            currentX += dx;
        }

        // Phase 2: walk along Z axis
        var currentZ = startZ;
        while (currentZ != endZ)
        {
            var pos = (endX, railY, currentZ);
            railPositions.Add(pos);

            if (blockCount > 0 && blockCount % 8 == 0)
                poweredRailPositions.Add(pos);

            blockCount++;
            currentZ += dz;
        }

        // Add the final position
        railPositions.Add((endX, railY, endZ));

        return new RailConnection(parentName, childName, railPositions, poweredRailPositions, startPos, endPos);
    }

    private async Task PlaceRailConnectionAsync(RailConnection connection, CancellationToken ct)
    {
        var railY = connection.StartPos.y;

        // Place station at start: detector_rail → powered_rail → detector_rail
        await PlaceStationAsync(connection.StartPos.x, railY, connection.StartPos.z, ct);

        // Place station at end
        await PlaceStationAsync(connection.EndPos.x, railY, connection.EndPos.z, ct);

        // Place rails along the path (skip station endpoints)
        for (var i = 0; i < connection.RailPositions.Count; i++)
        {
            var (x, y, z) = connection.RailPositions[i];

            // Skip positions that are part of station areas
            if (IsStationPosition(x, y, z, connection))
                continue;

            // Bridge over canal: place stone brick slab and support pillar under the rail
            if (canals?.CanalPositions.Contains((x, z)) == true)
            {
                await rcon.SendCommandAsync(
                    $"setblock {x} {y - 1} {z} minecraft:stone_brick_slab[type=top]",
                    CommandPriority.Low, ct);
                await rcon.SendCommandAsync(
                    $"setblock {x} {y - 2} {z} minecraft:stone_bricks",
                    CommandPriority.Low, ct);
            }

            if (connection.PoweredRailPositions.Contains((x, y, z)))
            {
                await rcon.SendCommandAsync(
                    $"setblock {x} {y} {z} minecraft:powered_rail",
                    CommandPriority.Low, ct);
                // Redstone torch underneath to power the rail
                if (canals?.CanalPositions.Contains((x, z)) != true)
                {
                    await rcon.SendCommandAsync(
                        $"setblock {x} {y - 1} {z} minecraft:redstone_torch",
                        CommandPriority.Low, ct);
                }
            }
            else
            {
                await rcon.SendCommandAsync(
                    $"setblock {x} {y} {z} minecraft:rail",
                    CommandPriority.Low, ct);
            }
        }

        // Spawn a chest minecart at the start position
        await rcon.SendCommandAsync(
            $"summon minecraft:chest_minecart {connection.StartPos.x} {railY} {connection.StartPos.z}",
            CommandPriority.Low, ct);
    }

    private async Task PlaceStationAsync(int x, int y, int z, CancellationToken ct)
    {
        // Station: detector_rail, powered_rail, detector_rail (along Z)
        await rcon.SendCommandAsync(
            $"setblock {x} {y} {z} minecraft:detector_rail",
            CommandPriority.Low, ct);
        await rcon.SendCommandAsync(
            $"setblock {x} {y} {z + 1} minecraft:powered_rail",
            CommandPriority.Low, ct);
        await rcon.SendCommandAsync(
            $"setblock {x} {y - 1} {z + 1} minecraft:redstone_torch",
            CommandPriority.Low, ct);
        await rcon.SendCommandAsync(
            $"setblock {x} {y} {z + 2} minecraft:detector_rail",
            CommandPriority.Low, ct);
    }

    private static bool IsStationPosition(int x, int y, int z, RailConnection connection)
    {
        // Check start station area (z, z+1, z+2)
        if (x == connection.StartPos.x && y == connection.StartPos.y &&
            z >= connection.StartPos.z && z <= connection.StartPos.z + 2)
            return true;

        // Check end station area
        if (x == connection.EndPos.x && y == connection.EndPos.y &&
            z >= connection.EndPos.z && z <= connection.EndPos.z + 2)
            return true;

        return false;
    }

    private async Task UpdateRailHealthAsync(CancellationToken ct)
    {
        foreach (var (name, info) in monitor.Resources)
        {
            var currentStatus = info.Status;
            _lastKnownStatus.TryGetValue(name, out var lastStatus);

            if (currentStatus == lastStatus) continue;
            _lastKnownStatus[name] = currentStatus;

            // Find all outgoing connections from this resource
            var affectedConnections = _connections.Where(c =>
                c.FromResource.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var connection in affectedConnections)
            {
                var key = $"{connection.FromResource}->{connection.ToResource}";

                if (currentStatus == ResourceStatus.Unhealthy)
                {
                    if (_connectionState.TryGetValue(key, out var connected) && connected)
                    {
                        await DisableRailsAsync(connection, ct);
                        _connectionState[key] = false;
                        logger.LogInformation("Minecart rails disabled: {Parent} -> {Child} (unhealthy)",
                            connection.FromResource, connection.ToResource);
                    }
                }
                else if (currentStatus == ResourceStatus.Healthy)
                {
                    if (_connectionState.TryGetValue(key, out var connected) && !connected)
                    {
                        await RestoreRailsAsync(connection, ct);
                        _connectionState[key] = true;
                        logger.LogInformation("Minecart rails restored: {Parent} -> {Child} (healthy)",
                            connection.FromResource, connection.ToResource);
                    }
                }
            }
        }
    }

    private async Task DisableRailsAsync(RailConnection connection, CancellationToken ct)
    {
        // Replace powered rails with air so minecart stops
        foreach (var (x, y, z) in connection.PoweredRailPositions)
        {
            await rcon.SendCommandAsync(
                $"setblock {x} {y} {z} minecraft:air",
                CommandPriority.Normal, ct);
        }
    }

    private async Task RestoreRailsAsync(RailConnection connection, CancellationToken ct)
    {
        // Restore powered rails so minecart resumes
        foreach (var (x, y, z) in connection.PoweredRailPositions)
        {
            await rcon.SendCommandAsync(
                $"setblock {x} {y} {z} minecraft:powered_rail",
                CommandPriority.Normal, ct);
        }
    }
}
