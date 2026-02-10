using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Grants in-world achievement-style announcements based on infrastructure events.
/// Uses RCON title commands and sounds for achievement feedback without requiring datapacks.
/// Each achievement is granted once per session.
/// </summary>
internal sealed class AdvancementService(
    RconService rcon,
    AspireResourceMonitor monitor,
    ILogger<AdvancementService> logger)
{
    private readonly HashSet<string> _granted = new();
    private readonly Dictionary<string, ResourceStatus> _previousStatus = new();

    /// <summary>
    /// Checks achievement conditions after health changes and grants any newly earned achievements.
    /// Called on every monitoring cycle with the latest health transition changes.
    /// </summary>
    public async Task CheckAchievementsAsync(IReadOnlyList<ResourceStatusChange> changes, CancellationToken ct = default)
    {
        // Track previous status for crash recovery detection
        foreach (var change in changes)
        {
            // Check "Survived a Crash" — resource went Unhealthy then back to Healthy
            if (change.NewStatus == ResourceStatus.Healthy
                && _previousStatus.TryGetValue(change.Name, out var prev)
                && prev == ResourceStatus.Unhealthy)
            {
                await TryGrantAsync("survived_a_crash", "Survived a Crash", ct);
            }

            _previousStatus[change.Name] = change.NewStatus;
        }

        // "First Service Online" — first resource transitions to Healthy
        if (changes.Any(c => c.NewStatus == ResourceStatus.Healthy))
        {
            await TryGrantAsync("first_service_online", "First Service Online", ct);
        }

        // "Full Fleet Healthy" — ALL resources are Healthy simultaneously
        if (monitor.TotalCount > 0 && monitor.HealthyCount == monitor.TotalCount)
        {
            await TryGrantAsync("full_fleet_healthy", "Full Fleet Healthy", ct);
        }

        // "Night Shift" — all resources healthy during Minecraft night (ticks 13000-23000)
        if (monitor.TotalCount > 0 && monitor.HealthyCount == monitor.TotalCount)
        {
            await CheckNightShiftAsync(ct);
        }
    }

    private async Task CheckNightShiftAsync(CancellationToken ct)
    {
        if (_granted.Contains("night_shift"))
            return;

        try
        {
            var response = await rcon.SendCommandAsync("time query daytime", ct);
            // Response format: "The time is <ticks>"
            var ticks = ParseDaytimeTicks(response);
            if (ticks >= 13000 && ticks <= 23000)
            {
                await TryGrantAsync("night_shift", "Night Shift", ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to query Minecraft time for Night Shift achievement");
        }
    }

    internal static int ParseDaytimeTicks(string response)
    {
        // Expected: "The time is 13000" or similar
        var parts = response.Split(' ');
        for (int i = parts.Length - 1; i >= 0; i--)
        {
            if (int.TryParse(parts[i], out var ticks))
                return ticks;
        }

        return -1;
    }

    private async Task TryGrantAsync(string id, string displayName, CancellationToken ct)
    {
        if (!_granted.Add(id))
            return;

        // Title announcement
        await rcon.SendCommandAsync("title @a times 10 60 20", ct);
        await rcon.SendCommandAsync(
            "title @a title {\"text\":\"\ud83c\udfc6 Achievement!\",\"color\":\"gold\"}", ct);
        await rcon.SendCommandAsync(
            $"title @a subtitle {{\"text\":\"{displayName}\",\"color\":\"yellow\"}}", ct);

        // Achievement sound
        await rcon.SendCommandAsync(
            "execute at @a run playsound minecraft:ui.toast.challenge_complete master @a ~ ~ ~ 1 1", ct);

        logger.LogInformation("Achievement granted: {Achievement}", displayName);
    }
}
