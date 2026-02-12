# Minecraft Coordinate System & Village Architecture

This document provides visual diagrams to help contributors understand the Minecraft coordinate system, village layout, and structure placement logic used by Aspire.Hosting.Minecraft.

---

## Minecraft Superflat Y-Level Breakdown

The project uses a Minecraft superflat world where Y-coordinates are negative. All structures are built at precise Y-levels:

```
Y=-59:  Air (player walking level)
        ↑ Players walk here
        │
Y=-60:  █████████████████████  ← BaseY (grass block surface)
        │                        Structures place floors here
        │                        Fences sit at this level
Y=-61:  ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓  (dirt layer)
        │                        Paths are placed here (cobblestone replaces dirt)
        │                        Grass at Y=-60 cleared for flush appearance
Y=-62:  ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓  (dirt)
        │
Y=-63:  ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓  (bedrock)
        │
Y=-64:  █████████████████████  (bedrock — unbreakable foundation)
```

**Key Y-Coordinates:**
- **BaseY = -60**: Grass surface where structure floors are placed
- **BaseY - 1 = -61**: Where paths are built (dirt layer, with grass removed above)
- **BaseY + 1 to BaseY + 10**: Structure walls and roofs (varies by building type)

---

## Village 2×N Grid Layout

The village uses a 2-column grid that expands infinitely along the Z-axis (north-south):

```
         X-axis →
       10        20        30
Z   ┌─────────┬─────────┐
│   │         │         │
↓   │ Index 0 │ Index 1 │  Z=0 (BaseZ)
0   │ (10,-60,0)|(20,-60,0)
    │    7×7   │   7×7   │
    └─────────┴─────────┘
    ┌─────────┬─────────┐
10  │ Index 2 │ Index 3 │  Z=10
    │(10,-60,10)|(20,-60,10)
    │    7×7   │   7×7   │
    └─────────┴─────────┘
    ┌─────────┬─────────┐
20  │ Index 4 │ Index 5 │  Z=20
    │(10,-60,20)|(20,-60,20)
    │    7×7   │   7×7   │
    └─────────┴─────────┘
         ...      ...
```

**Layout Constants (from VillageLayout.cs):**
- **BaseX = 10**: X-coordinate of first column
- **BaseZ = 0**: Z-coordinate of first row
- **Spacing = 10**: Center-to-center distance between structures
- **Columns = 2**: Always 2-column layout
- **StructureSize = 7**: Each structure footprint is 7×7 blocks

**Coordinate Calculation:**
```csharp
col = index % 2           // Alternates 0, 1, 0, 1, ...
row = index / 2           // Increments every 2 resources: 0, 0, 1, 1, 2, 2, ...
x = BaseX + (col × 10)    // Result: 10, 20, 10, 20, 10, 20, ...
z = BaseZ + (row × 10)    // Result: 0, 0, 10, 10, 20, 20, ...
y = BaseY (-60)           // Always the grass surface
```

---

## Structure Footprint Visualization

Each structure occupies a 7×7 block footprint with varying heights:

```
     7 blocks wide
    ┌───────────┐
    │ ■ ■ ■ ■ ■ │  } 7 blocks deep
    │ ■       ■ │
    │ ■  5×5  ■ │  Inner space varies:
    │ ■ hollow■ │  - Watchtower: 5×5 hollow (tall tower)
    │ ■   or  ■ │  - Others: 7×7 solid
    │ ■ solid ■ │
    │ ■ ■ ■ ■ ■ │
    └───────────┘
     Origin (x,z)
```

### Structure Heights (Y-axis)

| Structure   | Resource Type | Height (blocks above BaseY) | Total Y Range     |
|-------------|---------------|----------------------------|-------------------|
| Watchtower  | Project       | 10 blocks (+ flag pole)    | Y=-60 to Y=-49    |
| Warehouse   | Container     | 5 blocks                   | Y=-60 to Y=-55    |
| Workshop    | Executable    | 7 blocks (+ chimney)       | Y=-60 to Y=-52    |
| Cottage     | Unknown/Other | 5 blocks                   | Y=-60 to Y=-55    |

### Z-Coordinate Conventions (Front Wall Placement)

- **Front Wall = South-facing (Z-min side)**
- **Hollow structures (Watchtower)**: 5×5 inner box starts at origin+(1,1), so **front wall is at z+1**
- **Solid structures (Warehouse/Workshop/Cottage)**: **Front wall is at z (origin edge)**
- **Doors**: Always placed at the actual wall Z-coordinate, not origin Z

---

## Example: 4-Resource Village Coordinates

For a village with 4 resources, here are the exact coordinates:

```
┌────────────────────────────────────┐
│   10         20         30         │
│ ┌─────┐    ┌─────┐                │
│ │  0  │    │  1  │    Z=0         │
│ └─────┘    └─────┘                │
│   10,0       20,0                  │
│                                    │
│ ┌─────┐    ┌─────┐                │
│ │  2  │    │  3  │    Z=10        │
│ └─────┘    └─────┘                │
│   10,10      20,10                 │
└────────────────────────────────────┘
```

