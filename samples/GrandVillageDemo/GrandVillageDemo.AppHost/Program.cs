using Aspire.Hosting.Minecraft;

// ───────────────────────────────────────────────────────────────────────────────
// Grand Village Demo
//
// Showcases WithGrandVillage() which renders every monitored resource as a
// 15×15 block grand building variant (instead of the default 7×7 compact size).
// Includes all supported resource types so every grand building style is visible:
//
//   Resource Kind        │ Building Type    │ Grand Size
//   ─────────────────────┼──────────────────┼──────────
//   Project (.NET)       │ Watchtower       │ 15×15
//   Container (generic)  │ Warehouse        │ 15×15
//   Container (database) │ Cylinder         │ 15×15
//   Azure resource       │ AzureThemed      │ 15×15
//   Executable (Python)  │ Workshop         │ 15×15
//   Executable (Node.js) │ Workshop         │ 15×15
//   Other / unknown      │ Cottage          │ 15×15
// ───────────────────────────────────────────────────────────────────────────────

var builder = DistributedApplication.CreateBuilder(args);

// --- Databases (Cylinder grand buildings) ---
var redis = builder.AddRedis("cache");
var pg = builder.AddPostgres("db-host");
var db = pg.AddDatabase("db");

// --- Azure resources (AzureThemed grand buildings) ---
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();
var blobs = storage.AddBlobs("blobs");
var keyVault = builder.AddAzureKeyVault("keyvault");

// --- .NET projects (Watchtower grand buildings) ---
var api = builder.AddProject<Projects.GrandVillageDemo_ApiService>("api")
    .WithReference(redis)
    .WithReference(blobs);

var web = builder.AddProject<Projects.GrandVillageDemo_Web>("web")
    .WithReference(api)
    .WithExternalHttpEndpoints();

// --- Python executable (Workshop grand building) ---
var pythonApi = builder.AddPythonApp("python-api", "../GrandVillageDemo.PythonApi", "main.py")
    .WithHttpEndpoint(port: 5300);

// --- Node.js executable (Workshop grand building) ---
var nodeApi = builder.AddNodeApp("node-api", "../GrandVillageDemo.NodeApi", "app.js")
    .WithHttpEndpoint(port: 5400);

// --- Minecraft server with ALL features + Grand Village ---
var minecraft = builder.AddMinecraftServer("minecraft", gamePort: 25565, rconPort: 25575)
    .WithMaxPlayers(10)
    .WithMotd("Grand Village Demo")
    .WithBlueMap(port: 8200)
    .WithOpenTelemetry()
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()

    // Enable every feature including Grand Village and Minecart Rails
    .WithAllFeatures()

    // Wire all resource types so every grand building variant is shown
    // Projects → grand Watchtower (15×15)
    .WithMonitoredResource(api)
    .WithMonitoredResource(web)
    // Containers → grand Warehouse / Cylinder (15×15)
    .WithMonitoredResource(redis)
    .WithMonitoredResource(pg)
    // Azure → grand AzureThemed (15×15)
    .WithMonitoredResource(blobs, "AzureStorage")
    .WithMonitoredResource(keyVault, "AzureKeyVault")
    // Executables → grand Workshop (15×15)
    .WithMonitoredResource(pythonApi)
    .WithMonitoredResource(nodeApi);

builder.Build().Run();
