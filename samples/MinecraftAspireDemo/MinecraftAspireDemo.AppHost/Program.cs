using Aspire.Hosting.Minecraft;

var builder = DistributedApplication.CreateBuilder(args);

// Add supporting services (these will be visualized in the Minecraft world)
var redis = builder.AddRedis("cache");

var pg = builder.AddPostgres("db-host");

var db = pg.AddDatabase("db");

// Azure resources — these will appear as AzureThemed buildings
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();
var blobs = storage.AddBlobs("blobs");

var keyVault = builder.AddAzureKeyVault("keyvault");

// Add sample API service
var api = builder.AddProject<Projects.MinecraftAspireDemo_ApiService>("api")
    .WithReference(redis)
    .WithReference(blobs);

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

    // Sprint 1 — Core feedback
    .WithTitleAlerts()
    .WithWeatherEffects()
    .WithBossBar("Minecraft Demo")
    .WithSoundEffects()
    .WithParticleEffects()

    // Sprint 2 — Atmosphere & delight
    .WithActionBarTicker()
    .WithBeaconTowers()
    .WithFireworks()
    .WithGuardianMobs()
    .WithDeploymentFanfare()

    // Sprint 3 — Showstopper
    .WithWorldBorderPulse()
    .WithHeartbeat()
    .WithAchievements()
    .WithRedstoneDependencyGraph()
    .WithServiceSwitches()
    .WithPeacefulMode()

    // Sprint 4 — Visual Identity & Dashboard
    .WithRedstoneDashboard()

    // Monitored resources — each gets in-world representation
    // Projects → Watchtower buildings
    .WithMonitoredResource(api)
    .WithMonitoredResource(web)
    // Containers → Warehouse buildings (Redis/Postgres detected as Cylinder for databases)
    .WithMonitoredResource(redis)
    .WithMonitoredResource(pg)
    // Azure resources → AzureThemed buildings
    .WithMonitoredResource(blobs, "AzureStorage")
    .WithMonitoredResource(keyVault, "AzureKeyVault");

builder.Build().Run();
