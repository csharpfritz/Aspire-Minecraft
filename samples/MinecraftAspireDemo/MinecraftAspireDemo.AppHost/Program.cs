using Aspire.Hosting.Minecraft;

var builder = DistributedApplication.CreateBuilder(args);

// ──────────────────────────────────────────────────────────
// Toggle: set to true for the Grand Village (Milestone 5),
//         set to false for the classic compact village.
// ──────────────────────────────────────────────────────────
var useGrandVillage = true;

// Add supporting services (these will be visualized in the Minecraft world)
var redis = builder.AddRedis("cache");

var pg = builder.AddPostgres("db-host");

var db = pg.AddDatabase("db");

// Add sample API service
var api = builder.AddProject<Projects.MinecraftAspireDemo_ApiService>("api")
    .WithReference(redis);

// Add sample web frontend
var web = builder.AddProject<Projects.MinecraftAspireDemo_Web>("web")
    .WithReference(api)
    .WithExternalHttpEndpoints();

// Add Minecraft server with all integrations — the worker is created internally.
// World data is ephemeral by default (fresh world each run).
// Uncomment .WithPersistentWorld() to keep world data across restarts.
var minecraft = builder.AddMinecraftServer("minecraft", gamePort: 25565, rconPort: 25575)
    //.WithPersistentWorld()

    // Server configuration
    .WithMaxPlayers(10)
    .WithMotd("Aspire Fleet Monitor")

    // Integrations
    .WithBlueMap(port: 8100)
    .WithOpenTelemetry()
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()

    // Core feedback
    .WithTitleAlerts()
    .WithWeatherEffects()
    .WithBossBar("Minecraft Demo")
    .WithSoundEffects()
    .WithParticleEffects()

    // Atmosphere & delight
    .WithActionBarTicker()
    .WithBeaconTowers()
    .WithFireworks()
    .WithGuardianMobs()
    .WithDeploymentFanfare()

    // Showstopper
    .WithWorldBorderPulse()
    .WithHeartbeat()
    .WithAchievements()
    .WithRedstoneDependencyGraph()
    .WithServiceSwitches()
    .WithPeacefulMode()

    // Visual Identity & Dashboard
    .WithRedstoneDashboard()

    // Monitored resources — each gets in-world representation
    .WithMonitoredResource(api)
    .WithMonitoredResource(web)
    .WithMonitoredResource(redis)
    .WithMonitoredResource(pg);

// Grand Village: enlarged 15×15 buildings, minecart rail network
if (useGrandVillage)
{
    minecraft
        .WithGrandVillage()
        .WithMinecartRails();
}

builder.Build().Run();
