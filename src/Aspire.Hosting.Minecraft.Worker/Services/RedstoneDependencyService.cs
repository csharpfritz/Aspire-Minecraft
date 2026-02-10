using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Visualizes Aspire resource dependencies as redstone wire circuits in the Minecraft world.
/// Draws L-shaped redstone paths between dependent structures, with repeaters every 15 blocks
/// and redstone lamps at structure entrances. Health-reactive: breaks circuits when resources
/// go unhealthy and restores them on recovery.
/// </summary>
internal sealed class RedstoneDependencyService(
    RconService rcon,
    AspireResourceMonitor monitor,
    ILogger<RedstoneDependencyService> logger) : BackgroundService
{
    private bool _wiresBuilt;
    private readonly Dictionary<string, bool> _connectionState = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ResourceStatus> _lastKnownStatus = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Represents a redstone wire segment between two dependent resources.
    /// </summary>
    private record WireSegment(string FromResource, string ToResource, List<(int x, int y, int z)> WirePositions, List<(int x, int y, int z, string facing)> RepeaterPositions, (int x, int y, int z) FromLamp, (int x, int y, int z) ToLamp);

    private readonly List<WireSegment> _segments = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Redstone dependency service starting, waiting for resources...");

        // Wait until resources are discovered and structures are built
        while (monitor.TotalCount == 0 && !stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }

        // Extra delay to let structures finish building
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        logger.LogInformation("Redstone dependency service active");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_wiresBuilt)
                {
                    await BuildDependencyWiresAsync(stoppingToken);
                    _wiresBuilt = true;
                }

                await UpdateWireHealthAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in redstone dependency loop");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        logger.LogInformation("Redstone dependency service stopping");
    }

    private async Task BuildDependencyWiresAsync(CancellationToken ct)
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

                var segment = CalculateWireSegment(name, depLower, childIndex, parentIndex);
                _segments.Add(segment);
                _connectionState[$"{depLower}->{name}"] = true;

                await PlaceWireSegmentAsync(segment, ct);
                logger.LogInformation("Redstone wire placed: {Parent} -> {Child}", depLower, name);
            }
        }

        logger.LogInformation("Redstone dependency graph built with {Count} connections", _segments.Count);
    }

    private static WireSegment CalculateWireSegment(string childName, string parentName, int childIndex, int parentIndex)
    {
        var (px, py, pz) = VillageLayout.GetStructureOrigin(parentIndex);
        var (cx, cy, cz) = VillageLayout.GetStructureOrigin(childIndex);

        // Wire runs at ground level (BaseY) offset by -1 in Z (in front of structures)
        var wireY = VillageLayout.BaseY;

        // Start point: in front of parent structure entrance (center X, Z-1)
        var startX = px + 3;
        var startZ = pz - 1;

        // End point: in front of child structure entrance
        var endX = cx + 3;
        var endZ = cz - 1;

        // Lamp positions: at structure entrance wall, eye level
        var parentLamp = (px + 3, wireY + 2, pz - 1);
        var childLamp = (cx + 3, wireY + 2, cz - 1);

        // L-shaped path: go along X first, then Z
        var wirePositions = new List<(int x, int y, int z)>();
        var repeaterPositions = new List<(int x, int y, int z, string facing)>();

        var blockCount = 0;
        var dx = startX < endX ? 1 : -1;
        var dz = startZ < endZ ? 1 : -1;

        // Phase 1: walk along X axis
        var currentX = startX;
        while (currentX != endX)
        {
            wirePositions.Add((currentX, wireY, startZ));
            blockCount++;

            if (blockCount % 15 == 0)
            {
                var facing = dx > 0 ? "east" : "west";
                repeaterPositions.Add((currentX, wireY, startZ, facing));
            }

            currentX += dx;
        }

        // Phase 2: walk along Z axis
        var currentZ = startZ;
        while (currentZ != endZ)
        {
            wirePositions.Add((endX, wireY, currentZ));
            blockCount++;

            if (blockCount % 15 == 0)
            {
                var facing = dz > 0 ? "south" : "north";
                repeaterPositions.Add((endX, wireY, currentZ, facing));
            }

            currentZ += dz;
        }

        // Add the final position
        wirePositions.Add((endX, wireY, endZ));

        return new WireSegment(parentName, childName, wirePositions, repeaterPositions, parentLamp, childLamp);
    }

    private async Task PlaceWireSegmentAsync(WireSegment segment, CancellationToken ct)
    {
        // Place lamps at structure entrances
        await rcon.SendCommandAsync(
            $"setblock {segment.FromLamp.x} {segment.FromLamp.y} {segment.FromLamp.z} minecraft:redstone_lamp",
            CommandPriority.Low, ct);
        await rcon.SendCommandAsync(
            $"setblock {segment.ToLamp.x} {segment.ToLamp.y} {segment.ToLamp.z} minecraft:redstone_lamp",
            CommandPriority.Low, ct);

        // Place redstone wire along the path
        for (var i = 0; i < segment.WirePositions.Count; i++)
        {
            var (x, y, z) = segment.WirePositions[i];

            // Check if this position has a repeater instead
            var isRepeater = false;
            foreach (var (rx, ry, rz, facing) in segment.RepeaterPositions)
            {
                if (rx == x && ry == y && rz == z)
                {
                    await rcon.SendCommandAsync(
                        $"setblock {x} {y} {z} minecraft:repeater[facing={facing}]",
                        CommandPriority.Low, ct);
                    isRepeater = true;
                    break;
                }
            }

            if (!isRepeater)
            {
                await rcon.SendCommandAsync(
                    $"setblock {x} {y} {z} minecraft:redstone_wire",
                    CommandPriority.Low, ct);
            }
        }

        // Place a redstone block next to parent lamp to power the circuit
        await rcon.SendCommandAsync(
            $"setblock {segment.FromLamp.x} {segment.FromLamp.y} {segment.FromLamp.z + 1} minecraft:redstone_block",
            CommandPriority.Low, ct);
    }

    private async Task UpdateWireHealthAsync(CancellationToken ct)
    {
        foreach (var (name, info) in monitor.Resources)
        {
            var currentStatus = info.Status;
            _lastKnownStatus.TryGetValue(name, out var lastStatus);

            if (currentStatus == lastStatus) continue;
            _lastKnownStatus[name] = currentStatus;

            // Find all outgoing connections from this resource
            var affectedSegments = _segments.Where(s =>
                s.FromResource.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var segment in affectedSegments)
            {
                var key = $"{segment.FromResource}->{segment.ToResource}";

                if (currentStatus == ResourceStatus.Unhealthy)
                {
                    if (_connectionState.TryGetValue(key, out var connected) && connected)
                    {
                        await BreakCircuitAsync(segment, ct);
                        _connectionState[key] = false;
                        logger.LogInformation("Redstone circuit broken: {Parent} -> {Child} (unhealthy)",
                            segment.FromResource, segment.ToResource);
                    }
                }
                else if (currentStatus == ResourceStatus.Healthy)
                {
                    if (_connectionState.TryGetValue(key, out var connected) && !connected)
                    {
                        await RestoreCircuitAsync(segment, ct);
                        _connectionState[key] = true;
                        logger.LogInformation("Redstone circuit restored: {Parent} -> {Child} (healthy)",
                            segment.FromResource, segment.ToResource);
                    }
                }
            }
        }
    }

    private async Task BreakCircuitAsync(WireSegment segment, CancellationToken ct)
    {
        // Remove the redstone block powering the circuit
        await rcon.SendCommandAsync(
            $"setblock {segment.FromLamp.x} {segment.FromLamp.y} {segment.FromLamp.z + 1} minecraft:air",
            CommandPriority.Normal, ct);

        // Break wire at several points to visually show disconnection
        for (var i = 0; i < segment.WirePositions.Count; i += 5)
        {
            var (x, y, z) = segment.WirePositions[i];
            await rcon.SendCommandAsync(
                $"setblock {x} {y} {z} minecraft:air",
                CommandPriority.Low, ct);
        }
    }

    private async Task RestoreCircuitAsync(WireSegment segment, CancellationToken ct)
    {
        // Restore broken wire positions
        for (var i = 0; i < segment.WirePositions.Count; i += 5)
        {
            var (x, y, z) = segment.WirePositions[i];

            // Check if this should be a repeater
            var isRepeater = false;
            foreach (var (rx, ry, rz, facing) in segment.RepeaterPositions)
            {
                if (rx == x && ry == y && rz == z)
                {
                    await rcon.SendCommandAsync(
                        $"setblock {x} {y} {z} minecraft:repeater[facing={facing}]",
                        CommandPriority.Low, ct);
                    isRepeater = true;
                    break;
                }
            }

            if (!isRepeater)
            {
                await rcon.SendCommandAsync(
                    $"setblock {x} {y} {z} minecraft:redstone_wire",
                    CommandPriority.Low, ct);
            }
        }

        // Restore the redstone block powering the circuit
        await rcon.SendCommandAsync(
            $"setblock {segment.FromLamp.x} {segment.FromLamp.y} {segment.FromLamp.z + 1} minecraft:redstone_block",
            CommandPriority.Normal, ct);
    }
}
