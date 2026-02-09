using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Sends system messages to Minecraft players and logs them as structured OTEL events.
/// All outbound player messages flow through this service for audit trailing.
/// </summary>
public sealed class PlayerMessageService(RconService rcon, ILogger<PlayerMessageService> logger)
{
    /// <summary>
    /// Broadcasts a health status change alert to all players.
    /// </summary>
    public async Task BroadcastHealthAlertAsync(string resourceName, string oldStatus, string newStatus, CancellationToken ct = default)
    {
        var color = newStatus == "Healthy" ? "green" : "red";
        var icon = newStatus == "Healthy" ? "\\u2714" : "\\u26A0";
        var json = $$"""{"text":"{{icon}} {{resourceName}} is now {{newStatus}}","color":"{{color}}"}""";

        await rcon.SendCommandAsync($"tellraw @a {json}", ct);

        MinecraftMetrics.RecordPlayerMessage("ResourceHealthAlert");
        logger.LogInformation("Player message sent: {MessageType} {Command} {Target} {Content} {ResourceName} {Trigger} {OldStatus} {NewStatus}",
            "ResourceHealthAlert", "tellraw", "@a",
            $"{icon} {resourceName} is now {newStatus}",
            resourceName, "HealthChanged", oldStatus, newStatus);
    }

    /// <summary>
    /// Broadcasts a periodic status summary to all players.
    /// </summary>
    public async Task BroadcastStatusSummaryAsync(int healthyCount, int totalCount, CancellationToken ct = default)
    {
        var msg = $"[Aspire] {healthyCount}/{totalCount} services healthy";
        await rcon.SendCommandAsync($"say {msg}", ct);

        MinecraftMetrics.RecordPlayerMessage("StatusBroadcast");
        logger.LogInformation("Player message sent: {MessageType} {Command} {Target} {Content} {HealthyCount} {TotalCount} {Trigger}",
            "StatusBroadcast", "say", "@a", msg,
            healthyCount, totalCount, "PeriodicUpdate");
    }

    /// <summary>
    /// Announces a newly discovered Aspire resource to all players.
    /// </summary>
    public async Task AnnounceNewResourceAsync(string resourceName, string resourceType, CancellationToken ct = default)
    {
        var json = $$"""{"text":"\\u2714 New service '{{resourceName}}' is online","color":"green"}""";
        await rcon.SendCommandAsync($"tellraw @a {json}", ct);

        MinecraftMetrics.RecordPlayerMessage("ResourceDiscovered");
        logger.LogInformation("Player message sent: {MessageType} {Command} {Target} {Content} {ResourceName} {ResourceType} {Trigger}",
            "ResourceDiscovered", "tellraw", "@a",
            $"New service '{resourceName}' is online",
            resourceName, resourceType, "ResourceAdded");
    }

    /// <summary>
    /// Sends a custom system message to all players.
    /// </summary>
    public async Task SendSystemMessageAsync(string message, CancellationToken ct = default)
    {
        await rcon.SendCommandAsync($"say {message}", ct);

        MinecraftMetrics.RecordPlayerMessage("SystemMessage");
        logger.LogInformation("Player message sent: {MessageType} {Command} {Target} {Content} {Trigger}",
            "SystemMessage", "say", "@a", message, "Manual");
    }
}
