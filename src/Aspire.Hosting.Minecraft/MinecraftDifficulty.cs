namespace Aspire.Hosting.Minecraft;

/// <summary>
/// Minecraft difficulty levels for use with
/// <see cref="MinecraftServerBuilderExtensions"/>.
/// </summary>
public enum MinecraftDifficulty
{
    /// <summary>
    /// Peaceful — no hostile mobs spawn, health regenerates over time, hunger does not deplete.
    /// </summary>
    Peaceful,

    /// <summary>
    /// Easy — hostile mobs spawn but deal less damage. Hunger can deplete but won't kill.
    /// </summary>
    Easy,

    /// <summary>
    /// Normal — hostile mobs spawn with standard damage and behavior.
    /// </summary>
    Normal,

    /// <summary>
    /// Hard — hostile mobs deal more damage, zombies can break doors, and hunger can kill.
    /// </summary>
    Hard,
}
