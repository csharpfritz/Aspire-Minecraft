# Redstone Dependency Graph

The redstone dependency graph visualizes your distributed system's architecture using Minecraft redstone circuits. Dependencies between services appear as physical connections in the world.

## What It Does

Creates visual redstone circuits connecting dependent resources:
- **Redstone wire** paths from dependency to dependent
- **Repeaters** every 15 blocks to maintain signal strength
- **Redstone lamps** at structure entrances showing connection state
- **Circuit breaks** when a dependency becomes unhealthy

## How to Enable

```csharp
builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithRedstoneDependencyGraph()
    .WithMonitoredResource(api, "cache")  // api depends on cache
    .WithMonitoredResource(web, "api");   // web depends on api
```

**Note:** Dependencies can be explicit (as above) or auto-detected from `IResourceWithParent`.

## Configuration

No additional configuration. The feature is controlled by the `ASPIRE_FEATURE_REDSTONE_GRAPH` environment variable.

## What You'll See in Minecraft

### Basic Connection

For a simple dependency `API → Redis`:

```
Redis Structure               API Structure
    [Lamp]                       [Lamp]
      ↑                            ↑
      └─── Redstone Wire ─────────┘
```

**Path:** L-shaped wire from Redis to API, with lamps at both structures.

### Wire Path

Redstone wire follows an **L-shaped path**:
1. Start at dependency structure (e.g., Redis)
2. Run horizontally toward dependent structure
3. Turn 90 degrees
4. Run vertically to dependent structure
5. End at dependent structure (e.g., API)

### Repeaters

For long connections (>15 blocks), repeaters boost signal:

```
Redis ───wire───[Repeater]───wire───[Repeater]───wire─── API
```

**Placement:** Every 15 blocks along wire path

### Redstone Lamps

**Dependency structure (source):**
- Lamp placed at structure entrance
- **LIT** when dependency is healthy
- **DARK** when dependency is unhealthy

**Dependent structure (target):**
- Lamp placed at structure entrance
- **LIT** when receiving power (dependency healthy)
- **DARK** when no power (dependency unhealthy)

## Use Cases

### Visualizing Architecture

See your system's dependency graph in 3D:

```csharp
var redis = builder.AddRedis("cache");
var postgres = builder.AddPostgres("db-host");
var db = postgres.AddDatabase("db");
var api = builder.AddProject<Projects.MyApi>("api")
    .WithReference(redis)
    .WithReference(db);
var web = builder.AddProject<Projects.MyWeb>("web")
    .WithReference(api);

builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithRedstoneDependencyGraph()
    .WithMonitoredResource(api)     // Dependencies auto-detected
    .WithMonitoredResource(web)
    .WithMonitoredResource(redis)
    .WithMonitoredResource(postgres);
```

**Result:** Redstone wires show `redis→api`, `postgres→api`, `api→web`.

### Teaching Distributed Systems

Explain service dependencies with physical metaphor:
- **Redstone = data flow / communication**
- **Broken circuit = failed dependency**
- **Lit lamps = healthy connections**

### Debugging Architecture

Walk through the village to understand:
- Which services depend on which
- How failures propagate (circuits break)
- System complexity (wire density)

### Documentation

Take screenshots showing:
- Full system architecture
- Dependency chains
- Failure cascades

## Dependency Detection

### Auto-Detection

Resources with `WithReference()` or `IResourceWithParent`:

```csharp
var api = builder.AddProject<Projects.MyApi>("api")
    .WithReference(redis);  // Auto-detected dependency
```

### Explicit Dependencies

Specify dependencies manually:

```csharp
.WithMonitoredResource(api, "cache", "db")  // api depends on cache and db
```

### Database Parent-Child

Database resources auto-detect host dependency:

```csharp
var postgres = builder.AddPostgres("db-host");
var db = postgres.AddDatabase("db");  // db depends on db-host (auto)

.WithMonitoredResource(postgres)
.WithMonitoredResource(db)  // Wire from db-host to db appears
```

## Technical Details

### Wire Placement

**Y-level:** Y=BaseY-1 (Y=-61, in dirt layer below surface)

**Path calculation:**
1. Determine dependency structure position (X1, Z1)
2. Determine dependent structure position (X2, Z2)
3. Create L-shaped path:
   - Horizontal: X1→X2
   - Vertical: Z1→Z2

### Repeater Spacing

**Every 15 blocks** along the wire path:
- Prevents signal decay (redstone signal travels max 15 blocks)
- Maintains circuit integrity over long distances

**Direction:** Repeaters face the direction of signal flow.

### Lamp Placement

**Position:** Front of structure, at door level (Y=BaseY+1)

