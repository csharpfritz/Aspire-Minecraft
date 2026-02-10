using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Shows localized particle effects at resource structures on health transitions.
/// Crash: large_smoke + flame, Recovery: happy_villager.
/// </summary>
internal sealed class ParticleEffectService(
    RconService rcon,
    AspireResourceMonitor monitor,
    ILogger<ParticleEffectService> logger)
{

    /// <summary>
    /// Spawns particle effects at the structure of each resource that changed health.
    /// </summary>
    public async Task ShowParticlesForChangesAsync(IReadOnlyList<ResourceStatusChange> changes, CancellationToken ct = default)
    {
        if (changes.Count == 0) return;

        var resourceNames = monitor.Resources.Keys.ToList();

        foreach (var change in changes)
        {
            var index = resourceNames.IndexOf(change.Name);
            if (index < 0) continue;

            var (x, y, z) = VillageLayout.GetAboveStructure(index);

            if (change.NewStatus == ResourceStatus.Unhealthy)
            {
                // Crash: large_smoke + flame
                await rcon.SendCommandAsync(
                    $"particle minecraft:large_smoke {x} {y} {z} 1 1 1 0.02 30 force", ct);
                await rcon.SendCommandAsync(
                    $"particle minecraft:flame {x} {y} {z} 0.5 0.5 0.5 0.05 20 force", ct);
            }
            else if (change.NewStatus == ResourceStatus.Healthy)
            {
                // Recovery: happy_villager
                await rcon.SendCommandAsync(
                    $"particle minecraft:happy_villager {x} {y} {z} 1 1 1 0.5 40 force", ct);
            }

            logger.LogInformation("Particle effect at ({X},{Y},{Z}) for {Resource}: {Status}",
                x, y, z, change.Name, change.NewStatus);
        }
    }
}
