# Village Visualization

The village visualization feature creates a themed Minecraft village where each Aspire resource becomes a building. This provides an intuitive, spatial representation of your distributed system.

## What It Does

Each monitored Aspire resource is represented as:
- A **themed structure** based on resource type
- A **position in a 2-column grid** layout
- **Visual health indicators** on the building
- **Optional dependencies** shown via redstone circuits

## How to Enable

```csharp
builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithMonitoredResource(api)
    .WithMonitoredResource(redis);
```

The village is built automatically when the worker starts and monitors resources.

## Structure Types

### Watchtower (Projects)

**Material:** Stone brick  
**Height:** 10 blocks  
**Design:** Tall tower with hollow interior and flag pole  
**For:** .NET Project resources

**Visual features:**
- Stone brick walls
- Glass windows
- Flag pole at top
- Health lamp on front at height 5

### Warehouse (Containers)

**Material:** Iron block  
**Height:** 5 blocks  
**Design:** Cargo bay style  
**For:** Docker Container resources

**Visual features:**
- Iron block walls
- Industrial appearance
- Flat roof
- Health lamp on front at height 5

### Workshop (Executables)

**Material:** Oak planks  
**Height:** 7 blocks (+ chimney at 8)  
**Design:** Peaked roof with chimney  
**For:** Executable resources

**Visual features:**
- Oak plank walls
- Oak slab peaked roof
- Brick chimney
- Health lamp on front at height 5

### Cottage (Other Resources)

**Material:** Cobblestone  
**Height:** 5 blocks  
**Design:** Humble dwelling  
**For:** Unknown/other resource types

**Visual features:**
- Cobblestone walls
- Simple flat roof
- Modest appearance
- Health lamp on front at height 5

### Cylinder (Database Resources)

**Material:** Smooth stone  
**Height:** 7 blocks (including dome)  
**Design:** Round building with domed roof — evokes the database cylinder icon  
**For:** Database and data store resources (Postgres, Redis, SQL Server, MongoDB, MySQL, MariaDB, CosmosDB, Oracle, SQLite, RabbitMQ)

**Visual features:**
- Circular cross-section (radius 3, 7-block diameter)
- Polished deepslate floor and top band
- Smooth stone walls
- Dome roof with polished deepslate cap
- Copper and iron interior accents
- 1-wide centered door

### Azure-Themed (Azure Resources)

**Material:** Light blue concrete  
**Height:** 6 blocks (including roof)  
**Design:** Blue-themed building with glass roof and banner  
**For:** Azure resources (Service Bus, Key Vault, Event Hubs, App Configuration, SignalR, Storage)

**Visual features:**
- Light blue concrete walls
- Blue concrete trim at top
- Flat light blue stained glass roof
- Blue stained glass pane windows
- Light blue banner on rooftop
- 2-wide door

**Note:** Azure resources that are also databases (e.g., CosmosDB) get a Cylinder building instead, but still receive an Azure banner.

## Layout

### Grid System

Resources are arranged in a **2-column grid**:

```
Resource 1    Resource 2
Resource 3    Resource 4
Resource 5    Resource 6
...
```

**Spacing:** 10 blocks center-to-center between structures  
**Origin:** First structure at X=10, Z=0  
**Growth:** Village expands south (positive Z direction) as resources are added

### Coordinates

The village starts at fixed coordinates:
- **Base X:** 10
- **Base Y:** -60 (grass surface level in superflat world)
- **Base Z:** 0

**Example for 4 resources:**
- Resource 1: X=10, Z=0
- Resource 2: X=20, Z=0
- Resource 3: X=10, Z=10
- Resource 4: X=20, Z=10

### Finding the Village

1. Connect to the Minecraft server
2. Press `F3` to show coordinates
3. Fly to approximately **X=10, Y=-60, Z=0**
4. You'll see the structures arranged in the grid

## Visual Elements

### Paths

**Material:** Cobblestone  
**Placement:** Y=-61 (one block below surface)  
**Coverage:** Entire village area inside fence

The paths create a recessed plaza effect, flush with the surrounding grass.

### Fence Perimeter

**Material:** Oak fence  
**Clearance:** 4 blocks beyond structure edges  
**Gate:** 3-block-wide oak fence gate on south side (at X=17)

The fence provides clear visual boundaries for the village.

### Health Indicators

