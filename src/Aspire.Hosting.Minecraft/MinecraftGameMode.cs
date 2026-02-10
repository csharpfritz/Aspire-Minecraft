namespace Aspire.Hosting.Minecraft;

/// <summary>
/// Minecraft game modes for use with
/// <see cref="MinecraftServerBuilderExtensions"/>.
/// </summary>
public enum MinecraftGameMode
{
    /// <summary>
    /// Survival mode — players gather resources, craft items, and fight mobs. Health and hunger apply.
    /// </summary>
    Survival,

    /// <summary>
    /// Creative mode — unlimited resources, no health/hunger, ability to fly and break blocks instantly.
    /// </summary>
    Creative,

    /// <summary>
    /// Adventure mode — players can interact with blocks (e.g., buttons, levers) but cannot break or place them without the correct tool tags.
    /// </summary>
    Adventure,

    /// <summary>
    /// Spectator mode — players can fly through blocks and observe the world but cannot interact with anything.
    /// </summary>
    Spectator,
}
