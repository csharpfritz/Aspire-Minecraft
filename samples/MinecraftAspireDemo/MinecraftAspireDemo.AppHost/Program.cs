using Aspire.Hosting.Minecraft;

var builder = DistributedApplication.CreateBuilder(args);

// Add supporting services (these will be visualized in the Minecraft world)
var redis = builder.AddRedis("cache");

// Add Minecraft server with all integrations
var minecraft = builder.AddMinecraftServer("minecraft", gamePort: 25565, rconPort: 25575)
    .WithBlueMap(port: 8100)
    .WithOpenTelemetry()
    .WithAspireWorldDisplay();

// Add sample API service
var api = builder.AddProject<Projects.MinecraftAspireDemo_ApiService>("api")
    .WithReference(redis);

// Add sample web frontend
var web = builder.AddProject<Projects.MinecraftAspireDemo_Web>("web")
    .WithReference(api)
    .WithExternalHttpEndpoints();

// Add the Minecraft world worker — connects via RCON and creates in-world displays
builder.AddProject<Projects.Aspire_Hosting_Minecraft_Worker>("minecraft-worker")
    .WithReference(minecraft)
    .WaitFor(minecraft)
    .WithParentRelationship(minecraft)
    .WithEnvironment("ASPIRE_RESOURCE_API_URL", api.GetEndpoint("http").Property(EndpointProperty.Url))
    .WithEnvironment("ASPIRE_RESOURCE_API_TYPE", "Project")
    .WithEnvironment("ASPIRE_RESOURCE_WEB_URL", web.GetEndpoint("http").Property(EndpointProperty.Url))
    .WithEnvironment("ASPIRE_RESOURCE_WEB_TYPE", "Project");

builder.Build().Run();
