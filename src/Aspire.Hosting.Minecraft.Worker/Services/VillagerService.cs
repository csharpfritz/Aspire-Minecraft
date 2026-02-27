using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Easter egg: spawns three named NPC villagers running a fruit stand in the village.
/// Villagers wander naturally (NoAI=0) and never despawn (PersistenceRequired=1b).
/// </summary>
/// <remarks>
/// <para><b>DI registration needed in Program.cs:</b></para>
/// <code>builder.Services.AddSingleton&lt;VillagerService&gt;();</code>
///
/// <para><b>Initialization call needed in MinecraftWorldWorker (after structures are built):</b></para>
/// <code>await villagerService.SpawnVillagersAsync(stoppingToken);</code>
///
/// <para>Inject as optional parameter in the BackgroundService constructor:</para>
/// <code>VillagerService? villagerService = null</code>
/// </remarks>
internal sealed class VillagerService(
    RconService rcon,
    ILogger<VillagerService> logger)
{
    private bool _villagersSpawned;

    // Fruit stand origin — town center position (between village quadrants).
    // We'll calculate center from village bounds at spawn time.
    private int? _standX;
    private int? _standZ;

    // Villager definitions: (name, profession, spawn offset from stand)
    private static readonly (string Name, string Profession, int OffsetX, int OffsetZ)[] Villagers =
    [
        ("Maddy",   "farmer",    1, 1),   // behind the counter
        ("Damian",  "butcher",   3, 1),   // behind the counter
        ("Fowler",  "fisherman", 2, -1),  // in front, greeting customers
        ("Brady",   "librarian", 0, -1),  // browsing the stand
        ("Scott",   "armorer",   4, -1),  // passing byas a
    ];

    /// <summary>
    /// Builds the fruit stand structure and spawns the three named villagers once.
    /// Call after structures/fence are built so the area is forceloaded and clear.
    /// </summary>
    public async Task SpawnVillagersAsync(CancellationToken ct = default)
    {
        if (_villagersSpawned)
            return;

        var y = VillageLayout.SurfaceY + 1;
        
        // Calculate town center position if not yet done
        if (_standX is null || _standZ is null)
        {
            var resourceCount = 10; // Default fallback
            var (fMinX, fMinZ, fMaxX, fMaxZ) = VillageLayout.GetVillageBounds(resourceCount);
            _standX = (fMinX + fMaxX) / 2 - 2; // Center X, offset for 5-wide stand
            _standZ = (fMinZ + fMaxZ) / 2;     // Center Z
        }
        
        var sx = _standX.Value;
        var sz = _standZ.Value;

        await BuildFruitStandAsync(sx, y, sz, ct);
        await SpawnVillagerEntitiesAsync(sx, y, sz, ct);

        _villagersSpawned = true;
        logger.LogInformation("Fruit stand and villagers are ready in the village");
    }

    /// <summary>
    /// Builds a small 5×3 market stall with a counter, awning, barrels, and signage.
    /// </summary>
    private async Task BuildFruitStandAsync(int sx, int y, int sz, CancellationToken ct)
    {
        // Spruce plank floor under the stand
        await rcon.SendCommandAsync(
            $"fill {sx} {y} {sz} {sx + 4} {y} {sz + 2} minecraft:spruce_planks", ct);

        // Front counter — oak slabs across the south face
        await rcon.SendCommandAsync(
            $"fill {sx + 1} {y + 1} {sz} {sx + 3} {y + 1} {sz} minecraft:oak_slab[type=bottom]", ct);

        // Four corner support posts — spruce fence, 2 blocks tall
        foreach (var (cx, cz) in new[] { (sx, sz), (sx + 4, sz), (sx, sz + 2), (sx + 4, sz + 2) })
        {
            await rcon.SendCommandAsync(
                $"setblock {cx} {y + 1} {cz} minecraft:spruce_fence", ct);
            await rcon.SendCommandAsync(
                $"setblock {cx} {y + 2} {cz} minecraft:spruce_fence", ct);
        }

        // Orange wool awning overhead
        await rcon.SendCommandAsync(
            $"fill {sx} {y + 3} {sz} {sx + 4} {y + 3} {sz + 2} minecraft:orange_wool", ct);

        // Barrels along the back row for fruit storage
        await rcon.SendCommandAsync(
            $"setblock {sx + 1} {y + 1} {sz + 2} minecraft:barrel[facing=up]", ct);
        await rcon.SendCommandAsync(
            $"setblock {sx + 2} {y + 1} {sz + 2} minecraft:barrel[facing=up]", ct);
        await rcon.SendCommandAsync(
            $"setblock {sx + 3} {y + 1} {sz + 2} minecraft:barrel[facing=up]", ct);

        // Lantern hanging from the awning center
        await rcon.SendCommandAsync(
            $"setblock {sx + 2} {y + 2} {sz + 1} minecraft:lantern[hanging=true]", ct);

        // "Fresh Fruit!" sign on the front of the counter
        await rcon.SendCommandAsync(
            $"setblock {sx + 2} {y + 2} {sz} minecraft:spruce_wall_sign[facing=south]", ct);
        var signCmd = "data merge block " + $"{sx + 2} {y + 2} {sz}" +
            " {front_text:{messages:[\"\"," +
            "\"\\\"Fresh Fruit!\\\"\"," +
            "\"\\\"Maddy & Co.\\\"\"," +
            "\"\"]}}";
        await rcon.SendCommandAsync(signCmd, ct);

        logger.LogInformation("Built fruit stand at ({X},{Y},{Z})", sx, y, sz);
    }

    /// <summary>
    /// Summons the three named villagers near the fruit stand.
    /// </summary>
    private async Task SpawnVillagerEntitiesAsync(int sx, int y, int sz, CancellationToken ct)
    {
        foreach (var (name, profession, offsetX, offsetZ) in Villagers)
        {
            var vx = sx + offsetX;
            var vz = sz + offsetZ;

            var command = $"summon minecraft:villager {vx} {y} {vz} " +
                "{CustomName:'" + name + "'," +
                "CustomNameVisible:1b," +
                "PersistenceRequired:1b," +
                "VillagerData:{profession:\"minecraft:" + profession + "\",level:5,type:\"minecraft:plains\"}}";

            await rcon.SendCommandAsync(command, ct);

            logger.LogInformation("Spawned villager {Name} ({Profession}) at ({X},{Y},{Z})",
                name, profession, vx, y, vz);
        }
    }
}
