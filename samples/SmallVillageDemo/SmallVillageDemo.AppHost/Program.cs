using Aspire.Hosting.Minecraft;

// ───────────────────────────────────────────────────────────────────────────────
// Small Village Demo
//
// Minimal Aspire solution with only 2 monitored resources (a web app and a
// PostgreSQL database) to verify that the village layout adapts correctly when
// there are very few services.
// ───────────────────────────────────────────────────────────────────────────────

var builder = DistributedApplication.CreateBuilder(args);

// --- Database (Cylinder grand building) ---
var pg = builder.AddPostgres("db-host");
var db = pg.AddDatabase("db");

// --- .NET web project (Watchtower grand building) ---
var web = builder.AddProject<Projects.SmallVillageDemo_Web>("web")
    .WithReference(db)
    .WithExternalHttpEndpoints();

// --- Minecraft server with all features, monitoring just 2 resources ---
builder.AddMinecraftServer("minecraft", gamePort: 25665, rconPort: 25675)
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithAllFeatures()
    .WithMonitoredResource(web)
    .WithMonitoredResource(pg);

builder.Build().Run();
