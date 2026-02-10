using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Plays audio cues when resources change health state.
/// Down: wither ambient, Up: level up, All green: challenge complete.
/// </summary>
internal sealed class SoundEffectService(
    RconService rcon,
    AspireResourceMonitor monitor,
    ILogger<SoundEffectService> logger)
{
    /// <summary>
    /// Plays appropriate sounds for each health state transition.
    /// </summary>
    public async Task PlaySoundsForChangesAsync(IReadOnlyList<ResourceStatusChange> changes, CancellationToken ct = default)
    {
        if (changes.Count == 0) return;

        foreach (var change in changes)
        {
            if (change.NewStatus == ResourceStatus.Unhealthy)
            {
                await rcon.SendCommandAsync(
                    "playsound minecraft:entity.wither.ambient master @a ~ ~ ~ 1.0 1.0", ct);
            }
            else if (change.NewStatus == ResourceStatus.Healthy)
            {
                await rcon.SendCommandAsync(
                    "playsound minecraft:entity.player.levelup master @a ~ ~ ~ 1.0 1.0", ct);
            }

            logger.LogInformation("Sound played for {Resource}: {Status}", change.Name, change.NewStatus);
        }

        // If all resources are now healthy after a recovery, play celebration sound
        if (changes.Any(c => c.NewStatus == ResourceStatus.Healthy)
            && monitor.TotalCount > 0
            && monitor.HealthyCount == monitor.TotalCount)
        {
            await rcon.SendCommandAsync(
                "playsound minecraft:ui.toast.challenge_complete master @a ~ ~ ~ 1.0 1.0", ct);

            logger.LogInformation("All-green celebration sound played");
        }
    }
}
