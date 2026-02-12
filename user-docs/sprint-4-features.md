# Sprint 4 Features Guide

Sprint 4 introduces smarter building types, a health history dashboard, and a convenience method to enable everything at once.

## 📊 Redstone Dashboard

A physical wall west of the village that displays **health history over time** using redstone lamps. Think of it as a scrolling time-series chart built from Minecraft blocks.

### What It Shows

- **Each row** = one monitored resource (labeled with signs)
- **Each column** = one time slot (oldest on the left, newest on the right)
- **Lit lamp** (redstone lamp + redstone block) = healthy at that time
- **Dark lamp** (redstone lamp, no power) = unhealthy at that time
- **Sea lantern** = unknown/starting state

The display scrolls left on each update cycle, so you get a moving window of recent health history.

### How to Enable

```csharp
builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithRedstoneDashboard()
    .WithMonitoredResource(api)
    .WithMonitoredResource(redis);
```

### How to Read It

1. Fly west of the village (negative X direction) to find the dashboard wall
2. Each row has a sign on the left edge showing the resource name
3. Look across the row left-to-right to see health over time
4. A title sign at the top reads "Health Dashboard"

### Dashboard Sizing

The grid auto-scales based on resource count:

| Resources | Columns (time slots) |
|-----------|---------------------|
| 1–8       | 10                  |
| 9–16      | 8                   |
| 17+       | 6                   |

Maximum rows: 30 (one per resource).

## 🏛️ Database Cylinder Buildings

Database and data store resources now render as **round cylinder buildings** instead of generic cottages. The cylinder shape evokes the classic database icon.

### What Triggers a Cylinder

Any resource whose type contains one of these keywords (case-insensitive):

- `postgres` — PostgreSQL
- `redis` — Redis cache
- `sqlserver` / `sql-server` — SQL Server
- `mongodb` — MongoDB
- `mysql` — MySQL
- `mariadb` — MariaDB
- `cosmosdb` — Cosmos DB
- `oracle` — Oracle
- `sqlite` — SQLite
- `rabbitmq` — RabbitMQ

### How They Look

- **Walls:** Smooth stone, 4 blocks tall, circular cross-section (radius 3)
- **Floor:** Polished deepslate disc
- **Top band:** Polished deepslate trim at the top of the walls
- **Roof:** Smooth stone slab dome with polished deepslate cap
- **Interior:** Copper block accents on the floor, iron block detail blocks
- **Door:** 1-wide, 2-tall centered entrance on the south face
- **Footprint:** 7×7 blocks (fits the standard village grid cell)

### Example

```csharp
var redis = builder.AddRedis("cache");
var pg = builder.AddPostgres("db-host");

builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithMonitoredResource(redis)   // Cylinder building
    .WithMonitoredResource(pg);     // Cylinder building
```

No configuration needed — cylinder buildings are assigned automatically based on resource type.

## ☁️ Azure-Themed Buildings

Azure resources get a distinctive **blue-themed building** with banners, making cloud resources visually distinct from local infrastructure.

### What Triggers an Azure Building

Any resource whose type contains one of these keywords (case-insensitive):

- `azure` — Any Azure resource
- `cosmos` — Cosmos DB (note: database Cosmos resources get a Cylinder instead)
- `servicebus` — Azure Service Bus
- `eventhub` — Azure Event Hubs
- `keyvault` — Azure Key Vault
- `appconfiguration` — Azure App Configuration
- `signalr` — Azure SignalR
- `storage` — Azure Storage

**Priority rule:** If a resource matches both database and Azure keywords (e.g., `cosmosdb`), it gets a **Cylinder** building. Azure theming applies to non-database Azure resources.

### How They Look

- **Walls:** Light blue concrete, 4 blocks tall
- **Trim:** Blue concrete band at the top of the walls
- **Roof:** Flat light blue stained glass
- **Windows:** Blue stained glass panes on front and sides
- **Door:** 2-wide, 2-tall entrance
- **Banner:** Light blue banner on the rooftop — the Azure signature
- **Footprint:** 7×7 blocks (same as Cottage dimensions)

### Azure Banners on Other Buildings

If a resource is Azure-typed but gets a different building type (e.g., a Watchtower for an Azure-hosted .NET project), it still receives an **Azure light blue banner** on the rooftop as a visual indicator.

### Example

```csharp
// Assuming Azure integrations
var serviceBus = builder.AddAzureServiceBus("messaging");
var keyVault = builder.AddAzureKeyVault("secrets");

builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithMonitoredResource(serviceBus)  // Azure-themed building
    .WithMonitoredResource(keyVault);   // Azure-themed building
```

No configuration needed — Azure theming is assigned automatically based on resource type.

## 🎯 WithAllFeatures

A convenience method that enables **every opt-in feature** in a single call. Perfect for demos, presentations, or when you want the full experience without listing each feature individually.

### What It Enables

`WithAllFeatures()` is equivalent to calling all of the following:

```csharp
.WithParticleEffects()
.WithTitleAlerts()
.WithWeatherEffects()
.WithBossBar()
.WithSoundEffects()
.WithActionBarTicker()
.WithBeaconTowers()
.WithFireworks()
.WithGuardianMobs()
.WithDeploymentFanfare()
.WithWorldBorderPulse()
.WithAchievements()
.WithHeartbeat()
.WithRedstoneDependencyGraph()
.WithServiceSwitches()
.WithPeacefulMode()
.WithRedstoneDashboard()
.WithRconDebugLogging()
```

### When to Use It

- **Demos and presentations** — Maximum visual impact with minimal code
- **Exploring the integration** — See everything the project can do
- **Quick prototyping** — Get started fast, then pare down later

### When NOT to Use It

- **Production monitoring** — You probably don't want debug logging or guardian mobs
- **Performance-sensitive setups** — Some features (particles, mobs) add RCON command overhead
- **Selective monitoring** — If you only want specific features, call them individually

### Example

```csharp
builder.AddMinecraftServer("minecraft")
    .WithPersistentWorld()
    .WithBlueMap()
    .WithOpenTelemetry()
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithAllFeatures()  // Everything enabled!
    .WithMonitoredResource(api)
    .WithMonitoredResource(redis);
```

**Requires:** `WithAspireWorldDisplay()` must be called before `WithAllFeatures()`.

## Building Type Summary

| Building Type | Material | Shape | Assigned To |
|--------------|----------|-------|-------------|
| **Watchtower** | Stone brick | Tall tower (10 blocks) | .NET Projects |
| **Warehouse** | Iron block | Cargo bay (5 blocks) | Docker Containers |
| **Workshop** | Oak planks | Peaked roof + chimney | Executables |
| **Cylinder** | Smooth stone | Round with dome roof | Database resources |
| **Azure-Themed** | Light blue concrete | Flat blue glass roof + banner | Azure resources (non-database) |
| **Cottage** | Cobblestone | Humble dwelling | Everything else |

Building type is assigned automatically — no configuration needed.
