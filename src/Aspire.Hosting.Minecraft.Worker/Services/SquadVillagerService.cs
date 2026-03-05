using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Spawns named NPC villagers for each agent listed in the ASPIRE_SQUAD_AGENTS environment variable.
/// Each villager gets a unique profession, wanders naturally, and is tagged for cleanup.
/// </summary>
/// <remarks>
/// <para><b>DI registration needed in Program.cs (conditionally when env var is set):</b></para>
/// <code>builder.Services.AddSingleton&lt;SquadVillagerService&gt;();</code>
///
/// <para><b>Initialization call needed in MinecraftWorldWorker (after structures are built):</b></para>
/// <code>await squadVillagers.SpawnSquadVillagersAsync(stoppingToken);</code>
/// </remarks>
internal sealed class SquadVillagerService(
    RconService rcon,
    ILogger<SquadVillagerService> logger)
{
    private bool _villagersSpawned;
    private int _resourceCount = 10;

    private const string Tag = "squad_villager";

    // Professions cycled across squad members for visual variety.
    private static readonly string[] Professions =
    [
        "toolsmith",
        "weaponsmith",
        "cartographer",
        "cleric",
        "leatherworker",
        "mason",
        "shepherd",
        "nitwit",
        "fletcher",
        "librarian",
        "farmer",
        "butcher"
    ];

    /// <summary>
    /// Sets the resource count used to compute village bounds for villager placement.
    /// Call before <see cref="SpawnSquadVillagersAsync"/>.
    /// </summary>
    public void SetResourceCount(int resourceCount)
    {
        _resourceCount = resourceCount;
    }

    /// <summary>
    /// Removes all previously spawned squad villagers from the world.
    /// </summary>
    public async Task CleanupAsync(CancellationToken ct = default)
    {
        await rcon.SendCommandAsync($"kill @e[tag={Tag}]", ct);
        _villagersSpawned = false;
        logger.LogInformation("Cleaned up squad villagers");
    }

    /// <summary>
    /// Reads ASPIRE_SQUAD_AGENTS and spawns a named villager for each agent.
    /// Villagers are spread around building entrances and the village gathering area.
    /// </summary>
    public async Task SpawnSquadVillagersAsync(CancellationToken ct = default)
    {
        if (_villagersSpawned)
            return;

        var agentsEnv = Environment.GetEnvironmentVariable("ASPIRE_SQUAD_AGENTS");
        if (string.IsNullOrWhiteSpace(agentsEnv))
        {
            logger.LogDebug("ASPIRE_SQUAD_AGENTS not set — skipping squad villager spawning");
            return;
        }

        var agents = agentsEnv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (agents.Length == 0)
            return;

        // Clean up any leftover squad villagers from a previous run
        await rcon.SendCommandAsync($"kill @e[tag={Tag}]", ct);

        var y = VillageLayout.SurfaceY + 1;
        var positions = ComputePositions(agents.Length);

        for (var i = 0; i < agents.Length; i++)
        {
            var name = agents[i];
            var profession = Professions[i % Professions.Length];
            var (px, pz) = positions[i];

            var command = $"summon minecraft:villager {px} {y} {pz} " +
                "{CustomName:'\"" + EscapeName(name) + "\"'," +
                "CustomNameVisible:1b," +
                "PersistenceRequired:1b," +
                "NoAI:0," +
                "Silent:0b," +
                "Invulnerable:1b," +
                $"Tags:[\"{Tag}\"]," +
                "VillagerData:{profession:\"minecraft:" + profession + "\",level:5,type:\"minecraft:plains\"}}";

            await rcon.SendCommandAsync(command, ct);

            logger.LogInformation("Spawned squad villager {Name} ({Profession}) at ({X},{Y},{Z})",
                name, profession, px, y, pz);
        }

        _villagersSpawned = true;
        logger.LogInformation("Spawned {Count} squad villagers in the village", agents.Length);
    }

    /// <summary>
    /// Computes spread-out positions for squad villagers around the village.
    /// Uses structure entrances first, then fills in along pathways and gathering areas.
    /// Avoids the fruit stand area where the existing 5 NPCs live.
    /// </summary>
    private List<(int x, int z)> ComputePositions(int count)
    {
        var positions = new List<(int x, int z)>();

        // Get village bounds and fruit stand area to avoid
        var (fsMinX, fsMinZ, fsMaxX, fsMaxZ) = VillageLayout.GetFruitStandBounds(_resourceCount);

        // Strategy: place villagers near building entrances (south side, offset to avoid doors).
        // Buildings are on a 2-column grid. We walk structure indices and place villagers
        // 3 blocks south of each building's front, offset east so they don't block doorways.
        var structureCount = Math.Max(_resourceCount, 1);

        for (var i = 0; i < structureCount && positions.Count < count; i++)
        {
            var (ox, _, oz) = VillageLayout.GetStructureOrigin(i);
            var half = VillageLayout.StructureSize / 2;

            // Position: 3 blocks south of building entrance, offset +3 east of center
            var px = ox + half + 3;
            var pz = oz - 3;

            if (!OverlapsFruitStand(px, pz, fsMinX, fsMinZ, fsMaxX, fsMaxZ))
            {
                positions.Add((px, pz));
            }
        }

        // Second pass: west side of buildings for remaining agents
        for (var i = 0; i < structureCount && positions.Count < count; i++)
        {
            var (ox, _, oz) = VillageLayout.GetStructureOrigin(i);
            var half = VillageLayout.StructureSize / 2;

            // Position: west side of building, halfway along the wall
            var px = ox - 3;
            var pz = oz + half;

            if (!OverlapsFruitStand(px, pz, fsMinX, fsMinZ, fsMaxX, fsMaxZ))
            {
                positions.Add((px, pz));
            }
        }

        // Third pass: along the village entrance path (south fence area)
        if (positions.Count < count)
        {
            var (minX, minZ, maxX, _) = VillageLayout.GetVillageBounds(_resourceCount);
            var centerX = (minX + maxX) / 2;
            var entranceZ = minZ - VillageLayout.FenceClearance + 2;

            for (var offset = -8; offset <= 8 && positions.Count < count; offset += 4)
            {
                var px = centerX + offset;
                var pz = entranceZ;

                if (!OverlapsFruitStand(px, pz, fsMinX, fsMinZ, fsMaxX, fsMaxZ))
                {
                    positions.Add((px, pz));
                }
            }
        }

        // Final fallback: wrap around buildings again with different offsets
        var fallbackOffset = 0;
        while (positions.Count < count)
        {
            fallbackOffset++;
            var (ox, _, oz) = VillageLayout.GetStructureOrigin(fallbackOffset % structureCount);
            var px = ox + VillageLayout.StructureSize + 2;
            var pz = oz + (fallbackOffset * 3) % VillageLayout.StructureSize;
            positions.Add((px, pz));
        }

        return positions;
    }

    private static bool OverlapsFruitStand(int x, int z, int fsMinX, int fsMinZ, int fsMaxX, int fsMaxZ)
    {
        // 3-block buffer around fruit stand to avoid crowding existing NPCs
        return x >= fsMinX - 3 && x <= fsMaxX + 3 && z >= fsMinZ - 3 && z <= fsMaxZ + 3;
    }

    private static string EscapeName(string name)
    {
        // Escape any characters that would break JSON/NBT strings
        return name.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("'", "\\'");
    }
}
