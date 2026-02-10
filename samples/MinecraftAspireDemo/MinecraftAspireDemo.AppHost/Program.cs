using Aspire.Hosting.Minecraft;

var builder = DistributedApplication.CreateBuilder(args);

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

// Add Minecraft server with all integrations — the worker is created internally
// World data is ephemeral by default (fresh world each run).
// Uncomment .WithPersistentWorld() to keep world data across restarts.
var minecraft = builder.AddMinecraftServer("minecraft", gamePort: 25565, rconPort: 25575)
    //.WithPersistentWorld()
    .WithMaxPlayers(10)
    .WithMotd("Aspire Fleet Monitor")
    .WithBlueMap(port: 8100)
    .WithOpenTelemetry()
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    // Sprint 1 features
    .WithParticleEffects()
    .WithTitleAlerts()
    .WithWeatherEffects()
    .WithBossBar()
    .WithSoundEffects()
    // Sprint 2 features
    .WithActionBarTicker()
    .WithBeaconTowers()
    .WithFireworks()
    .WithGuardianMobs()
    .WithDeploymentFanfare()
    .WithMonitoredResource(api)
    .WithMonitoredResource(web)
    .WithMonitoredResource(redis)
    .WithMonitoredResource(pg);

builder.Build().Run();
