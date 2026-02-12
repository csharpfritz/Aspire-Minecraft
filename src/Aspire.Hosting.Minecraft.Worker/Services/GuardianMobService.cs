using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Spawns guardian mobs per monitored resource.
/// Healthy: iron golem (protector), Unhealthy: zombie (threat).
/// Mobs are named after the resource for identification and cleanup.
/// </summary>
internal sealed class GuardianMobService(
    RconService rcon,
    AspireResourceMonitor monitor,
    ILogger<GuardianMobService> logger)
{
    private const int BaseX = 10;
    private const int BaseZ = -4; // Offset to avoid overlap with structures
    private const int Spacing = 10;

    private int BaseY => VillageLayout.SurfaceY + 2;

    private readonly Dictionary<string, ResourceStatus> _lastKnownStatus = new();

    /// <summary>
    /// Updates guardian mobs for all monitored resources based on current health.
    /// </summary>
    public async Task UpdateGuardianMobsAsync(CancellationToken ct = default)
    {
        var index = 0;
        foreach (var (name, info) in monitor.Resources)
        {
            // Only respawn if status changed or first time
            if (!_lastKnownStatus.TryGetValue(name, out var lastStatus) || lastStatus != info.Status)
            {
                await SpawnGuardianAsync(name, info.Status, index, ct);
                _lastKnownStatus[name] = info.Status;
            }
            index++;
        }
    }

    private async Task SpawnGuardianAsync(string resourceName, ResourceStatus status, int index, CancellationToken ct)
    {
        var col = index % VillageLayout.Columns;
        var row = index / VillageLayout.Columns;
        var x = BaseX + 3 + (col * Spacing);
        var y = BaseY;
        var z = BaseZ + (row * Spacing);

        var mobTag = $"guardian_{resourceName}";

        // Kill existing mob for this resource
        await rcon.SendCommandAsync(
            $"kill @e[name={mobTag}]", ct);

        if (status == ResourceStatus.Healthy)
        {
            // Spawn iron golem (protector)
            await rcon.SendCommandAsync(
                $"summon minecraft:iron_golem {x} {y} {z} {{CustomName:\"\\\"" + mobTag + "\\\"\",NoAI:1b,Invulnerable:1b,PersistenceRequired:1b}}", ct);
        }
        else if (status == ResourceStatus.Unhealthy)
        {
            // Spawn zombie (threat)
            await rcon.SendCommandAsync(
                $"summon minecraft:zombie {x} {y} {z} {{CustomName:\"\\\"" + mobTag + "\\\"\",NoAI:1b,Invulnerable:1b,PersistenceRequired:1b}}", ct);
        }

        logger.LogInformation("Guardian mob updated: {Resource} -> {MobType} at ({X},{Y},{Z})",
            resourceName, status == ResourceStatus.Healthy ? "iron_golem" : "zombie", x, y, z);
    }
}