**Material:** Glowstone (healthy) or Redstone lamp (unhealthy)  
**Placement:** Embedded in front wall at Y=BaseY+5  
**Position:** Horizontally centered, above door

**Behavior:**
- **Healthy:** Glowing yellow glowstone block
- **Unhealthy:** Dark redstone lamp (red when unhealthy)

### Structure Doors

All structures have a front entrance (south-facing):
- **Watchtower:** 3-block tall door (height 1-3)
- **Others:** 2-block tall door (height 1-2)
- **Width:** 2-3 blocks (centered)

## What You'll See in Minecraft

After the worker starts:

1. **Initial build phase** (10-30 seconds for 4 resources):
   - Structures appear one by one
   - Cobblestone paths laid down
   - Fence perimeter built
   - Health lamps placed

2. **Completed village:**
   - All structures visible
   - Clear paths throughout
   - Fence with gate entrance
   - Health lamps glowing (all healthy initially)

3. **During operation:**
   - Health lamps change when resources fail
   - Additional features activate (if enabled)

## Common Use Cases

### Small Application (2-4 resources)

Compact village fits within 30×20 block area. Easy to navigate and monitor.

**Example:**
```csharp
var api = builder.AddProject<Projects.Api>("api");
var web = builder.AddProject<Projects.Web>("web");

builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithMonitoredResource(api)
    .WithMonitoredResource(web);
```

### Medium Application (5-10 resources)

Village extends to 50-60 blocks in Z direction. Still easily visible from one vantage point.

**Example:**
```csharp
var redis = builder.AddRedis("cache");
var postgres = builder.AddPostgres("db-host");
var db = postgres.AddDatabase("db");
var api = builder.AddProject<Projects.Api>("api");
var web = builder.AddProject<Projects.Web>("web");
var worker = builder.AddProject<Projects.Worker>("worker");

builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithMonitoredResource(api)
    .WithMonitoredResource(web)
    .WithMonitoredResource(worker)
    .WithMonitoredResource(redis)
    .WithMonitoredResource(postgres);
```

### Demo/Presentation

Combine village with visual effects for impact:

```csharp
builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithBeaconTowers()           // Tall beacons for visibility
    .WithServiceSwitches()        // Show state clearly
    .WithRedstoneDependencyGraph() // Show architecture
    .WithMonitoredResource(api)
    .WithMonitoredResource(redis);
```

### Monitoring Dashboard View

Use with BlueMap for top-down view:

```csharp
builder.AddMinecraftServer("minecraft")
    .WithBlueMap()  // Web map at port 8100
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithMonitoredResource(api);
```

Open `http://localhost:8100` to see the village from above.

## Technical Details

### Building Process

The village is built using RCON commands sent from the worker:

1. **Structure floors** — Place floor blocks at Y=-60
2. **Structure walls** — Build walls from Y=-59 upward
3. **Door clearing** — Clear air blocks for entrances
4. **Health lamps** — Place glowstone in front walls
5. **Paths** — Clear grass, place cobblestone
6. **Fence** — Build perimeter with gate

**Command rate:** 10 commands/second (rate-limited)  
**Build time:** ~1-2 seconds per structure

### Coordinate System

**Y-levels in superflat world:**
- Y=-64 to -62: Bedrock
- Y=-61: Dirt (paths placed here)
- Y=-60: Grass (structure floors here)
- Y=-59 and up: Air (walls start here)

**Structure footprint:** 7×7 blocks each

### Scaling Limits

**Recommended:** Up to 10 resources (comfortable view)  
**Maximum:** ~45 resources (world border constraint at 256 blocks)

Beyond 10 resources, consider:
- Filtering resource types
- Using BlueMap for overview
- Increasing view distance: `.WithServerProperty("view-distance", "10")`

## Persistence

### With `.WithPersistentWorld()`

Village persists across restarts. Worker detects existing structures and skips rebuilding.

### Without (default)

Fresh world every run. Village is rebuilt each time.

## Integration with Other Features

Village serves as the foundation for:
- **[Service Switches](service-switches.md)** — Levers placed on structures
- **[Redstone Dependencies](redstone-dependencies.md)** — Wires connect structures
- **[Beacon Towers](health-monitoring.md#beacon-towers)** — Beacons above structures
- **[Health Indicators](health-monitoring.md)** — Lamps embedded in walls

All features use the same coordinate system and structure positions.
