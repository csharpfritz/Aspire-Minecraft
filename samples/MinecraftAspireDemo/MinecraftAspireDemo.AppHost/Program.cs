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
var minecraft = builder.AddMinecraftServer("minecraft", gamePort: 25565, rconPort: 25575)
    .WithBlueMap(port: 8100)
    .WithOpenTelemetry()
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithParticleEffects()
    .WithTitleAlerts()
    .WithWeatherEffects()
    .WithBossBar()
    .WithSoundEffects()
    .WithMonitoredResource(api)
    .WithMonitoredResource(web)
    .WithMonitoredResource(redis)
    .WithMonitoredResource(pg);

builder.Build().Run();
