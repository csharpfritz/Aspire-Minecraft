using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Plays a celebration when a resource transitions from Starting to Running (Unknown → Healthy).
/// Includes lightning bolt, fireworks, and a title announcement.
/// </summary>
internal sealed class DeploymentFanfareService(
    RconService rcon,
    ILogger<DeploymentFanfareService> logger)
{
    /// <summary>
    /// Checks for deployment events (Unknown → Healthy) and triggers fanfare.
    /// </summary>
    public async Task CheckAndCelebrateAsync(IReadOnlyList<ResourceStatusChange> changes, CancellationToken ct = default)
    {
        foreach (var change in changes)
        {
            // Unknown → Healthy represents a fresh deployment (Starting → Running)
            if (change.OldStatus == ResourceStatus.Unknown && change.NewStatus == ResourceStatus.Healthy)
            {
                await PlayFanfareAsync(change.Name, ct);
            }
        }
    }

    private async Task PlayFanfareAsync(string resourceName, CancellationToken ct)
    {
        // Lightning bolt at a central location
        await rcon.SendCommandAsync(
            "summon minecraft:lightning_bolt 14 -58 0", ct);

        // Fireworks
        await rcon.SendCommandAsync(
            "summon minecraft:firework_rocket 12 -58 0", ct);
        await rcon.SendCommandAsync(
            "summon minecraft:firework_rocket 16 -58 0", ct);

        // Title announcement (plain strings, no JSON text components)
        await rcon.SendCommandAsync(
            "title @a times 10 40 10", ct);
        await rcon.SendCommandAsync(
            $"title @a title \"DEPLOYED\"", ct);
        await rcon.SendCommandAsync(
            $"title @a subtitle \"{resourceName} is online\"", ct);

        logger.LogInformation("Deployment fanfare played for {Resource}", resourceName);
    }
}