**Power source:**
- **Dependency structure:** Powered by redstone torch (always ON when healthy)
- **Dependent structure:** Powered by redstone wire from dependency

### Circuit Breaking

When a dependency becomes unhealthy:
1. **Redstone torch at dependency structure turns OFF**
2. **Wire loses power**
3. **Lamps go DARK**
4. **Visual break** in the circuit

## Visual Reference

### Example Architecture

```
System:
  Redis (cache)
  ├─→ API (depends on cache)
      └─→ Web (depends on api)
```

**In Minecraft:**

```
Redis Structure
  [Lamp:LIT]
     ↓
     └── Redstone Wire ──→ API Structure
                            [Lamp:LIT]
                               ↓
                               └── Redstone Wire ──→ Web Structure
                                                      [Lamp:LIT]
```

**All healthy:** All lamps lit, continuous circuit.

**Redis fails:**

```
Redis Structure
  [Lamp:DARK]
     X (circuit broken)
     
API Structure              Web Structure
  [Lamp:DARK]                [Lamp:DARK]
```

**Cascade effect:** Redis failure breaks circuits to API and Web.

## Combining with Other Features

### With Service Switches

Differentiate between state and dependencies:

```csharp
.WithServiceSwitches()           // Lever shows service state
.WithRedstoneDependencyGraph()   // Wire shows dependencies
```

**Result:**
- **Levers:** Individual service health
- **Wires:** Dependency relationships
- **Lamps:** Connection health

### With Beacon Towers

High-level and detailed views:

```csharp
.WithBeaconTowers()              // Health from afar
.WithRedstoneDependencyGraph()   // Architecture up close
```

### With Village Only

Minimal but informative:

```csharp
.WithRedstoneDependencyGraph()   // Architecture visualization only
```

**Use when:** You want to understand system structure without additional visual clutter.

## Common Patterns

### Simple Chain

```
Redis → API → Web
```

**Wires:** Two L-shaped connections

### Fan-Out

```
Redis → API
      ↘ Worker
```

**Wires:** Two wires from Redis (one to API, one to Worker)

### Fan-In

```
Redis → API
Postgres → API
```

**Wires:** Two wires converging on API

### Complex DAG

```
Redis → API → Web
     ↘ Worker
Postgres → API
```

**Wires:** Four connections showing full dependency graph

## Performance

**Build time:**
- ~2-3 RCON commands per dependency connection
- Repeater placement adds commands for long distances
- Total: ~5-10 seconds for 10 dependencies

**Runtime:**
- Updates only on health state changes
- No continuous polling of wires

**Recommended:** Works well with up to 20 dependency connections.

## Troubleshooting

### Wires not appearing

**Cause:** Dependencies not detected or feature not enabled

**Solution:**
1. Verify `.WithRedstoneDependencyGraph()` is called
2. Check that dependencies are declared (explicitly or via `WithReference`)
3. Review worker logs for errors

### Wires not connected

**Cause:** Long distance without repeaters or placement error

**Solution:** Check worker logs for RCON command errors. File an issue if repeaters are missing.

### Lamps not responding to health

**Cause:** Polling delay or circuit issue

**Solution:**
1. Wait 5-10 seconds for health check cycle
2. Check that dependency structure has redstone torch
3. Verify wire path is continuous

## Code Example

```csharp
using Aspire.Hosting.Minecraft;

var builder = DistributedApplication.CreateBuilder(args);

// Build dependency graph
var redis = builder.AddRedis("cache");
var postgres = builder.AddPostgres("db-host");
var db = postgres.AddDatabase("db");

var api = builder.AddProject<Projects.MyApi>("api")
    .WithReference(redis)
    .WithReference(db);

var web = builder.AddProject<Projects.MyWeb>("web")
    .WithReference(api);

// Visualize with redstone graph
builder.AddMinecraftServer("minecraft")
    .WithAspireWorldDisplay<Projects.Aspire_Hosting_Minecraft_Worker>()
    .WithRedstoneDependencyGraph()
    .WithMonitoredResource(api)
    .WithMonitoredResource(web)
    .WithMonitoredResource(redis)
    .WithMonitoredResource(postgres);

builder.Build().Run();
```

**Result:**
- Village with 4 structures (API, Web, Redis, Postgres)
- Redstone wires: Redis→API, Postgres→API, API→Web
- Lamps show connection health
- Circuit breaks cascade from dependencies to dependents

## Integration

Redstone dependency graph integrates with:
- **[Village Visualization](village-visualization.md)** — Wires connect structures
- **[Service Switches](service-switches.md)** — Separate visual elements
- **[Health Monitoring](health-monitoring.md)** — Lamps show connection health
