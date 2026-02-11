namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Priority levels for RCON commands. High-priority commands bypass rate limits;
/// low-priority commands queue when the rate limit is hit.
/// </summary>
internal enum CommandPriority
{
    /// <summary>Low priority — structure builds, cosmetic updates. Queued when rate-limited.</summary>
    Low,

    /// <summary>Normal priority — default for most commands.</summary>
    Normal,

    /// <summary>High priority — health updates, player messages. Bypasses rate limits.</summary>
    High
}
