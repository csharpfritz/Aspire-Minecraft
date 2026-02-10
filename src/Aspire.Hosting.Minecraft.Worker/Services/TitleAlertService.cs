using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Shows dramatic full-screen title text when a resource goes down or recovers.
/// Uses RCON title commands with colored text.
/// </summary>
internal sealed class TitleAlertService(
    RconService rcon,
    ILogger<TitleAlertService> logger)
{
    /// <summary>
    /// Displays title alerts for each health state transition.
    /// </summary>
    public async Task ShowTitleAlertsAsync(IReadOnlyList<ResourceStatusChange> changes, CancellationToken ct = default)
    {
        if (changes.Count == 0) return;

        // Set title display times: fade-in 10 ticks, stay 60 ticks, fade-out 20 ticks
        await rcon.SendCommandAsync("title @a times 10 60 20", ct);

        foreach (var change in changes)
        {
            if (change.NewStatus == ResourceStatus.Unhealthy)
            {
                await rcon.SendCommandAsync(
                    """title @a title {"text":"\u26A0 SERVICE DOWN","color":"red","bold":true}""", ct);
                await rcon.SendCommandAsync(
                    $$"""title @a subtitle {"text":"{{change.Name}}","color":"gray"}""", ct);
            }
            else if (change.NewStatus == ResourceStatus.Healthy)
            {
                await rcon.SendCommandAsync(
                    """title @a title {"text":"\u2705 BACK ONLINE","color":"green","bold":true}""", ct);
                await rcon.SendCommandAsync(
                    $$"""title @a subtitle {"text":"{{change.Name}}","color":"gray"}""", ct);
            }

            logger.LogInformation("Title alert shown for {Resource}: {Status}",
                change.Name, change.NewStatus);
        }
    }
}