**Exact Coordinates:**
| Index | Column | Row | Origin (x, y, z) | Center (x, y, z) |
|-------|--------|-----|------------------|------------------|
| 0     | 0      | 0   | (10, -60, 0)     | (13, -60, 3)     |
| 1     | 1      | 0   | (20, -60, 0)     | (23, -60, 3)     |
| 2     | 0      | 1   | (10, -60, 10)    | (13, -60, 13)    |
| 3     | 1      | 1   | (20, -60, 10)    | (23, -60, 13)    |

---

## Fence Perimeter Calculation

The village is surrounded by a fence with a 4-block clearance from structure edges:

```
         Fence extends 4 blocks beyond village bounds
         ←────────────────────────────────────────→
        ┌─────────────────────────────────────────┐
        │ Fence (oak_fence at Y=-60)              │
        │  ┌───────────────────────────────────┐  │
        │  │  4-block gap (walking space)      │  │
        │  │  ┌─────────────────────────────┐  │  │
        │  │  │ Village Bounds               │  │  │
        │  │  │  ┌─────┐    ┌─────┐          │  │  │
        │  │  │  │  0  │    │  1  │          │  │  │
        │  │  │  └─────┘    └─────┘          │  │  │
        │  │  │                               │  │  │
        │  │  │  ┌─────┐    ┌─────┐          │  │  │
        │  │  │  │  2  │    │  3  │          │  │  │
        │  │  │  └─────┘    └─────┘          │  │  │
        │  │  └─────────────────────────────┘  │  │
        │  └───────────────────────────────────┘  │
        └─────────────────────────────────────────┘
        South gate (3-wide) at X=17 (boulevard center)
```

**Fence Calculation (from VillageLayout.GetFencePerimeter):**
```
Village bounds:
  minX = BaseX = 10
  minZ = BaseZ = 0
  maxX = BaseX + ((cols - 1) × 10) + 6 = 10 + 10 + 6 = 26
  maxZ = BaseZ + ((rows - 1) × 10) + 6 = 0 + 10 + 6 = 16

Fence perimeter (4-block clearance):
  fenceMinX = minX - 4 = 6
  fenceMinZ = minZ - 4 = -4
  fenceMaxX = maxX + 4 = 30
  fenceMaxZ = maxZ + 4 = 20

Fence gate:
  X = BaseX + StructureSize = 10 + 7 = 17 (boulevard center)
  Z = fenceMinZ = -4 (south wall)
  Width = 3 blocks
```

**4-Resource Village Fence:**
- Fence perimeter: (6, -60, -4) to (30, -60, 20)
- Gate opening: X=17 to X=19, Z=-4 (south entrance, 3 blocks wide)

---

## Path Placement Rules

Paths cover the entire village area inside the fence, creating a cobblestone "plaza":

```
Vertical cross-section at path location:

Y=-59:  Air ░░░░░░░░░░░░░░░░░░  (Players walk at this level)
              ↑
Y=-60:  Air ░░░░░░░░░░░░░░░░░░  (Grass blocks cleared via /fill ... air replace grass_block)
              ↓
Y=-61:  Cobblestone ████████████  (Paths placed in dirt layer)

Y=-62:  Dirt ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓  (Undisturbed)
```

**Path Building Process (from StructureBuilder.BuildPathsAsync):**
1. **Clear grass surface**: `/fill ... Y=-60 ... minecraft:air replace grass_block`
2. **Place cobblestone**: `/fill ... Y=-61 ... minecraft:cobblestone`
3. **Result**: Paths are flush with surrounding grass, recessed one block below surface

**Path Bounds:**
- X: fenceMinX + 1 to fenceMaxX - 1 (inside fence)
- Y: BaseY - 1 (-61)
- Z: fenceMinZ + 1 to fenceMaxZ - 1 (inside fence)

---

## World Size and Scale Limits

The project is configured for a compact demo environment:

```
World Configuration (from MinecraftServerBuilderExtensions.cs):
- MAX_WORLD_SIZE = 256 blocks (world border radius)
- VIEW_DISTANCE = 6 chunks (96 blocks)
- SIMULATION_DISTANCE = 4 chunks
```

**Maximum Village Size:**
- **Current layout**: 2 columns × 10-block spacing = 20 blocks wide (X-axis)
- **Maximum resources before exceeding world border**: ~50 resources
  - 50 resources = 25 rows × 10-block spacing = 250 blocks north (Z-axis)
  - Total with fence: 250 + 4 (clearance) + fence = ~258 blocks (exceeds 256)
- **Recommendation**: Limit to ~45 resources for safe margin
- **Azure integration note**: Production Azure RGs can have 200+ resources — need `MaxResources` cap

**Render Distance:**
- Beacon beams visible at: 256 blocks (beacon render distance)
- Chunk render distance: 96 blocks (6 chunks × 16 blocks/chunk)
- **Design decision**: 2-column layout keeps village within render distance for better visibility

---

## References

For implementation details, see:
- `src/Aspire.Hosting.Minecraft.Worker/Services/VillageLayout.cs` — coordinate calculations
- `src/Aspire.Hosting.Minecraft.Worker/Services/StructureBuilder.cs` — structure building logic
- `src/Aspire.Hosting.Minecraft/MinecraftServerBuilderExtensions.cs` — world configuration
