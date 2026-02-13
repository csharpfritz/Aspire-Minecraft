using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Easter egg: spawns Fritz's three real horses inside the village fence.
/// Horses are tame, wander freely (NoAI=0), and never despawn.
/// </summary>
internal sealed class HorseSpawnService(
    RconService rcon,
    ILogger<HorseSpawnService> logger)
{
    private bool _horsesSpawned;

    // Horse definitions: (name, variant, nameColor)
    // Variant = color + (marking * 256)
    //   Charmer: black(4) + none(0) = 4
    //   Dancer:  brown(3) + white_field(2*256) = 515
    //   Toby:    white(0) + white_dots(3*256) = 768
    private static readonly (string Name, int Variant, string Color)[] Horses =
    [
        ("Charmer", 4, "dark_gray"),
        ("Dancer", 515, "gold"),
        ("Toby", 768, "white"),
    ];

    /// <summary>
    /// Spawns Fritz's horses once inside the village fence area.
    /// Call after structures are built so the fence exists.
    /// </summary>
    public async Task SpawnHorsesAsync(CancellationToken ct = default)
    {
        if (_horsesSpawned)
            return;

        var y = VillageLayout.SurfaceY + 1;

        for (var i = 0; i < Horses.Length; i++)
        {
            var (name, variant, color) = Horses[i];
            // Place horses in the clearance area between the south fence and the first row of structures
            var x = VillageLayout.BaseX + 1 + (i * 2);
            var z = VillageLayout.BaseZ - 6;

            var command = $"summon minecraft:horse {x} {y} {z} " +
                "{CustomName:'" + name + "',Variant:" + variant + ",Tame:1b,PersistenceRequired:1b}";

            await rcon.SendCommandAsync(command, ct);

            logger.LogInformation("Spawned horse {Name} (variant {Variant}) at ({X},{Y},{Z})",
                name, variant, x, y, z);
        }

        _horsesSpawned = true;
        logger.LogInformation("Fritz's horses are in the village");
    }
}
