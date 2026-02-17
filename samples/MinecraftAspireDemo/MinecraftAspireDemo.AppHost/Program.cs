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

// Add sample Python API — Executable resource → Workshop building
var pythonApi = builder.AddPythonApp("python-api", "../MinecraftAspireDemo.PythonApi", "main.py")
    .WithHttpEndpoint(port: 5100);

// Add sample Node.js API — Executable resource → Workshop building
var nodeApi = builder.AddNodeApp("node-api", "../MinecraftAspireDemo.NodeApi", "app.js")
    .WithHttpEndpoint(port: 5200);

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
    .WithServiceSwitches()
    .WithPeacefulMode()

    // Visual Identity & Dashboard
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
    .WithMonitoredResource(keyVault, "AzureKeyVault")
    // Executable resources (Python, Node.js) → Workshop buildings
    .WithMonitoredResource(pythonApi)
    .WithMonitoredResource(nodeApi);

// Grand Village: enlarged 15×15 buildings, minecart rail network
if (useGrandVillage)
{
    minecraft
        .WithGrandVillage()
        .WithMinecartRails()
        .WithCanals()
        .WithErrorBoats();
}

builder.Build().Run();
