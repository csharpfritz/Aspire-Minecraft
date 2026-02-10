namespace Aspire.Hosting.Minecraft;

/// <summary>
/// Common Minecraft <c>server.properties</c> keys for use with
/// <see cref="MinecraftServerBuilderExtensions"/>.
/// Each member maps to a well-known property in the Minecraft server configuration.
/// </summary>
public enum ServerProperty
{
    /// <summary>
    /// Maximum number of players that can be connected simultaneously.
    /// Valid values: 0–2147483647. Default: 20.
    /// </summary>
    MaxPlayers,

    /// <summary>
    /// Message of the day displayed in the server browser.
    /// Supports Minecraft formatting codes.
    /// </summary>
    Motd,

    /// <summary>
    /// Server difficulty level.
    /// Valid values: <c>"peaceful"</c>, <c>"easy"</c>, <c>"normal"</c>, <c>"hard"</c>.
    /// </summary>
    Difficulty,

    /// <summary>
    /// Default game mode for new players.
    /// Valid values: <c>"survival"</c>, <c>"creative"</c>, <c>"adventure"</c>, <c>"spectator"</c>.
    /// </summary>
    GameMode,

    /// <summary>
    /// Whether player-versus-player combat is enabled.
    /// Valid values: <c>"true"</c>, <c>"false"</c>. Default: <c>"true"</c>.
    /// </summary>
    Pvp,

    /// <summary>
    /// Whether the server runs in hardcore mode (permanent death, forced hard difficulty).
    /// Valid values: <c>"true"</c>, <c>"false"</c>. Default: <c>"false"</c>.
    /// </summary>
    Hardcore,

    /// <summary>
    /// Maximum distance (in chunks) the server sends to clients.
    /// Valid values: 3–32. Default: 10.
    /// </summary>
    ViewDistance,

    /// <summary>
    /// Maximum distance (in chunks) for entity simulation and mob spawning.
    /// Valid values: 3–32. Default: 10.
    /// </summary>
    SimulationDistance,

    /// <summary>
    /// Maximum radius of the world in blocks.
    /// Valid values: 1–29999984. Default: 29999984.
    /// </summary>
    MaxWorldSize,

    /// <summary>
    /// Radius (in blocks) around the world spawn where blocks cannot be broken by non-ops.
    /// Set to 0 to disable. Default: 16.
    /// </summary>
    SpawnProtection,

    /// <summary>
    /// Whether animals (passive mobs) can spawn.
    /// Valid values: <c>"true"</c>, <c>"false"</c>. Default: <c>"true"</c>.
    /// </summary>
    SpawnAnimals,

    /// <summary>
    /// Whether monsters (hostile mobs) can spawn.
    /// Valid values: <c>"true"</c>, <c>"false"</c>. Default: <c>"true"</c>.
    /// </summary>
    SpawnMonsters,

    /// <summary>
    /// Whether NPCs (villagers) can spawn.
    /// Valid values: <c>"true"</c>, <c>"false"</c>. Default: <c>"true"</c>.
    /// </summary>
    SpawnNpcs,

    /// <summary>
    /// Whether players can fly in survival mode (requires a mod or plugin).
    /// Valid values: <c>"true"</c>, <c>"false"</c>. Default: <c>"false"</c>.
    /// </summary>
    AllowFlight,

    /// <summary>
    /// Whether players can travel to the Nether dimension.
    /// Valid values: <c>"true"</c>, <c>"false"</c>. Default: <c>"true"</c>.
    /// </summary>
    AllowNether,

    /// <summary>
    /// Whether the server forces players into the default game mode on login.
    /// Valid values: <c>"true"</c>, <c>"false"</c>. Default: <c>"false"</c>.
    /// </summary>
    ForceGamemode,

    /// <summary>
    /// World generator type.
    /// Valid values: <c>"normal"</c>, <c>"flat"</c>, <c>"largeBiomes"</c>, <c>"amplified"</c>, <c>"buffet"</c>.
    /// </summary>
    LevelType,

    /// <summary>
    /// Name of the world folder on disk.
    /// Default: <c>"world"</c>.
    /// </summary>
    LevelName,

    /// <summary>
    /// World generation seed. Leave blank for a random seed.
    /// </summary>
    Seed,

    /// <summary>
    /// Whether the server enforces a whitelist of allowed players.
    /// Valid values: <c>"true"</c>, <c>"false"</c>. Default: <c>"false"</c>.
    /// </summary>
    WhiteList,

    /// <summary>
    /// Whether the server verifies connecting players against Minecraft account databases.
    /// Set to <c>"false"</c> for offline/development servers. Default: <c>"true"</c>.
    /// </summary>
    OnlineMode,

    /// <summary>
    /// Whether command blocks are enabled.
    /// Valid values: <c>"true"</c>, <c>"false"</c>. Default: <c>"false"</c>.
    /// </summary>
    EnableCommandBlock,

    /// <summary>
    /// The port the Minecraft server listens on.
    /// Default: 25565.
    /// </summary>
    ServerPort,

    /// <summary>
    /// Maximum height at which players can place blocks.
    /// Valid values: 0–256. Default: 256.
    /// </summary>
    MaxBuildHeight,

    /// <summary>
    /// Whether structures (villages, strongholds, etc.) generate in the world.
    /// Valid values: <c>"true"</c>, <c>"false"</c>. Default: <c>"true"</c>.
    /// </summary>
    GenerateStructures,
}
