using Aspire.Hosting.Minecraft;
using Aspire.Hosting.ApplicationModel;

// ───────────────────────────────────────────────────────────────────────────────
// Grand Village Demo
//
// Showcases the Grand Village feature which renders every monitored resource as a
// 15×15 block grand building with furnished interiors and multi-story layouts.
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
//   Executable (Java)    │ Workshop         │ 15×15
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
var serviceBus = builder.AddAzureServiceBus("servicebus");
var cosmosDb = builder.AddAzureCosmosDB("cosmos")
    .RunAsEmulator();

// --- .NET projects (Watchtower grand buildings) ---
var api = builder.AddProject<Projects.GrandVillageDemo_ApiService>("api")
    .WithReference(redis)
    .WithReference(blobs)
    .WithHttpHealthCheck("/health")
    .WithHttpCommand("/trigger-error", "Trigger Error", commandName: "trigger-error", commandOptions: new HttpCommandOptions
    {
        IconName = "ErrorCircle",
        IconVariant = IconVariant.Filled,
        Description = "Simulates a failure on the API service to trigger an error boat in the Minecraft canal",
        ConfirmationMessage = "This will simulate a failure on the API service. Continue?",
        IsHighlighted = true
    });

var web = builder.AddProject<Projects.GrandVillageDemo_Web>("web")
    .WithReference(api)
    .WithExternalHttpEndpoints();

var worker = builder.AddProject<Projects.GrandVillageDemo_WorkerService>("worker")
    .WithReference(redis);

var gateway = builder.AddProject<Projects.GrandVillageDemo_Gateway>("gateway")
    .WithReference(api)
    .WithReference(web);

// --- Python executable (Workshop grand building) ---
var pythonApi = builder.AddPythonApp("python-api", "../GrandVillageDemo.PythonApi", "main.py")
    .WithHttpEndpoint(port: 5300);

// --- Node.js executable (Workshop grand building) ---
var nodeApi = builder.AddNodeApp("node-api", "../GrandVillageDemo.NodeApi", "app.js")
    .WithHttpEndpoint(port: 5400);

// --- Java Spring container (Workshop grand building — orange branding) ---
// Note: AddSpringApp auto-registers an HTTP endpoint via JavaAppContainerResourceOptions.
// Set Port there instead of chaining .WithHttpEndpoint() to avoid a duplicate endpoint conflict.
var javaApi = builder.AddSpringApp("java-api",
    new JavaAppContainerResourceOptions
    {
        ContainerImageName = "aliencube/aspire-spring-maven-sample",
        OtelAgentPath = "/agents",
        Port = 5500,
    });

// --- Minecraft server with ALL features ---
var minecraft = builder.AddMinecraftServer("minecraft", gamePort: 25565, rconPort: 25575)
    .WithMaxPlayers(10)
    .WithMotd("Grand Village Demo")
    .WithBlueMap(port: 8200)
    .WithOpenTelemetry()
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()

    // Enable every feature (Grand Village buildings are now the default)
    .WithAllFeatures()

    // Wire all resource types so every grand building variant is shown
    // Projects → grand Watchtower (15×15)
    .WithMonitoredResource(api)
    .WithMonitoredResource(web)
    .WithMonitoredResource(worker)
    .WithMonitoredResource(gateway)
    // Containers → grand Warehouse / Cylinder (15×15)
    .WithMonitoredResource(redis)
    .WithMonitoredResource(pg)
    // Azure → grand AzureThemed (15×15)
    .WithMonitoredResource(blobs, "AzureStorage")
    .WithMonitoredResource(keyVault, "AzureKeyVault")
    .WithMonitoredResource(serviceBus, "AzureServiceBus")
    .WithMonitoredResource(cosmosDb, "AzureCosmosDB")
    // Executables → grand Workshop (15×15)
    .WithMonitoredResource(pythonApi)
    .WithMonitoredResource(nodeApi)
    .WithMonitoredResource(javaApi);

builder.Build().Run();
