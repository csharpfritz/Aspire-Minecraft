# Minecraft Building Constraints and Conventions

This document serves as the **authoritative source of truth** for all Minecraft building rules, coordinate conventions, and technical constraints used in Aspire.Hosting.Minecraft. All code that interacts with the Minecraft world must follow these conventions.

---

## Table of Contents

1. [Y-Level Conventions](#y-level-conventions)
2. [Structure Size Limits](#structure-size-limits)
3. [Coordinate Boundaries and Safe Zones](#coordinate-boundaries-and-safe-zones)
4. [RCON Rate Limits](#rcon-rate-limits)
5. [Z-Coordinate Conventions](#z-coordinate-conventions)
6. [Path Placement Rules](#path-placement-rules)
7. [Fence Placement Rules](#fence-placement-rules)
8. [Maximum Resource Limits](#maximum-resource-limits)
9. [World Configuration](#world-configuration)

---

## Y-Level Conventions

### Superflat World Y-Levels

The project uses Minecraft superflat terrain (`LEVEL_TYPE=flat`). The Y-axis coordinate system is as follows:

| Y-Level | Block Type | Purpose |
|---------|-----------|---------|
| Y=-64   | Bedrock   | Unbreakable world foundation |
| Y=-63   | Bedrock   | Part of bedrock layer |
| Y=-62   | Bedrock   | Part of bedrock layer |
| Y=-61   | Dirt      | Subsurface layer; **paths are placed here** |
| Y=-60   | Grass     | **BaseY** — Surface level; structure floors, fences placed here |
| Y=-59   | Air       | Ground level where players walk |

### BaseY Definition

**`BaseY = -60`** is the grass block surface in superflat worlds.

- **Structure floors**: Placed at `BaseY` (Y=-60)
- **Structure walls**: Start at `BaseY + 1` (Y=-59) and extend upward
- **Fences/barriers**: Placed at `BaseY` (Y=-60), sitting directly on grass
- **Paths**: Placed at `BaseY - 1` (Y=-61) in the dirt layer, with grass at `BaseY` cleared for flush appearance
- **Player walking level**: `BaseY + 1` (Y=-59)

### ⚠️ Critical Y-Level Rules

1. **Never place fences at `BaseY + 1`** — they will float one block above the ground.
2. **Paths must be at `BaseY - 1`** with grass cleared at `BaseY` — this makes them flush with the surrounding terrain.
3. **Structure floors must be at `BaseY`** — this ensures proper alignment with the world surface.

---

## Structure Size Limits

### Footprint and Height

All village structures follow standardized dimensions:

| Structure Type | Resource Type | Footprint | Height (blocks above BaseY) | Total Y Range |
|----------------|---------------|-----------|----------------------------|---------------|
| Watchtower     | Project       | 7×7       | 10 (+ flag pole at Y+11)   | Y=-60 to Y=-49 |
| Warehouse      | Container     | 7×7       | 5                          | Y=-60 to Y=-55 |
| Workshop       | Executable    | 7×7       | 7 (+ chimney at Y+8)       | Y=-60 to Y=-52 |
| Cottage        | Unknown/Other | 7×7       | 5                          | Y=-60 to Y=-55 |

### Layout Constants

From `VillageLayout.cs`:

```csharp
public const int BaseX = 10;        // X-coordinate of first column
public const int BaseY = -60;       // Surface level (grass block)
public const int BaseZ = 0;         // Z-coordinate of first row
public const int Spacing = 10;      // Center-to-center distance between structures
public const int Columns = 2;       // Number of columns in grid
public const int StructureSize = 7; // Structure footprint width/depth (7×7)
```

### Structure Spacing

- **Spacing = 10 blocks** center-to-center between adjacent structures
- **Gap between structures**: 10 - 7 = **3 blocks**
- This gap provides clear visual separation and prevents overlap

---

## Coordinate Boundaries and Safe Zones

### World Boundaries

Configured in `MinecraftServerBuilderExtensions.cs`:

- **MAX_WORLD_SIZE = 256** blocks (world border diameter)
- **World center**: X=0, Z=0
- **Usable radius**: ±128 blocks from origin
- **Village starts**: X=10, Z=0 (well within safe zone)

### Village Bounds Calculation

For a village with `N` resources:

```
rows = ⌈N / 2⌉ (ceiling division)
cols = min(N, 2)

Village bounds (structure footprints only):
  minX = BaseX = 10
  minZ = BaseZ = 0
  maxX = BaseX + ((cols - 1) × 10) + 6
  maxZ = BaseZ + ((rows - 1) × 10) + 6

Example (4 resources = 2 rows × 2 cols):
  maxX = 10 + 10 + 6 = 26
  maxZ = 0 + 10 + 6 = 16
```

### Safe Zones and Clearances

- **Fence clearance**: 4 blocks beyond village bounds on all sides
- **Path clearance**: 1 block inside fence perimeter
- **Spawn protection**: `SPAWN_PROTECTION=0` (disabled for demos)

---

## RCON Rate Limits

### Rate Limiting Configuration

From `RconService.cs`:

- **MaxCommandsPerSecond = 10** (configurable, defaults to 10)
- **Throttle interval = 250ms** (duplicate command suppression)
- **Command priority levels**:
  - **High priority**: Health updates, player messages — **bypass rate limits**
  - **Low priority**: Visual effects, decorations — **queued when rate-limited**

### Command Timing

- **Single command latency**: ~10-50ms (network + server processing)
- **Throttle window**: 250ms between duplicate commands
- **Rate limit**: 100ms between commands (10 commands/sec)

### Building Operation Costs

From testing and production use:

| Operation | Command Count | Estimated Time (at 10 cmd/sec) |
|-----------|---------------|--------------------------------|
| Single structure (watchtower) | ~15-20 commands | 1.5-2 seconds |
| Paths (entire village) | 2-3 commands | 0.2-0.3 seconds |
| Fence perimeter | 6-8 commands | 0.6-0.8 seconds |
| 4-resource village (initial build) | ~70-80 commands | 7-8 seconds |
| 50-resource village (initial build) | ~800-900 commands | 80-90 seconds |

### ⚠️ RCON Performance Constraints

1. **Initial world construction is the bottleneck**: 50 resources × 15 commands × 100ms throttle = ~75 seconds minimum.
2. **Duplicate command suppression**: Commands sent within 250ms of identical previous command are skipped (prevents flicker/glitching).
3. **High-priority commands bypass rate limits**: Critical health updates and player messages are never queued.
4. **Low-priority commands queue**: Visual effects and decorations queue when rate-limited, processed when capacity available.

### Azure Integration Considerations

From `.ai-team/agents/rhodey/history.md`:

> **RCON throughput is a hidden constraint for Azure.** 50 resources × ~15 fill commands × 250ms throttle = ~3 minutes for initial world build. The Aspire path typically has 3–8 resources, so this never surfaced. May need batch mode or throttle bypass for initial construction.

For large Azure resource groups (200+ resources), consider:
- Batch construction mode (reduce per-structure command count)
- Resource type filtering (exclude low-priority resource types)
- Increase `MaxCommandsPerSecond` for initial build phase

---

## Z-Coordinate Conventions

### Front Wall Placement

All structures face **south** (Z-min direction). The front entrance is always on the **Z-min side**:

- **Hollow structures (Watchtower)**: 
  - 5×5 inner hollow box starts at `origin + (1, 1, 1)`
  - **Front wall is at `z + 1`** (one block offset from origin)
  - Door clearing: `z + 1` (actual wall location)

- **Solid structures (Warehouse, Workshop, Cottage)**:
  - Walls fill entire 7×7 footprint
  - **Front wall is at `z`** (origin edge)
  - Door clearing: `z` (origin edge)

### Door Placement Logic

From `StructureBuilder.cs`:

```csharp
// Watchtower (hollow structure)
// Front wall at z+1, so door clears at z+1
$"fill {x + 2} {y + 1} {z + 1} {x + 4} {y + 3} {z + 1} minecraft:air"

// Warehouse/Workshop/Cottage (solid structures)
// Front wall at z (origin edge), so door clears at z
$"fill {x + 2} {y + 1} {z} {x + 3} {y + 2} {z} minecraft:air"
```

### Health Indicator Placement

Health lamps are placed in the front wall at:

```csharp
// Watchtower: front wall at z+1
var lampZ = z + 1;

// Others: front wall at z
var lampZ = z;

// Lamp coordinates: (x + 3, y + 5, lampZ)
// - X: Centered horizontally (x + 3 = center of 7-block width)
// - Y: y + 5 (5 blocks above BaseY, above door height)
// - Z: Embedded in actual wall face, not origin
```

---

## Path Placement Rules

### Path Construction Process

Paths cover the entire village area inside the fence, creating a recessed cobblestone plaza.

**Two-step process** (from `StructureBuilder.BuildPathsAsync`):

1. **Clear grass surface at BaseY**:
   ```bash
   /fill {minX} -60 {minZ} {maxX} -60 {maxZ} minecraft:air replace grass_block
   ```
   This removes grass blocks at Y=-60, leaving air.

2. **Place cobblestone at BaseY - 1**:
   ```bash
   /fill {minX} -61 {minZ} {maxX} -61 {maxZ} minecraft:cobblestone
   ```
   This places cobblestone in the dirt layer at Y=-61.

### Result

- Cobblestone sits at Y=-61 (dirt layer)
- Grass removed at Y=-60 (surface layer)
- Paths are **flush with surrounding grass** (both at effective surface level)
- Players walk on cobblestone at the same visual level as grass

### Path Bounds

```
Path area (inside fence perimeter):
  minX = fenceMinX + 1
  maxX = fenceMaxX - 1
  minZ = fenceMinZ + 1
  maxZ = fenceMaxZ - 1
  Y = BaseY - 1 (-61)
```

---

## Fence Placement Rules

### Fence Perimeter Calculation

Fences surround the village with a **4-block clearance** from structure edges.

From `VillageLayout.GetFencePerimeter`:

```csharp
public static (int minX, int minZ, int maxX, int maxZ) GetFencePerimeter(int resourceCount)
{
    var (minX, minZ, maxX, maxZ) = GetVillageBounds(resourceCount);
    return (minX - 4, minZ - 4, maxX + 4, maxZ + 4);
}
```

### Fence Y-Level

**Fences are placed at `BaseY` (Y=-60)**, sitting directly on the grass surface.

❌ **Wrong**: `fenceY = BaseY + 1` (Y=-59) — fence floats in the air  
✅ **Correct**: `fenceY = BaseY` (Y=-60) — fence sits on grass

### Fence Gate

A 3-block-wide gate is placed on the south side (Z-min) at the boulevard center:

```
Gate location:
  X = BaseX + StructureSize = 10 + 7 = 17 (boulevard center)
  Z = fenceMinZ (south wall)
  Width = 3 blocks (X=17 to X=19)
  Block type: minecraft:oak_fence_gate[facing=south]
```

### Fence Construction Order

From `StructureBuilder.BuildFencePerimeterAsync`:

1. **South side**: Two segments with gate gap
   ```bash
   /fill {minX} {fenceY} {minZ} {gateX-1} {fenceY} {minZ} minecraft:oak_fence
   /fill {gateX+2} {fenceY} {minZ} {maxX} {fenceY} {minZ} minecraft:oak_fence
   /fill {gateX} {fenceY} {minZ} {gateX+2} {fenceY} {minZ} minecraft:oak_fence_gate[facing=south]
   ```

2. **North side**: Continuous fence
   ```bash
   /fill {minX} {fenceY} {maxZ} {maxX} {fenceY} {maxZ} minecraft:oak_fence
   ```

3. **West side**: Skip corners (already placed)
   ```bash
   /fill {minX} {fenceY} {minZ+1} {minX} {fenceY} {maxZ-1} minecraft:oak_fence
   ```

4. **East side**: Skip corners
   ```bash
   /fill {maxX} {fenceY} {minZ+1} {maxX} {fenceY} {maxZ-1} minecraft:oak_fence
   ```

---

## Maximum Resource Limits

### Current Layout Limits

Given the 2×N grid layout with 10-block spacing:

- **Width (X-axis)**: Fixed at ~30 blocks (2 columns × 10 spacing + structure size + fence)
- **Depth (Z-axis)**: Grows with resource count (10 blocks per row)

### Safe Resource Count

For **MAX_WORLD_SIZE = 256** blocks:

```
Maximum Z extent = 128 blocks (half of 256 diameter)
Fence perimeter needs 4-block clearance

Safe calculation:
  Max rows = (128 - BaseZ - StructureSize - 4) / Spacing
  Max rows = (128 - 0 - 7 - 4) / 10 = 11.7 ≈ 11 rows
  Max resources = rows × 2 columns = 22 resources (conservative)

Practical limit (with margin):
  ~45 resources (22 rows) before approaching world border
  ~50 resources (25 rows) at absolute maximum
```

### ⚠️ Critical Scaling Constraint

From architectural review:

> **Scale is the biggest unknown.** A production Azure RG can have 200+ resources. The current 2-column village layout at 10-block spacing would stretch 1,000+ blocks — well beyond beacon render distance (256 blocks) and the configured `MAX_WORLD_SIZE` (256). A `MaxResources` cap and default resource type exclusion list are mandatory for v1.

**Recommendations**:
- Implement `MaxResources` configuration option (default: 45)
- Add resource type filtering for Azure integration (exclude low-priority types)
- Consider compact layout mode (reduce spacing or increase columns)
- Document scaling limits clearly in API documentation

---

## World Configuration

### Server Properties

From `MinecraftServerBuilderExtensions.cs`, the following environment variables configure the Minecraft world:

```csharp
.WithEnvironment("LEVEL_TYPE", "flat")              // Superflat world
.WithEnvironment("GENERATE_STRUCTURES", "false")    // No villages, temples, etc.
.WithEnvironment("SPAWN_MONSTERS", "false")         // Peaceful mode (no hostile mobs)
.WithEnvironment("SPAWN_ANIMALS", "false")          // No passive mobs
.WithEnvironment("SPAWN_NPCS", "false")             // No villagers
.WithEnvironment("SPAWN_PROTECTION", "0")           // Disable spawn protection
.WithEnvironment("VIEW_DISTANCE", "6")              // 6 chunks (96 blocks)
.WithEnvironment("SIMULATION_DISTANCE", "4")        // 4 chunks
.WithEnvironment("MAX_WORLD_SIZE", "256")           // World border diameter
```

### Render Distances

- **View distance**: 6 chunks = 96 blocks (1 chunk = 16 blocks)
- **Beacon render distance**: 256 blocks (hardcoded in Minecraft)
- **World border**: 256 blocks diameter (±128 from origin)

### Performance Optimizations

The following settings are tuned for fast startup and demo performance:

- **No structure generation**: Prevents vanilla villages, temples, strongholds
- **No mob spawning**: Peaceful mode, no distractions
- **Reduced simulation distance**: 4 chunks (64 blocks) — reduces server load
- **Superflat terrain**: Minimal world generation overhead

---

## References

### Source Files

- `src/Aspire.Hosting.Minecraft.Worker/Services/VillageLayout.cs` — coordinate calculations
- `src/Aspire.Hosting.Minecraft.Worker/Services/StructureBuilder.cs` — structure building logic
- `src/Aspire.Hosting.Minecraft.Worker/Services/RconService.cs` — RCON rate limiting
- `src/Aspire.Hosting.Minecraft/MinecraftServerBuilderExtensions.cs` — world configuration
- `src/Aspire.Hosting.Minecraft.Rcon/RconClient.cs` — RCON protocol implementation

### Decision Records

- `.ai-team/decisions.md` — Team-wide architectural decisions
- `.ai-team/decisions/inbox/rocket-switches-display-only.md` — Service switch behavior
- `.ai-team/decisions/inbox/rocket-village-rebuild-fix.md` — Structure rebuild fix

### Related Documentation

- `docs/architecture-diagram.md` — Visual diagrams of coordinate system and village layout
- `docs/api-surface.md` — Public API surface documentation
- `docs/epics/azure-resource-group-integration.md` — Azure scaling considerations
