# Sprint 4 Technical Design

> **Author:** Rhodey (Lead)
> **Date:** 2026-02-12
> **Requested by:** Jeffrey T. Fritz
> **Status:** 📐 Design — ready for implementation

---

## Table of Contents

1. [Part 1: Redstone Dashboard Wall](#part-1-redstone-dashboard-wall)
2. [Part 2: Enhanced Building Architecture](#part-2-enhanced-building-architecture)
3. [Part 3: Sprint 4 Scope & Issues](#part-3-sprint-4-scope--issues)

---

## Part 1: Redstone Dashboard Wall

### Overview

A wall-mounted grid of redstone lamps that displays health history as a scrolling time-series display. Each row is a resource, each column is a time bucket (most recent on the right). Below the lamp grid, colored concrete bars visualize the health-duration ratio per resource.

### Placement Coordinates

The dashboard is placed **west of the village** (negative X direction), far enough away to not overshadow buildings but close enough to walk to.

Using `VillageLayout` constants:

```
Village western edge:  BaseX - 4 (fence perimeter) = 10 - 4 = 6
Dashboard wall X:      BaseX - 15 = -5  (11 blocks west of fence — clear sightline)
Dashboard wall Z:      BaseZ = 0  (aligned with village front row)
Dashboard wall Y:      SurfaceY + 2 = -58  (two blocks above surface, eye-level for players)
```

The wall faces **east** (toward the village), so players walking out the village gate turn left and see the display.

**Concrete constants to add to `VillageLayout.cs`:**

```csharp
/// <summary>X-coordinate of the dashboard wall's left edge (west of village).</summary>
public const int DashboardX = -5;

/// <summary>Z-coordinate of the dashboard wall's south edge (aligned with village front).</summary>
public const int DashboardZ = BaseZ;

/// <summary>Y-coordinate of the dashboard wall's bottom edge.</summary>
public static int DashboardY => SurfaceY + 2;  // -58 in superflat
```

### Grid Dimensions & Scaling

| Resource Count | Rows | Columns (time buckets) | Wall Size (blocks) | Notes |
|----------------|------|------------------------|---------------------|-------|
| 1–4            | N    | 10                     | N × 10              | Compact |
| 5–8            | N    | 10                     | N × 10              | Standard |
| 9–16           | N    | 8                      | N × 8               | Reduced history |
| 17–30          | N    | 6                      | N × 6               | Minimum viable |
| 31+            | 30   | 6                      | 30 × 6              | Capped; overflow resources omitted |

Each lamp occupies **1 block**. Rows are separated by 1 block of stone (for contrast), so actual wall height = `rows * 2 - 1`. Column spacing is tight: 1 block per time bucket.

**Scaling logic:**

```csharp
public static (int rows, int columns) GetDashboardDimensions(int resourceCount)
{
    var rows = Math.Min(resourceCount, 30);
    var columns = resourceCount switch
    {
        <= 8  => 10,
        <= 16 => 8,
        _     => 6
    };
    return (rows, columns);
}
```

### Wall Construction

**Frame:** Dark stone bricks form a border around the lamp grid. A sign or hologram above displays "HEALTH DASHBOARD".

```
RCON construction sequence (one-time build):
1. /fill {x} {y-1} {z} {x} {y + wallHeight} {z + wallWidth + 1} minecraft:polished_blackstone_bricks
   (solid back wall)
2. For each row r (0..rows-1):
     lamp_y = DashboardY + (r * 2)
     For each col c (0..columns-1):
       lamp_z = DashboardZ + c + 1
       /setblock {DashboardX} {lamp_y} {lamp_z} minecraft:redstone_lamp
   (initialize all lamps — they start unlit)
3. Row label signs (one per row, at z=DashboardZ):
     /setblock {DashboardX} {lamp_y} {DashboardZ} minecraft:oak_wall_sign[facing=east]
     /data merge block {x} {lamp_y} {DashboardZ} {front_text:{messages:["","resourceName","",""]}}
```

**Total RCON commands for initial build:** `1 (back wall) + (rows × columns) lamps + rows signs ≈ 1 + N*10 + N`. For 8 resources: ~89 commands (~9 seconds at 10 cmd/sec). Acceptable for one-time construction.

### Lamp State Encoding

| State | Block | Visual |
|-------|-------|--------|
| Healthy | `minecraft:redstone_lamp[lit=true]` (place redstone block behind) | Bright amber glow |
| Unhealthy | `minecraft:redstone_lamp` (no power) | Dark, unlit |
| Unknown | `minecraft:sea_lantern` | Blue-green glow (distinct) |

**Power mechanism:** To light a redstone lamp, place `minecraft:redstone_block` behind it (`x-1`). To unlight, replace with `minecraft:air`. This avoids complex redstone wiring.

```
To light lamp at (x, y, z):
  /setblock {x-1} {y} {z} minecraft:redstone_block

To unlight lamp at (x, y, z):
  /setblock {x-1} {y} {z} minecraft:air
```

### Shift-Register Scrolling via /clone

Instead of updating every lamp individually each cycle (N×columns commands), use Minecraft's `/clone` command to shift the entire grid left by one column, then only write the new rightmost column.

**RCON command sequence per update cycle:**

```
Step 1 — Shift existing data left by 1 column:
  For each row r:
    source: (DashboardX-1, lamp_y, DashboardZ+2) to (DashboardX-1, lamp_y, DashboardZ+columns)
    dest:   (DashboardX-1, lamp_y, DashboardZ+1)
    /clone {x-1} {lamp_y} {z+2} {x-1} {lamp_y} {z+columns} {x-1} {lamp_y} {z+1} replace

  Alternative: clone entire slab in one command if rows are contiguous:
    /clone {x-1} {y_bottom} {z+2} {x-1} {y_top} {z+columns} {x-1} {y_bottom} {z+1} replace

Step 2 — Write new column (rightmost, z = DashboardZ + columns):
  For each row r:
    If healthy: /setblock {x-1} {lamp_y} {z+columns} minecraft:redstone_block
    If unhealthy: /setblock {x-1} {lamp_y} {z+columns} minecraft:air
```

**RCON budget per cycle:** 1 clone command + N setblock commands = **N+1 commands** per update. For 8 resources: 9 commands (~1 second). This runs every `DisplayUpdateInterval` (10 seconds) — well within budget.

**Why /clone works:** `/clone` copies blocks including their state. Redstone blocks behind lamps get cloned along with the lamps, preserving lit/unlit state. The entire power layer shifts with one command.

### Health History Ring Buffer

Add to `AspireResourceMonitor` (or a new `HealthHistoryTracker` class):

```csharp
internal sealed class HealthHistoryTracker
{
    // Ring buffer: resource name → fixed-size array of statuses
    private readonly Dictionary<string, ResourceStatus[]> _history = new();
    private readonly int _maxColumns;

    public HealthHistoryTracker(int maxColumns = 10)
    {
        _maxColumns = maxColumns;
    }

    /// <summary>
    /// Records current health snapshot. Called each poll cycle.
    /// Shifts existing entries left; newest entry at index [_maxColumns-1].
    /// </summary>
    public void RecordSnapshot(IReadOnlyDictionary<string, ResourceInfo> resources)
    {
        foreach (var (name, info) in resources)
        {
            if (!_history.TryGetValue(name, out var buffer))
            {
                buffer = new ResourceStatus[_maxColumns];
                Array.Fill(buffer, ResourceStatus.Unknown);
                _history[name] = buffer;
            }
            // Shift left
            Array.Copy(buffer, 1, buffer, 0, _maxColumns - 1);
            // Write newest
            buffer[_maxColumns - 1] = info.Status;
        }
    }

    public IReadOnlyDictionary<string, ResourceStatus[]> History => _history;
}
```

**Integration with MinecraftWorldWorker:** The `HealthHistoryTracker` is injected into the worker. After each `PollHealthAsync()` call, call `historyTracker.RecordSnapshot(resourceMonitor.Resources)`. The `RedstoneDashboardService` reads `historyTracker.History` to update the wall.

### Concrete Bar Chart (Below Dashboard)

Below the lamp grid, a row of colored concrete columns visualizes per-resource health duration as a bar chart.

**Placement:** Starting at `DashboardY - 1` (one block below lamp grid), bars extend downward.

| Health Duration % | Bar Height (blocks) | Concrete Color |
|-------------------|---------------------|----------------|
| 90–100% | 5 | `minecraft:lime_concrete` |
| 70–89% | 4 | `minecraft:yellow_concrete` |
| 50–69% | 3 | `minecraft:orange_concrete` |
| 0–49% | 1–2 | `minecraft:red_concrete` |

Each resource gets a 1-wide column at the Z-coordinate matching its lamp row. Height is proportional to `healthyCount / totalSnapshots` from the ring buffer.

```
For each resource r at z-offset:
  health_pct = count(status == Healthy in history) / history.length * 100
  bar_height = health_pct >= 90 ? 5 : health_pct >= 70 ? 4 : health_pct >= 50 ? 3 : max(1, health_pct / 25)
  color = health_pct >= 90 ? "lime_concrete" : health_pct >= 70 ? "yellow_concrete" : ...

  // Clear old bar
  /fill {DashboardX} {DashboardY-6} {z_offset} {DashboardX} {DashboardY-1} {z_offset} minecraft:air

  // Place new bar
  /fill {DashboardX} {DashboardY-bar_height} {z_offset} {DashboardX} {DashboardY-1} {z_offset} minecraft:{color}
```

**RCON cost:** 2 commands per resource per cycle (clear + place) = 2N commands. For 8 resources: 16 commands. Runs every update cycle.

### Extension Method: `WithRedstoneDashboard()`

Follows the established pattern:

```csharp
/// <summary>
/// Enables the Redstone Dashboard Wall — a time-series grid of redstone lamps
/// showing per-resource health history. Includes a scrolling display that shifts
/// left each poll cycle, with colored concrete bar charts below showing uptime
/// percentages. Placed west of the village at eye level.
/// Requires <see cref="WithAspireWorldDisplay{TWorkerProject}"/> to be called first.
/// </summary>
public static IResourceBuilder<MinecraftServerResource> WithRedstoneDashboard(
    this IResourceBuilder<MinecraftServerResource> builder)
{
    var workerBuilder = builder.Resource.WorkerBuilder
        ?? throw new InvalidOperationException(
            "WithRedstoneDashboard() requires WithAspireWorldDisplay() to be called first.");

    workerBuilder.WithEnvironment("ASPIRE_FEATURE_REDSTONE_DASHBOARD", "true");
    return builder;
}
```

**Worker service:** `RedstoneDashboardService` — registered conditionally in `Program.cs` when `ASPIRE_FEATURE_REDSTONE_DASHBOARD` is set. Injected as `RedstoneDashboardService? redstoneDashboard = null` in `MinecraftWorldWorker`. Called in the continuous features section: `if (redstoneDashboard is not null) await redstoneDashboard.UpdateAsync(stoppingToken);`

### Total RCON Budget (Dashboard)

| Phase | Commands | Frequency | Time |
|-------|----------|-----------|------|
| Initial build | ~90 (8 resources) | Once | ~9s |
| Scroll + new column | 9 | Every 10s | <1s |
| Bar chart update | 16 | Every 10s | ~1.6s |
| **Per-cycle total** | **25** | **Every 10s** | **~2.5s** |

This leaves ~7.5 seconds of RCON headroom per 10-second cycle — fine alongside other services.

---

## Part 2: Enhanced Building Architecture

### Azure Resource Detection

Resources are detected via `ASPIRE_RESOURCE_{NAME}_TYPE` environment variables. The type string determines the building style.

**Detection strategy — resource type string matching:**

```csharp
internal static bool IsAzureResource(string resourceType)
{
    var lower = resourceType.ToLowerInvariant();
    return lower.StartsWith("azure.")
        || lower.Contains("azure")
        || lower is "cosmosdb" or "servicebus" or "signalr"
                  or "appinsights" or "applicationinsights";
}

internal static bool IsDatabaseResource(string resourceType)
{
    var lower = resourceType.ToLowerInvariant();
    return lower is "postgres" or "postgresql" or "sqlserver" or "mysql"
                  or "mongodb" or "redis" or "garnet"
                  or "oracle" or "cosmos" or "cosmosdb"
                  or "milvus" or "qdrant" or "elasticsearch"
        || lower.Contains("database") || lower.Contains("db")
        || lower.Contains("sql") || lower.Contains("cache")
        || lower.Contains("redis") || lower.Contains("mongo");
}
```

**Why string matching, not a separate package:** These are name-based heuristics on types that Aspire already provides. No Azure SDK dependency needed. The same detection works whether the resource comes from local Docker containers (e.g., a PostgreSQL container) or Azure resources. If a resource is _both_ Azure and a database (e.g., Azure SQL), the database shape takes priority with an Azure banner added on top.

### Updated Structure Type Mapping

```csharp
internal static string GetStructureType(string resourceType)
{
    if (IsDatabaseResource(resourceType))
        return "Cylinder";

    if (IsAzureResource(resourceType))
        return "AzureThemed";

    return resourceType.ToLowerInvariant() switch
    {
        "project"    => "Watchtower",
        "container"  => "Warehouse",
        "executable" => "Workshop",
        _            => "Cottage"
    };
}
```

| Aspire Type | Structure | Description |
|-------------|-----------|-------------|
| `project` | Watchtower | Tall tower with flag (unchanged) |
| `container` | Warehouse | Flat iron building (unchanged) |
| `executable` | Workshop | Oak building with chimney (unchanged) |
| `postgres`, `redis`, `sqlserver`, etc. | **Cylinder** | Round building — database cylinder icon |
| `azure.*`, `cosmosdb`, `servicebus`, etc. | **AzureThemed** | Cottage-style with azure blue banner |
| Unknown/other | Cottage | Default humble dwelling (unchanged) |

### Cylinder Building (Database Resources)

A cylindrical building evoking the classic database icon from architecture diagrams. Must fit within the existing 7×7 grid cell.

**Geometry:**

- **Radius:** 3 blocks (diameter 7 — fills the 7×7 footprint exactly)
- **Height:** 6 blocks (walls), total 7 with roof cap
- **Block palette:** `polished_deepslate` walls, `deepslate_tiles` floor, `smooth_quartz` cap

**Circular footprint (7×7 grid, center at x+3, z+3):**

```
Row 0:  . . # # # . .
Row 1:  . # # # # # .
Row 2:  # # # # # # #
Row 3:  # # # # # # #   ← center row
Row 4:  # # # # # # #
Row 5:  . # # # # # .
Row 6:  . . # # # . .
```

Where `#` = block placed, `.` = air/empty.

**RCON construction commands:**

```
// Floor (circular)
/fill {x+2} {y} {z} {x+4} {y} {z} minecraft:deepslate_tiles           // row 0: 3 wide
/fill {x+1} {y} {z+1} {x+5} {y} {z+1} minecraft:deepslate_tiles       // row 1: 5 wide
/fill {x} {y} {z+2} {x+6} {y} {z+4} minecraft:deepslate_tiles         // rows 2-4: full 7 wide
/fill {x+1} {y} {z+5} {x+5} {y} {z+5} minecraft:deepslate_tiles       // row 5: 5 wide
/fill {x+2} {y} {z+6} {x+4} {y} {z+6} minecraft:deepslate_tiles       // row 6: 3 wide

// Walls: Same circular pattern, repeated from y+1 to y+6, but ONLY the perimeter
// (hollow interior — place outer ring blocks only)
For each wall_y from y+1 to y+6:
  // Row 0 perimeter: 3 blocks
  /fill {x+2} {wall_y} {z} {x+4} {wall_y} {z} minecraft:polished_deepslate
  // Row 1 corners only
  /setblock {x+1} {wall_y} {z+1} minecraft:polished_deepslate
  /setblock {x+5} {wall_y} {z+1} minecraft:polished_deepslate
  // Row 2-4 sides only
  /setblock {x} {wall_y} {z+2} minecraft:polished_deepslate
  /setblock {x+6} {wall_y} {z+2} minecraft:polished_deepslate
  /setblock {x} {wall_y} {z+3} minecraft:polished_deepslate
  /setblock {x+6} {wall_y} {z+3} minecraft:polished_deepslate
  /setblock {x} {wall_y} {z+4} minecraft:polished_deepslate
  /setblock {x+6} {wall_y} {z+4} minecraft:polished_deepslate
  // Row 5 corners
  /setblock {x+1} {wall_y} {z+5} minecraft:polished_deepslate
  /setblock {x+5} {wall_y} {z+5} minecraft:polished_deepslate
  // Row 6 perimeter: 3 blocks
  /fill {x+2} {wall_y} {z+6} {x+4} {wall_y} {z+6} minecraft:polished_deepslate

// Roof cap (same circular pattern as floor, with smooth_quartz)
// Same 5-command pattern as floor, at y+7, using minecraft:smooth_quartz
```

**Optimization:** The wall ring is identical for all 6 levels. Use a helper method that takes `y` and places one ring, then loop.

**RCON command count:** Floor (5) + Walls (6 levels × 13 commands per ring = 78) + Roof (5) = **~88 commands per cylinder**. This is ~4× more than a watchtower due to the circular geometry. Mitigation: the `/fill` commands handle straight segments, keeping it manageable.

**Interior decoration:**

```
// Brewing stand (data processing metaphor)
/setblock {x+3} {y+1} {z+3} minecraft:brewing_stand

// Cauldron (data storage metaphor)
/setblock {x+2} {y+1} {z+3} minecraft:cauldron[level=3]
```

### Door and Entrance for Cylinder

The front entrance is on the Z-min side (south-facing), matching all other structures. Since the cylinder wall at z is only 3 blocks wide (x+2 to x+4), the door is 2 blocks wide centered:

```
// Door opening: 2 wide × 2 tall at z=0 (row 0 of circular footprint)
/fill {x+3} {y+1} {z} {x+4} {y+2} {z} minecraft:air
// Alternative: 1-wide door at exact center (x+3)
/fill {x+3} {y+1} {z} {x+3} {y+2} {z} minecraft:air
```

Recommendation: Use a **1-wide entrance at x+3** (centered). Round buildings have narrow doorways — it's architecturally appropriate and avoids structural weakness in the thin curved wall.

### Health Indicator for Cylinder

The health lamp goes in the front wall at center, above the door:

```csharp
// Cylinder: front wall perimeter at z+0 (row 0), centered at x+3
// Door is y+1 to y+2, so lamp at y+3
var lampZ = z;      // front perimeter at z
var lampY = y + 3;  // above 2-tall door
// Place: setblock {x+3} {y+3} {z} {lampBlock}
```

This is consistent: lamp is always at `x+3` (centered), in the front wall, above the door.

### Azure Blue Banner/Flag

Azure-themed resources (and databases that are also Azure) get a bright azure blue banner on the rooftop. This signals "this resource comes from Azure" at a glance.

**Banner block:** `minecraft:light_blue_banner[rotation=0]`

The `light_blue` dye color in Minecraft (#3AB3DA) is the closest match to Azure blue (#0078D4). For the Aspire color palette, `light_blue_banner` is the right choice — it's distinctive against the existing blue (watchtower) and purple (warehouse) accents.

**Banner pattern for Azure feel:**

A plain `light_blue_banner` is fine for v1. For extra flair, banners support patterns via NBT:

```
// Plain azure banner (simple, clean)
/setblock {x+3} {y+height+1} {z+3} minecraft:light_blue_banner[rotation=0]

// Optional: banner with gradient pattern (resembles Azure logo shape)
/setblock {x+3} {y+height+1} {z+3} minecraft:light_blue_banner[rotation=0]
/data merge block {x+3} {y+height+1} {z+3} {Patterns:[{Pattern:"gra",Color:11},{Pattern:"gru",Color:3}]}
```

Pattern breakdown:
- `gra` = gradient (top-to-bottom fade) in `Color:11` (blue)
- `gru` = gradient upside-down in `Color:3` (light blue)

This creates a two-tone blue gradient reminiscent of the Azure brand.

**Placement rules:**

| Structure Type | Banner Position | Notes |
|----------------|----------------|-------|
| Watchtower | `(x+3, y+11, z+2)` | On existing flag pole, replace blue_banner |
| Warehouse | `(x+3, y+6, z+3)` | On flat roof, centered |
| Workshop | `(x+3, y+7, z+3)` | Above peaked roof |
| Cylinder | `(x+3, y+8, z+3)` | On roof cap, centered |
| Cottage | `(x+3, y+6, z+3)` | On flat roof, centered |
| AzureThemed | `(x+3, y+6, z+3)` | Always gets azure banner |

**Logic:** After building any structure, if `IsAzureResource(resourceType)` is true, place the azure banner at the roof position. Database cylinders that are also Azure get both the cylinder shape AND the azure banner — double visual signal.

### AzureThemed Structure (Non-Database Azure Resources)

For Azure resources that aren't databases (e.g., Azure Storage, Azure Key Vault, Azure Service Bus), use a modified Cottage with Azure accents:

- **Walls:** `light_blue_concrete` instead of cobblestone
- **Trim:** `blue_concrete` instead of light_blue_wool
- **Roof:** `blue_stained_glass` slab (translucent, distinctive)
- **Azure banner** on rooftop (always)

This is a minimal diff from `BuildCottageAsync` — change 3 block types and add the banner. No new geometry needed.

```csharp
private async Task BuildAzureThemedAsync(int x, int y, int z, CancellationToken ct)
{
    // Same as Cottage but with Azure color palette
    await rcon.SendCommandAsync(
        $"fill {x} {y} {z} {x + 6} {y} {z + 6} minecraft:light_blue_concrete", ct);
    await rcon.SendCommandAsync(
        $"fill {x} {y + 1} {z} {x + 6} {y + 4} {z + 6} minecraft:light_blue_concrete hollow", ct);
    // Blue trim at top
    await rcon.SendCommandAsync(
        $"fill {x} {y + 4} {z} {x + 6} {y + 4} {z} minecraft:blue_concrete", ct);
    await rcon.SendCommandAsync(
        $"fill {x} {y + 4} {z + 6} {x + 6} {y + 4} {z + 6} minecraft:blue_concrete", ct);
    await rcon.SendCommandAsync(
        $"fill {x} {y + 4} {z + 1} {x} {y + 4} {z + 5} minecraft:blue_concrete", ct);
    await rcon.SendCommandAsync(
        $"fill {x + 6} {y + 4} {z + 1} {x + 6} {y + 4} {z + 5} minecraft:blue_concrete", ct);
    // Flat roof
    await rcon.SendCommandAsync(
        $"fill {x} {y + 5} {z} {x + 6} {y + 5} {z + 6} minecraft:light_blue_stained_glass", ct);
    // Door
    await rcon.SendCommandAsync(
        $"fill {x + 2} {y + 1} {z} {x + 3} {y + 2} {z} minecraft:air", ct);
    // Windows
    await rcon.SendCommandAsync(
        $"setblock {x + 1} {y + 2} {z} minecraft:blue_stained_glass_pane", ct);
    await rcon.SendCommandAsync(
        $"setblock {x + 5} {y + 2} {z} minecraft:blue_stained_glass_pane", ct);
    // Azure banner on roof
    await rcon.SendCommandAsync(
        $"setblock {x + 3} {y + 6} {z + 3} minecraft:light_blue_banner[rotation=0]", ct);
}
```

### Health Indicator Position Summary (All Structure Types)

| Structure | Front Wall Z | Door Height | Lamp (x, y, z) |
|-----------|-------------|-------------|-----------------|
| Watchtower | z+1 | 3 (y+1 to y+3) | (x+3, y+4, z+1) |
| Warehouse | z | 3 (y+1 to y+3) | (x+3, y+4, z) |
| Workshop | z | 2 (y+1 to y+2) | (x+3, y+3, z) |
| Cottage | z | 2 (y+1 to y+2) | (x+3, y+3, z) |
| **Cylinder** | z | 2 (y+1 to y+2) | (x+3, y+3, z) |
| **AzureThemed** | z | 2 (y+1 to y+2) | (x+3, y+3, z) |

Updated `PlaceHealthIndicatorAsync`:

```csharp
private async Task PlaceHealthIndicatorAsync(int x, int y, int z, string structureType, ResourceStatus status, CancellationToken ct)
{
    var lampBlock = status switch
    {
        ResourceStatus.Healthy => "minecraft:glowstone",
        ResourceStatus.Unhealthy => "minecraft:redstone_lamp",
        _ => "minecraft:sea_lantern"
    };

    var lampZ = structureType == "Watchtower" ? z + 1 : z;
    var lampY = (structureType is "Watchtower" or "Warehouse") ? y + 4 : y + 3;

    await rcon.SendCommandAsync(
        $"setblock {x + 3} {lampY} {lampZ} {lampBlock}", ct);
}
```

No change needed for Cylinder or AzureThemed — they both use front wall at `z` with 2-tall doors, same as Workshop and Cottage.

---

## Part 3: Sprint 4 Scope & Issues

### Sprint 4 Theme: "Visual Identity & Dashboard"

Sprint 4 adds two headline features (Redstone Dashboard, Enhanced Buildings) plus Jeff's earlier Sprint 4 ideas (WithAllFeatures, env var tightening, welcome teleport, Dragon Egg) and required docs/README updates.

### Final Issue List

#### Core Features

| # | Title | Description | Size | Assignee |
|---|-------|-------------|------|----------|
| 1 | **WithAllFeatures() convenience method** | Add `WithAllFeatures()` extension method that calls all 16+ `With*()` feature methods. Includes XML docs listing which features are enabled. Guards on `WithAspireWorldDisplay()` like all other feature methods. | S | **Shuri** |
| 2 | **Tighten feature env var checks to == "true"** | Change all `!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPIRE_FEATURE_*"))` checks in `Program.cs` to `== "true"`. Prevents accidental feature activation from empty or junk env var values. Update all 16 existing feature registrations. | S | **Shuri** |
| 3 | **Welcome teleport on player join** | When a player joins the server, teleport them to the village entrance (fence gate coordinates) and show a welcome title. Listen for join events via RCON `list` polling or server log monitoring. One-time per player per session. | M | **Rocket** |
| 4 | **Dragon Health Egg monument** | Build the Dragon Egg SLO monument at village center: obsidian pedestal, End Crystals per resource (beams when healthy, particle explosion when unhealthy), Purpur progress ring for uptime. New `WithDragonEgg()` extension method, `DragonEggService` in worker. | L | **Rocket** |

#### Enhanced Buildings

| # | Title | Description | Size | Assignee |
|---|-------|-------------|------|----------|
| 5 | **Cylinder buildings for database resources** | Add `BuildCylinderAsync()` to `StructureBuilder` using circular geometry within 7×7 footprint. Deepslate/quartz palette. Add `IsDatabaseResource()` detection method matching postgres, redis, sqlserver, mongodb, etc. Update `GetStructureType()` mapping. | M | **Rocket** |
| 6 | **Azure-themed buildings with blue banner** | Add `BuildAzureThemedAsync()` — Cottage variant with light_blue_concrete walls, blue trim, and azure banner. Add `IsAzureResource()` detection method. Place azure banner on any structure type when resource is Azure-sourced (including database cylinders). | M | **Rocket** |
| 7 | **Update health indicator for new structure types** | Ensure `PlaceHealthIndicatorAsync()` handles Cylinder and AzureThemed types correctly. Door height and front-wall Z offset must be consistent. Add unit tests for all 6 structure types. | S | **Nebula** |

#### Redstone Dashboard

| # | Title | Description | Size | Assignee |
|---|-------|-------------|------|----------|
| 8 | **Redstone Dashboard Wall construction** | Build the lamp grid west of village. `RedstoneDashboardService` with initial wall build, row labels, and frame. Add `DashboardX/Y/Z` constants to `VillageLayout`. Scaling logic for grid dimensions based on resource count. | M | **Rocket** |
| 9 | **Health history ring buffer** | Implement `HealthHistoryTracker` with per-resource ring buffer. Integrate with `MinecraftWorldWorker` to record snapshots each poll cycle. Expose `History` dictionary for dashboard consumption. | S | **Shuri** |
| 10 | **Dashboard scroll and bar chart updates** | Implement `/clone`-based shift-register scrolling per cycle. Write new column from latest health snapshot. Render concrete bar chart below grid showing uptime percentage per resource. | M | **Rocket** |
| 11 | **WithRedstoneDashboard() extension method** | Add extension method following established pattern. Wire `RedstoneDashboardService` conditional registration in `Program.cs`. Add to `MinecraftWorldWorker` constructor and update loop. | S | **Shuri** |

#### Documentation & Polish

| # | Title | Description | Size | Assignee |
|---|-------|-------------|------|----------|
| 12 | **README update for Sprint 4 features** | Add new features to README: WithRedstoneDashboard, WithDragonEgg, WithAllFeatures, enhanced building styles. Update code sample. Add screenshot placeholders. | S | **Shuri** |
| 13 | **User docs for Sprint 4 features** | Create user-facing docs in `user-docs/` for: Redstone Dashboard, Dragon Health Egg, Enhanced Buildings, Welcome Teleport. Follow existing doc format (What → How → What You'll See → Use Cases → Code Example). | M | **Vision** |
| 14 | **Unit tests for Sprint 4 features** | Tests for: `IsAzureResource()`, `IsDatabaseResource()`, `GetStructureType()` with new types, `HealthHistoryTracker` ring buffer, dashboard dimension scaling. Target: 30+ new tests. | M | **Nebula** |

### What Defers (Not in Sprint 4)

| Feature | Reason |
|---------|--------|
| Sculk Error Network | Cool but Sprint 4 is already full. Sprint 5 candidate. |
| IRconCommandSender interface | Nice refactor but no user-facing value. Backlog. |
| Azure SDK integration package | Multi-sprint epic, not ready. Sprint 5+. |
| Trace River / OTLP features | Requires architectural OTLP ingestion work. Sprint 5 epic. |
| NuGet package icon | Nice-to-have. Quick win for Sprint 5 or any sprint with slack. |

### Sprint 4 Summary

- **14 issues** total
- **Size breakdown:** 6 Small + 6 Medium + 1 Large + 1 Medium (docs) = ~24 story points (using S=1, M=2, L=4)
- **New extension methods:** `WithRedstoneDashboard()`, `WithDragonEgg()`, `WithAllFeatures()`
- **New services:** `RedstoneDashboardService`, `DragonEggService`, `HealthHistoryTracker`, `WelcomeTeleportService`
- **Modified files:** `StructureBuilder.cs`, `VillageLayout.cs`, `AspireResourceMonitor.cs` (or new detection helper), `MinecraftServerBuilderExtensions.cs`, `Program.cs`
- **New user docs:** 4 pages in `user-docs/`
- **Target test count:** 30+ new tests (390+ total)

### Milestone

All 14 issues should be created under a **"Sprint 4 — Visual Identity & Dashboard"** GitHub milestone.

---

## Appendix: Block ID Reference

| Block | Minecraft ID | Used For |
|-------|-------------|----------|
| Redstone Lamp | `minecraft:redstone_lamp` | Dashboard health grid |
| Redstone Block | `minecraft:redstone_block` | Powers lamps (lit state) |
| Polished Blackstone Bricks | `minecraft:polished_blackstone_bricks` | Dashboard frame |
| Polished Deepslate | `minecraft:polished_deepslate` | Cylinder walls |
| Deepslate Tiles | `minecraft:deepslate_tiles` | Cylinder floor |
| Smooth Quartz | `minecraft:smooth_quartz` | Cylinder roof cap |
| Light Blue Concrete | `minecraft:light_blue_concrete` | Azure building walls |
| Blue Concrete | `minecraft:blue_concrete` | Azure building trim |
| Light Blue Banner | `minecraft:light_blue_banner` | Azure flag/banner |
| Light Blue Stained Glass | `minecraft:light_blue_stained_glass` | Azure building roof |
| Lime Concrete | `minecraft:lime_concrete` | Bar chart: 90-100% |
| Yellow Concrete | `minecraft:yellow_concrete` | Bar chart: 70-89% |
| Orange Concrete | `minecraft:orange_concrete` | Bar chart: 50-69% |
| Red Concrete | `minecraft:red_concrete` | Bar chart: 0-49% |
| Dragon Egg | `minecraft:dragon_egg` | SLO monument |
| End Crystal | Entity: `ender_crystal` | Per-resource health beam |
| Purpur Block | `minecraft:purpur_block` | Uptime progress ring |
| End Stone | `minecraft:end_stone` | SLO ring base |
