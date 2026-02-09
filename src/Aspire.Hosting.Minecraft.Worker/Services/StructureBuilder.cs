using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Builds block structures in the Minecraft world representing each Aspire resource.
/// Each service gets a small building with a torch indicating health status.
/// </summary>
public sealed class StructureBuilder(
    RconService rcon,
    AspireResourceMonitor monitor,
    ILogger<StructureBuilder> logger)
{
    private const int BaseX = 10;
    private const int BaseY = -60; // One block above superflat grass surface (Y=-61)
    private const int BaseZ = 0;
    private const int Spacing = 6;

    // Block types for different resource types
    private static readonly Dictionary<string, string> ResourceBlocks = new()
    {
        ["Project"] = "minecraft:emerald_block",
        ["Container"] = "minecraft:iron_block",
        ["Redis"] = "minecraft:redstone_block",
        ["Postgres"] = "minecraft:lapis_block",
        ["Unknown"] = "minecraft:stone_bricks"
    };

    /// <summary>
    /// Builds or updates structures for all monitored resources.
    /// </summary>
    public async Task UpdateStructuresAsync(CancellationToken ct = default)
    {
        using var activity = MinecraftMetrics.ActivitySource.StartActivity("minecraft.world.update_structures");

        try
        {
            var index = 0;
            foreach (var (_, info) in monitor.Resources)
            {
                await BuildResourceStructureAsync(info, index, ct);
                index++;
            }

            logger.LogInformation("Structures updated for {Count} resources", monitor.TotalCount);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update structures");
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
        }
    }

    private async Task BuildResourceStructureAsync(ResourceInfo info, int index, CancellationToken ct)
    {
        var x = BaseX + (index * Spacing);
        var y = BaseY;
        var z = BaseZ;

        var blockType = ResourceBlocks.GetValueOrDefault(info.Type, ResourceBlocks["Unknown"]);

        // Build a 3x3x2 base (two layers tall)
        await rcon.SendCommandAsync(
            $"fill {x} {y} {z} {x + 2} {y + 1} {z + 2} {blockType}", ct);

        // Place torch on top based on health status
        var torchBlock = info.Status switch
        {
            ResourceStatus.Healthy => "minecraft:torch",
            ResourceStatus.Unhealthy => "minecraft:redstone_torch",
            _ => "minecraft:air"
        };
        await rcon.SendCommandAsync(
            $"setblock {x + 1} {y + 2} {z + 1} {torchBlock}", ct);

        // Place a sign on the front face with the resource name
        var signY = y + 1;
        await rcon.SendCommandAsync(
            $"setblock {x + 1} {signY} {z - 1} minecraft:oak_sign[rotation=8]", ct);
        var signCmd = "data merge block " + $"{x + 1} {signY} {z - 1}" +
            " {front_text:{messages:[\"\"," +
            "\"" + info.Name + "\"," +
            "\"(" + info.Status + ")\"," +
            "\"\"]}}";
        await rcon.SendCommandAsync(signCmd, ct);

        logger.LogInformation("Structure built: {ResourceName} {BlockType} {TorchState}",
            info.Name, blockType, info.Status.ToString().ToLowerInvariant());
    }
}
