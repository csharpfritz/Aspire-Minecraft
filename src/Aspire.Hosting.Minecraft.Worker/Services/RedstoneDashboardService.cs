using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Builds and maintains a physical Dashboard Wall west of the village.
/// Displays resource health over time as a scrolling grid of self-luminous blocks.
/// Each row represents a resource; each column represents a time slot (oldest left, newest right).
/// Glowstone = healthy, redstone_lamp (unlit) = unhealthy, sea lantern = unknown.
/// Repaints all columns from HealthHistoryTracker each update cycle (oldest left, newest right).
/// </summary>
internal sealed class RedstoneDashboardService(
    RconService rcon,
    AspireResourceMonitor monitor,
    HealthHistoryTracker tracker,
    ILogger<RedstoneDashboardService> logger)
{
    private bool _initialized;
    private List<string> _resourceOrder = new();
    private int _rows;
    private int _columns;

    /// <summary>
    /// One-time initialization: builds the dashboard wall frame, lamp grid, and row labels.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;

        var resources = monitor.Resources;
        if (resources.Count == 0)
        {
            logger.LogWarning("No resources discovered — skipping dashboard build");
            return;
        }

        _resourceOrder = VillageLayout.ReorderByDependency(resources);
        (_rows, _columns) = GetDashboardDimensions(_resourceOrder.Count);

        logger.LogInformation("Building redstone dashboard: {Rows} rows × {Columns} columns", _rows, _columns);

        await BuildFrameAsync(ct);
        await BuildLampGridAsync(ct);
        await PlaceLabelsAsync(ct);

        _initialized = true;
        logger.LogInformation("Redstone dashboard initialized");
    }

    /// <summary>
    /// Records current health into the ring buffer and repaints all columns from history.
    /// </summary>
    public async Task UpdateAsync(CancellationToken ct = default)
    {
        if (!_initialized) return;

        try
        {
            // Record current health snapshot into ring buffer
            foreach (var name in _resourceOrder)
            {
                if (monitor.Resources.TryGetValue(name, out var info))
                {
                    tracker.Record(name, info.Status);
                }
            }

            // Repaint all columns from history (oldest left, newest right)
            await RepaintAllColumnsAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating redstone dashboard");
        }
    }

    /// <summary>
    /// Returns scaled grid dimensions based on resource count.
    /// </summary>
    public static (int rows, int columns) GetDashboardDimensions(int resourceCount)
    {
        var rows = Math.Min(resourceCount, 30);
        var columns = resourceCount switch
        {
            <= 8 => 10,
            <= 16 => 8,
            _ => 6
        };
        return (rows, columns);
    }

    private async Task BuildFrameAsync(CancellationToken ct)
    {
        var x = VillageLayout.DashboardX;
        var y = VillageLayout.SurfaceY + 2;
        var z = VillageLayout.BaseZ;

        // Wall height: rows separated by stone dividers = rows * 2 - 1, plus 2 for frame top/bottom
        var wallHeight = (_rows * 2 - 1) + 2;
        // Wall width: columns + 2 for frame left/right + 1 for sign column
        var wallWidth = _columns + 2;

        // Solid back wall (stone bricks) — visual backing behind lamp grid
        await rcon.SendCommandAsync(
            $"fill {x - 1} {y - 1} {z} {x - 1} {y - 1 + wallHeight - 1} {z + wallWidth - 1} minecraft:stone_bricks",
            CommandPriority.Low, ct);

        // Front frame (stone bricks border)
        // Bottom edge
        await rcon.SendCommandAsync(
            $"fill {x} {y - 1} {z} {x} {y - 1} {z + wallWidth - 1} minecraft:stone_bricks",
            CommandPriority.Low, ct);
        // Top edge
        await rcon.SendCommandAsync(
            $"fill {x} {y - 1 + wallHeight - 1} {z} {x} {y - 1 + wallHeight - 1} {z + wallWidth - 1} minecraft:stone_bricks",
            CommandPriority.Low, ct);
        // Left edge
        await rcon.SendCommandAsync(
            $"fill {x} {y - 1} {z} {x} {y - 1 + wallHeight - 1} {z} minecraft:stone_bricks",
            CommandPriority.Low, ct);
        // Right edge
        await rcon.SendCommandAsync(
            $"fill {x} {y - 1} {z + wallWidth - 1} {x} {y - 1 + wallHeight - 1} {z + wallWidth - 1} minecraft:stone_bricks",
            CommandPriority.Low, ct);

        // Stone dividers between lamp rows (fill horizontal rows of stone between lamps)
        for (var r = 0; r < _rows - 1; r++)
        {
            var dividerY = y + (r * 2) + 1;
            await rcon.SendCommandAsync(
                $"fill {x} {dividerY} {z + 1} {x} {dividerY} {z + _columns} minecraft:stone_bricks",
                CommandPriority.Low, ct);
        }
    }

    private async Task BuildLampGridAsync(CancellationToken ct)
    {
        var x = VillageLayout.DashboardX;
        var y = VillageLayout.SurfaceY + 2;
        var z = VillageLayout.BaseZ;

        // Place lamps in a grid: each row at y + (r * 2), each column at z + c + 1
        // All lamps start as unlit redstone_lamp (dark = no data yet)
        for (var r = 0; r < _rows; r++)
        {
            var lampY = y + (r * 2);
            var lampZStart = z + 1;
            var lampZEnd = z + _columns;

            await rcon.SendCommandAsync(
                $"fill {x} {lampY} {lampZStart} {x} {lampY} {lampZEnd} minecraft:redstone_lamp",
                CommandPriority.Low, ct);
        }
    }

    private async Task PlaceLabelsAsync(CancellationToken ct)
    {
        var x = VillageLayout.DashboardX;
        var y = VillageLayout.SurfaceY + 2;
        var z = VillageLayout.BaseZ;

        // Title sign above the wall
        var titleY = y + (_rows * 2 - 1) + 1;
        await rcon.SendCommandAsync(
            $"setblock {x} {titleY} {z + 1} minecraft:oak_wall_sign[facing=east]",
            CommandPriority.Low, ct);
        await rcon.SendCommandAsync(
            "data merge block " + $"{x} {titleY} {z + 1}" +
            " {front_text:{messages:[\"\"," +
            "\"\\\"§lHealth Dashboard\\\"\"," +
            "\"\",\"\"]}}",
            CommandPriority.Low, ct);

        // Row label signs (one per resource, at z = BaseZ, the left frame edge)
        for (var r = 0; r < _rows; r++)
        {
            var lampY = y + (r * 2);
            var resourceName = r < _resourceOrder.Count ? _resourceOrder[r] : "?";

            await rcon.SendCommandAsync(
                $"setblock {x} {lampY} {z} minecraft:oak_wall_sign[facing=east]",
                CommandPriority.Low, ct);
            await rcon.SendCommandAsync(
                "data merge block " + $"{x} {lampY} {z}" +
                " {front_text:{messages:[\"\"," +
                $"\"\\\"{resourceName}\\\"\"," +
                "\"\",\"\"]}}",
                CommandPriority.Low, ct);
        }
    }

    private async Task RepaintAllColumnsAsync(CancellationToken ct)
    {
        var x = VillageLayout.DashboardX;
        var y = VillageLayout.SurfaceY + 2;
        var z = VillageLayout.BaseZ;

        for (var r = 0; r < _rows; r++)
        {
            var lampY = y + (r * 2);
            var resourceName = _resourceOrder[r];
            var history = tracker.GetHistory(resourceName);

            // Write each column from left (oldest) to right (newest)
            // Pad with empty (redstone_lamp) if history is shorter than columns
            for (var c = 0; c < _columns; c++)
            {
                var historyIndex = c - (_columns - history.Count);
                var block = historyIndex >= 0 && historyIndex < history.Count
                    ? history[historyIndex] switch
                    {
                        ResourceStatus.Healthy => "minecraft:glowstone",
                        ResourceStatus.Unhealthy => "minecraft:redstone_lamp",
                        _ => "minecraft:sea_lantern"
                    }
                    : "minecraft:redstone_lamp";

                await rcon.SendCommandAsync(
                    $"setblock {x} {lampY} {z + c + 1} {block}",
                    CommandPriority.Normal, ct);
            }
        }
    }
}
