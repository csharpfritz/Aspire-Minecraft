# Minecraft Building Reference — Sprint 4

> **Author:** Rocket (Integration Dev)
> **Date:** 2026-02-11
> **Purpose:** Implementation bible for Sprint 4 building enhancements. Every RCON command here is copy-paste ready with `(x, y, z)` as the structure origin from `VillageLayout.GetStructureOrigin(index)`.

---

## Table of Contents

1. [A. Database Cylinder Building](#a-database-cylinder-building)
2. [B. Azure Flag/Banner](#b-azure-flagbanner)
3. [C. Enhanced Building Palettes](#c-enhanced-building-palettes)
4. [D. Dashboard Wall](#d-dashboard-wall)

---

## A. Database Cylinder Building

### Design Rationale

Database resources should look like the cylinder icons used in architecture diagrams. A 3-block radius circle produces a 7-block diameter — a perfect fit for the existing 7×7 structure footprint with 10-block spacing.

### Circle Geometry — Radius 3

The circle is centered at `(x+3, y, z+3)` (the center of the 7×7 grid cell). All offsets below are relative to the structure origin `(x, z)`.

#### Block Map (7×7 grid, 0-indexed from origin)

```
       0   1   2   3   4   5   6     ← X offset
  0    .   .   W   W   W   .   .
  1    .   W   F   F   F   W   .
  2    W   F   F   F   F   F   W
  3    W   F   F   C   F   F   W     ← center at (3,3)
  4    W   F   F   F   F   F   W
  5    .   W   F   F   F   W   .
  6    .   .   W   W   W   .   .

  W = Wall (perimeter)   F = Floor (interior)   C = Center   . = Outside
```

#### Perimeter (Wall) Blocks — 16 positions

These XZ offsets from origin define the cylinder wall at each Y level:

```
(2,0) (3,0) (4,0)              — north face
(1,1) (5,1)                    — upper diagonals
(0,2) (6,2)                    — east/west widest
(0,3) (6,3)                    — east/west middle
(0,4) (6,4)                    — east/west widest
(1,5) (5,5)                    — lower diagonals
(2,6) (3,6) (4,6)              — south face
```

#### Interior (Floor) Blocks — 21 positions

```
(2,1) (3,1) (4,1)
(1,2) (2,2) (3,2) (4,2) (5,2)
(1,3) (2,3) (3,3) (4,3) (5,3)
(1,4) (2,4) (3,4) (4,4) (5,4)
(2,5) (3,5) (4,5)
```

### Block Palette — "Database" Feel

| Element | Block | Why |
|---------|-------|-----|
| Floor | `minecraft:polished_deepslate` | Dark, industrial, server-room feel |
| Walls (lower 3 rows) | `minecraft:smooth_stone` | Clean data-center aesthetic |
| Walls (top band) | `minecraft:polished_deepslate` | Contrast band like a DB cylinder cap |
| Dome roof | `minecraft:smooth_stone_slab` | Rounded cap like cylinder icon |
| Dome peak | `minecraft:polished_deepslate_slab` | Dark cap top |
| Interior accent | `minecraft:copper_block` | Copper = data wiring/connections |
| Door frame | `minecraft:iron_block` | Matches industrial/data theme |
| Health indicator | `minecraft:glowstone` / `minecraft:redstone_lamp` | Standard health lamp system |

### Building Height

- **Floor:** `y+0` (1 layer)
- **Walls:** `y+1` to `y+4` (4 layers, matching current building heights)
- **Dome:** `y+5` (outer ring) + `y+6` (inner cap)
- **Total:** 7 blocks tall (floor to dome peak)

### Door Placement

The door faces south (Z-min side), centered at `(x+3, z+0)`. On a round building, we carve a 2-wide × 2-tall opening at the flattest part of the south face.

Door position: `(x+2, y+1, z+0)` to `(x+4, y+2, z+0)` — 3-wide opening through the wall thickness.

### Health Indicator Placement

The health lamp sits above the door on the south face at `(x+3, y+3, z+0)` — embedded in the curved wall, visible from the front.

### RCON Command Sequence

```csharp
/// Build a database cylinder at origin (x, y, z)
private async Task BuildCylinderAsync(int x, int y, int z, CancellationToken ct)
{
    // === FLOOR (y+0): polished deepslate disc ===
    // Row z+0: 3 blocks
    await rcon.SendCommandAsync(
        $"fill {x+2} {y} {z} {x+4} {y} {z} minecraft:polished_deepslate", ct);
    // Row z+1: 5 blocks
    await rcon.SendCommandAsync(
        $"fill {x+1} {y} {z+1} {x+5} {y} {z+1} minecraft:polished_deepslate", ct);
    // Row z+2: 7 blocks (full width)
    await rcon.SendCommandAsync(
        $"fill {x} {y} {z+2} {x+6} {y} {z+2} minecraft:polished_deepslate", ct);
    // Row z+3: 7 blocks (full width)
    await rcon.SendCommandAsync(
        $"fill {x} {y} {z+3} {x+6} {y} {z+3} minecraft:polished_deepslate", ct);
    // Row z+4: 7 blocks (full width)
    await rcon.SendCommandAsync(
        $"fill {x} {y} {z+4} {x+6} {y} {z+4} minecraft:polished_deepslate", ct);
    // Row z+5: 5 blocks
    await rcon.SendCommandAsync(
        $"fill {x+1} {y} {z+5} {x+5} {y} {z+5} minecraft:polished_deepslate", ct);
    // Row z+6: 3 blocks
    await rcon.SendCommandAsync(
        $"fill {x+2} {y} {z+6} {x+4} {y} {z+6} minecraft:polished_deepslate", ct);

    // === WALLS (y+1 to y+4): smooth stone perimeter, 4 layers ===
    for (int layer = 1; layer <= 4; layer++)
    {
        string wallBlock = layer <= 3
            ? "minecraft:smooth_stone"
            : "minecraft:polished_deepslate";

        // North face (z+0): 3 blocks
        await rcon.SendCommandAsync(
            $"fill {x+2} {y+layer} {z} {x+4} {y+layer} {z} {wallBlock}", ct);
        // South face (z+6): 3 blocks
        await rcon.SendCommandAsync(
            $"fill {x+2} {y+layer} {z+6} {x+4} {y+layer} {z+6} {wallBlock}", ct);
        // Upper diagonals
        await rcon.SendCommandAsync(
            $"setblock {x+1} {y+layer} {z+1} {wallBlock}", ct);
        await rcon.SendCommandAsync(
            $"setblock {x+5} {y+layer} {z+1} {wallBlock}", ct);
        // Lower diagonals
        await rcon.SendCommandAsync(
            $"setblock {x+1} {y+layer} {z+5} {wallBlock}", ct);
        await rcon.SendCommandAsync(
            $"setblock {x+5} {y+layer} {z+5} {wallBlock}", ct);
        // East/west faces (z+2 to z+4): 1 block each side
        await rcon.SendCommandAsync(
            $"fill {x} {y+layer} {z+2} {x} {y+layer} {z+4} {wallBlock}", ct);
        await rcon.SendCommandAsync(
            $"fill {x+6} {y+layer} {z+2} {x+6} {y+layer} {z+4} {wallBlock}", ct);
    }

    // === INTERIOR AIR (y+1 to y+4): clear the inside ===
    // The wall commands only place the perimeter, but let's ensure interior is clear
    for (int layer = 1; layer <= 4; layer++)
    {
        // Interior rows (same shape as floor interior)
        await rcon.SendCommandAsync(
            $"fill {x+2} {y+layer} {z+1} {x+4} {y+layer} {z+1} minecraft:air", ct);
        await rcon.SendCommandAsync(
            $"fill {x+1} {y+layer} {z+2} {x+5} {y+layer} {z+2} minecraft:air", ct);
        await rcon.SendCommandAsync(
            $"fill {x+1} {y+layer} {z+3} {x+5} {y+layer} {z+3} minecraft:air", ct);
        await rcon.SendCommandAsync(
            $"fill {x+1} {y+layer} {z+4} {x+5} {y+layer} {z+4} minecraft:air", ct);
        await rcon.SendCommandAsync(
            $"fill {x+2} {y+layer} {z+5} {x+4} {y+layer} {z+5} minecraft:air", ct);
    }

    // === DOME ROOF ===
    // Level 1 (y+5): outer ring as smooth_stone_slab (same disc as floor, but slabs)
    await rcon.SendCommandAsync(
        $"fill {x+2} {y+5} {z} {x+4} {y+5} {z} minecraft:smooth_stone_slab", ct);
    await rcon.SendCommandAsync(
        $"fill {x+1} {y+5} {z+1} {x+5} {y+5} {z+1} minecraft:smooth_stone_slab", ct);
    await rcon.SendCommandAsync(
        $"fill {x} {y+5} {z+2} {x+6} {y+5} {z+2} minecraft:smooth_stone_slab", ct);
    await rcon.SendCommandAsync(
        $"fill {x} {y+5} {z+3} {x+6} {y+5} {z+3} minecraft:smooth_stone_slab", ct);
    await rcon.SendCommandAsync(
        $"fill {x} {y+5} {z+4} {x+6} {y+5} {z+4} minecraft:smooth_stone_slab", ct);
    await rcon.SendCommandAsync(
        $"fill {x+1} {y+5} {z+5} {x+5} {y+5} {z+5} minecraft:smooth_stone_slab", ct);
    await rcon.SendCommandAsync(
        $"fill {x+2} {y+5} {z+6} {x+4} {y+5} {z+6} minecraft:smooth_stone_slab", ct);

    // Level 2 (y+6): inner cap — smaller circle (radius ~2)
    await rcon.SendCommandAsync(
        $"fill {x+2} {y+6} {z+1} {x+4} {y+6} {z+1} minecraft:polished_deepslate_slab", ct);
    await rcon.SendCommandAsync(
        $"fill {x+1} {y+6} {z+2} {x+5} {y+6} {z+2} minecraft:polished_deepslate_slab", ct);
    await rcon.SendCommandAsync(
        $"fill {x+1} {y+6} {z+3} {x+5} {y+6} {z+3} minecraft:polished_deepslate_slab", ct);
    await rcon.SendCommandAsync(
        $"fill {x+1} {y+6} {z+4} {x+5} {y+6} {z+4} minecraft:polished_deepslate_slab", ct);
    await rcon.SendCommandAsync(
        $"fill {x+2} {y+6} {z+5} {x+4} {y+6} {z+5} minecraft:polished_deepslate_slab", ct);

    // === DOOR (south face, z+0): 3-wide × 2-tall opening ===
    await rcon.SendCommandAsync(
        $"fill {x+2} {y+1} {z} {x+4} {y+2} {z} minecraft:air", ct);

    // === INTERIOR ACCENTS ===
    // Copper accent blocks on floor (data connection aesthetic)
    await rcon.SendCommandAsync(
        $"setblock {x+3} {y} {z+3} minecraft:copper_block", ct);
    await rcon.SendCommandAsync(
        $"setblock {x+2} {y} {z+3} minecraft:copper_block", ct);
    await rcon.SendCommandAsync(
        $"setblock {x+4} {y} {z+3} minecraft:copper_block", ct);

    // Iron block door frame accents
    await rcon.SendCommandAsync(
        $"setblock {x+1} {y+1} {z+1} minecraft:iron_block", ct);
    await rcon.SendCommandAsync(
        $"setblock {x+5} {y+1} {z+1} minecraft:iron_block", ct);
}
```

### Optimized Version (fewer RCON calls)

The above is maximally clear. For production, combine the wall-building into a helper that loops Y levels and uses a coordinate list. The command count is ~60 per cylinder (vs ~20 for a rectangular building). Consider batching in `CommandPriority.Low` if performance matters.

### Integration with StructureBuilder

Add to `GetStructureType`:

```csharp
"database" or "postgres" or "mysql" or "sqlserver" or "redis" or "mongodb"
    => "Cylinder",
```

Add `case "Cylinder": await BuildCylinderAsync(x, y, z, ct); break;` to the switch in `BuildResourceStructureAsync`.

Health indicator for Cylinder: `lampZ = z` (front face), `lampY = y + 3` (above 2-tall door).

---

## B. Azure Flag/Banner

### Banner Pattern Design — Azure Blue

The Azure brand color is a bright blue. In Minecraft, `light_blue_banner` is the closest base. We add a white stripe pattern to evoke the Azure logo's "window pane" feel.

#### Recommended Banner Recipe (NBT)

```
Base: light_blue_banner
Pattern 1: white stripe (str) — vertical center stripe
Pattern 2: white base (bs) — bottom stripe for grounding
```

**NBT for banner block:**
```
{Patterns:[{Color:0,Pattern:"str"},{Color:0,Pattern:"bs"}]}
```

> `Color:0` = white in Minecraft banner color index. The base banner color (light blue) is determined by the block ID itself.

#### Alternative: Simpler Azure Flag

For a cleaner look, just a solid `light_blue_banner` (no patterns) reads well at Minecraft scale. The light blue is close enough to Azure blue.

**NBT for simple banner:**
```
(no Patterns tag needed — just the base light_blue_banner)
```

### RCON Commands for Banner Placement

#### Option 1: Standing Banner on Rooftop Flagpole (Recommended)

A fence post flagpole with the banner next to it, on top of the roof. This matches the current Watchtower pattern.

```
# Flagpole (2-block oak fence on roof center)
fill {x+3} {y+roof} {z+3} {x+3} {y+roof+1} {z+3} minecraft:oak_fence

# Banner beside flagpole (rotation=8 faces south toward viewer)
setblock {x+3} {y+roof+1} {z+2} minecraft:light_blue_banner[rotation=8]{Patterns:[{Color:0,Pattern:"str"},{Color:0,Pattern:"bs"}]}
```

Where `roof` depends on structure type:
- Watchtower: `roof = 9` (current flag is already there — swap to Azure banner)
- Warehouse: `roof = 6` (above flat iron roof at y+5)
- Workshop: `roof = 7` (above peaked roof at y+6)
- Cottage: `roof = 6` (above slab roof at y+5)
- Cylinder: `roof = 7` (above dome at y+6)

#### Option 2: Wall-Mounted Banner (Alternative)

```
# Wall banner on south face (facing=south)
setblock {x+3} {y+3} {z-1} minecraft:light_blue_wall_banner[facing=south]{Patterns:[{Color:0,Pattern:"str"},{Color:0,Pattern:"bs"}]}
```

Wall banners look good but are less visible from a distance. Rooftop is preferred.

### Detecting Azure Resources

Azure resources can be identified by:
- Resource type containing "azure" (e.g., `azure.storage`, `azure.sql`)
- Connection string patterns (`*.database.windows.net`, `*.blob.core.windows.net`)
- Environment variable `ASPIRE_RESOURCE_{NAME}_AZURE=true` (if set by the hosting integration)

In code:
```csharp
private static bool IsAzureResource(ResourceInfo info)
{
    return info.Type.Contains("azure", StringComparison.OrdinalIgnoreCase)
        || info.Name.Contains("azure", StringComparison.OrdinalIgnoreCase);
}
```

### Azure Banner Placement Logic

```csharp
private async Task PlaceAzureBannerAsync(int x, int y, int z, string structureType, CancellationToken ct)
{
    int roofY = structureType switch
    {
        "Watchtower" => y + 9,
        "Warehouse" => y + 6,
        "Workshop" => y + 7,
        "Cottage" => y + 6,
        "Cylinder" => y + 7,
        _ => y + 6,
    };

    // Flagpole
    await rcon.SendCommandAsync(
        $"fill {x+3} {roofY} {z+3} {x+3} {roofY+1} {z+3} minecraft:oak_fence", ct);

    // Azure banner
    await rcon.SendCommandAsync(
        $"setblock {x+3} {roofY+1} {z+2} minecraft:light_blue_banner[rotation=8]" +
        "{Patterns:[{Color:0,Pattern:\"str\"},{Color:0,Pattern:\"bs\"}]}", ct);
}
```

---

## C. Enhanced Building Palettes

### Watchtower (Projects) — Grand Stone Keep

**Current:** Stone bricks walls, blue wool trim, stone brick slab roof.
**Enhanced:** A proper fortified tower with mixed materials and battlements.

| Element | Current | Enhanced |
|---------|---------|----------|
| Floor | `stone_bricks` | `chiseled_stone_bricks` |
| Walls (lower) | `stone_bricks` | `stone_bricks` (keep) |
| Walls (upper band) | `blue_wool` | `polished_andesite` |
| Corner pillars | `stone_brick_stairs` | `deepslate_brick_wall` (vertical pillar look) |
| Windows | `glass_pane` | `light_blue_stained_glass_pane` (brand color) |
| Roof | `stone_brick_slab` | `deepslate_tile_slab` (darker, richer) |
| Battlements | _(none)_ | `stone_brick_wall` crenellation at top |
| Interior | _(empty)_ | `bookshelf` + `lantern` (library/office feel) |
| Lighting | _(none)_ | `lantern` hanging from ceiling at y+7 |

**RCON additions for enhanced Watchtower:**

```
# Chiseled floor
fill {x} {y} {z} {x+6} {y} {z+6} minecraft:chiseled_stone_bricks

# Deepslate pillar columns (replace stairs)
fill {x+1} {y+1} {z+1} {x+1} {y+9} {z+1} minecraft:deepslate_brick_wall
fill {x+5} {y+1} {z+1} {x+5} {y+9} {z+1} minecraft:deepslate_brick_wall
fill {x+1} {y+1} {z+5} {x+1} {y+9} {z+5} minecraft:deepslate_brick_wall
fill {x+5} {y+1} {z+5} {x+5} {y+9} {z+5} minecraft:deepslate_brick_wall

# Polished andesite accent band at y+8 (instead of blue_wool)
fill {x+1} {y+8} {z+1} {x+5} {y+8} {z+5} minecraft:polished_andesite

# Tinted windows
setblock {x+3} {y+4} {z+1} minecraft:light_blue_stained_glass_pane
setblock {x+3} {y+4} {z+5} minecraft:light_blue_stained_glass_pane
setblock {x+1} {y+4} {z+3} minecraft:light_blue_stained_glass_pane
setblock {x+5} {y+4} {z+3} minecraft:light_blue_stained_glass_pane

# Second row of windows at y+6
setblock {x+3} {y+6} {z+1} minecraft:light_blue_stained_glass_pane
setblock {x+3} {y+6} {z+5} minecraft:light_blue_stained_glass_pane
setblock {x+1} {y+6} {z+3} minecraft:light_blue_stained_glass_pane
setblock {x+5} {y+6} {z+3} minecraft:light_blue_stained_glass_pane

# Darker roof
fill {x+2} {y+9} {z+2} {x+4} {y+9} {z+4} minecraft:deepslate_tile_slab

# Battlements (stone brick walls on the roof edge)
setblock {x+2} {y+10} {z+2} minecraft:stone_brick_wall
setblock {x+4} {y+10} {z+2} minecraft:stone_brick_wall
setblock {x+2} {y+10} {z+4} minecraft:stone_brick_wall
setblock {x+4} {y+10} {z+4} minecraft:stone_brick_wall

# Interior: bookshelves and lantern
setblock {x+2} {y+1} {z+4} minecraft:bookshelf
setblock {x+4} {y+1} {z+4} minecraft:bookshelf
setblock {x+2} {y+2} {z+4} minecraft:bookshelf
setblock {x+4} {y+2} {z+4} minecraft:bookshelf
setblock {x+3} {y+7} {z+3} minecraft:lantern[hanging=true]
```

---

### Warehouse (Containers) — Industrial Shipping Yard

**Current:** Iron blocks all over, purple stained glass, barrels.
**Enhanced:** Mixed metals with an industrial feel — corrugated iron, cauldrons, chains.

| Element | Current | Enhanced |
|---------|---------|----------|
| Floor | `iron_block` | `smooth_stone` (concrete slab feel) |
| Walls (main) | `iron_block` | `iron_block` lower + `light_gray_concrete` upper |
| Walls (accent stripe) | _(none)_ | `orange_concrete` stripe at y+2 (shipping container look) |
| Windows | `purple_stained_glass` | `gray_stained_glass_pane` (industrial) |
| Roof | `iron_block` | `iron_trapdoor` (corrugated look) |
| Interior | `barrel` x4 | `barrel` x4 + `chain` + `cauldron` |
| Door | 3-wide air | `iron_door` frame with `iron_block` threshold |
| Lighting | _(none)_ | `soul_lantern` (cool industrial light) |

**RCON additions for enhanced Warehouse:**

```
# Smooth stone floor
fill {x} {y} {z} {x+6} {y} {z+6} minecraft:smooth_stone

# Lower walls: iron block (y+1 to y+2)
fill {x} {y+1} {z} {x+6} {y+2} {z+6} minecraft:iron_block hollow

# Orange accent stripe at y+2 (all 4 sides)
fill {x} {y+2} {z} {x+6} {y+2} {z} minecraft:orange_concrete
fill {x} {y+2} {z+6} {x+6} {y+2} {z+6} minecraft:orange_concrete
fill {x} {y+2} {z+1} {x} {y+2} {z+5} minecraft:orange_concrete
fill {x+6} {y+2} {z+1} {x+6} {y+2} {z+5} minecraft:orange_concrete

# Upper walls: light gray concrete (y+3 to y+4)
fill {x} {y+3} {z} {x+6} {y+4} {z+6} minecraft:light_gray_concrete hollow

# Industrial windows (gray glass)
fill {x} {y+3} {z+2} {x} {y+3} {z+4} minecraft:gray_stained_glass_pane
fill {x+6} {y+3} {z+2} {x+6} {y+3} {z+4} minecraft:gray_stained_glass_pane
fill {x+2} {y+3} {z+6} {x+4} {y+3} {z+6} minecraft:gray_stained_glass_pane

# Iron trapdoor roof (corrugated look)
fill {x} {y+5} {z} {x+6} {y+5} {z+6} minecraft:iron_trapdoor[half=top,open=false]

# Cargo door (3-wide x 3-tall, cleared LAST)
fill {x+2} {y+1} {z} {x+4} {y+3} {z} minecraft:air

# Interior: barrels, chains, soul lanterns
setblock {x+1} {y+1} {z+5} minecraft:barrel[facing=up]
setblock {x+5} {y+1} {z+5} minecraft:barrel[facing=up]
setblock {x+1} {y+1} {z+3} minecraft:barrel[facing=up]
setblock {x+5} {y+1} {z+3} minecraft:barrel[facing=up]
setblock {x+3} {y+1} {z+5} minecraft:cauldron
setblock {x+3} {y+4} {z+3} minecraft:soul_lantern[hanging=true]
fill {x+1} {y+3} {z+1} {x+1} {y+4} {z+1} minecraft:chain
fill {x+5} {y+3} {z+1} {x+5} {y+4} {z+1} minecraft:chain
```

---

### Workshop (Executables) — Active Forge

**Current:** Oak planks, cyan glass, chimney with campfire, crafting table + anvil.
**Enhanced:** Mixed wood species, redstone accents (active machinery), furnace heat.

| Element | Current | Enhanced |
|---------|---------|----------|
| Floor | `oak_planks` | `spruce_planks` (darker, richer) |
| Walls (lower) | `oak_planks` | `stripped_spruce_log` (timber frame) |
| Walls (upper) | `oak_planks` | `spruce_planks` (infill) |
| Windows | `cyan_stained_glass` | `cyan_stained_glass_pane` + `spruce_trapdoor` shutters |
| Roof | `oak_stairs` + `oak_slab` | `dark_oak_stairs` + `dark_oak_slab` (contrast) |
| Chimney | `cobblestone` + `campfire` | `bricks` + `campfire` + `smoker` |
| Interior | `crafting_table` + `anvil` | + `smithing_table` + `blast_furnace` + `redstone_torch` |
| Lighting | _(none)_ | `redstone_torch` on walls (active/working feel) |

**RCON additions for enhanced Workshop:**

```
# Spruce plank floor
fill {x} {y} {z} {x+6} {y} {z+6} minecraft:spruce_planks

# Timber frame walls: stripped spruce logs at base
fill {x} {y+1} {z} {x+6} {y+1} {z+6} minecraft:stripped_spruce_log hollow

# Upper walls: spruce planks (y+2 to y+4)
fill {x} {y+2} {z} {x+6} {y+4} {z+6} minecraft:spruce_planks hollow

# Dark oak peaked roof
fill {x} {y+5} {z} {x+6} {y+5} {z+1} minecraft:dark_oak_stairs[facing=south,half=bottom]
fill {x} {y+5} {z+5} {x+6} {y+5} {z+6} minecraft:dark_oak_stairs[facing=north,half=bottom]
fill {x} {y+5} {z+2} {x+6} {y+5} {z+4} minecraft:dark_oak_planks
fill {x} {y+6} {z+3} {x+6} {y+6} {z+3} minecraft:dark_oak_slab

# Window shutters (trapdoors flanking glass)
setblock {x+1} {y+3} {z} minecraft:cyan_stained_glass_pane
setblock {x+5} {y+3} {z} minecraft:cyan_stained_glass_pane
setblock {x} {y+3} {z+3} minecraft:cyan_stained_glass_pane
setblock {x+6} {y+3} {z+3} minecraft:cyan_stained_glass_pane

# Brick chimney
fill {x+6} {y+5} {z+6} {x+6} {y+7} {z+6} minecraft:bricks
setblock {x+6} {y+7} {z+6} minecraft:campfire

# Interior workshop equipment
setblock {x+2} {y+1} {z+5} minecraft:crafting_table
setblock {x+4} {y+1} {z+5} minecraft:anvil
setblock {x+2} {y+1} {z+3} minecraft:smithing_table
setblock {x+4} {y+1} {z+3} minecraft:blast_furnace[facing=north]

# Redstone torches (active machinery glow)
setblock {x+1} {y+3} {z+6} minecraft:redstone_wall_torch[facing=north]
setblock {x+5} {y+3} {z+6} minecraft:redstone_wall_torch[facing=north]

# Door (2-wide, cleared LAST)
fill {x+2} {y+1} {z} {x+3} {y+2} {z} minecraft:air
```

---

### Cottage (Unknown/Other) — Charming Hideaway

**Current:** Cobblestone walls, light blue wool trim, cobblestone slab roof.
**Enhanced:** Keep it simple but add warmth — flower pots, mixed cobble/mossy, wooden accents.

| Element | Current | Enhanced |
|---------|---------|----------|
| Floor | `cobblestone` | `cobblestone` + center `moss_block` (garden feel) |
| Walls (lower) | `cobblestone` | `cobblestone` with `mossy_cobblestone` accents |
| Walls (trim) | `light_blue_wool` | `stripped_oak_log` (timber frame top) |
| Windows | `glass_pane` | `glass_pane` + `flower_pot` on `oak_slab` windowsill |
| Roof | `cobblestone_slab` | `oak_stairs` peaked roof (homey) |
| Door | air gap | air gap + `oak_trapdoor` awning above |
| Interior | _(empty)_ | `flower_pot` + `torch` |
| Lighting | _(none)_ | `torch` on walls |

**RCON additions for enhanced Cottage:**

```
# Cobblestone floor with mossy center
fill {x} {y} {z} {x+6} {y} {z+6} minecraft:cobblestone
fill {x+2} {y} {z+2} {x+4} {y} {z+4} minecraft:moss_block

# Mixed cobblestone walls (y+1 to y+3)
fill {x} {y+1} {z} {x+6} {y+3} {z+6} minecraft:cobblestone hollow

# Mossy cobblestone accents (corners, random patches)
setblock {x} {y+1} {z} minecraft:mossy_cobblestone
setblock {x+6} {y+1} {z} minecraft:mossy_cobblestone
setblock {x} {y+1} {z+6} minecraft:mossy_cobblestone
setblock {x+6} {y+1} {z+6} minecraft:mossy_cobblestone
setblock {x} {y+2} {z+3} minecraft:mossy_cobblestone
setblock {x+6} {y+2} {z+3} minecraft:mossy_cobblestone

# Timber frame top band (stripped oak log at y+4)
fill {x} {y+4} {z} {x+6} {y+4} {z} minecraft:stripped_oak_log[axis=x]
fill {x} {y+4} {z+6} {x+6} {y+4} {z+6} minecraft:stripped_oak_log[axis=x]
fill {x} {y+4} {z+1} {x} {y+4} {z+5} minecraft:stripped_oak_log[axis=z]
fill {x+6} {y+4} {z+1} {x+6} {y+4} {z+5} minecraft:stripped_oak_log[axis=z]

# Peaked oak stair roof
fill {x} {y+5} {z} {x+6} {y+5} {z+1} minecraft:oak_stairs[facing=south,half=bottom]
fill {x} {y+5} {z+5} {x+6} {y+5} {z+6} minecraft:oak_stairs[facing=north,half=bottom]
fill {x} {y+5} {z+2} {x+6} {y+5} {z+4} minecraft:oak_planks
fill {x} {y+6} {z+3} {x+6} {y+6} {z+3} minecraft:oak_slab

# Windows with flower pots
setblock {x+1} {y+2} {z} minecraft:glass_pane
setblock {x+5} {y+2} {z} minecraft:glass_pane
setblock {x} {y+2} {z+3} minecraft:glass_pane
setblock {x+6} {y+2} {z+3} minecraft:glass_pane

# Awning above door
fill {x+2} {y+3} {z-1} {x+3} {y+3} {z-1} minecraft:oak_trapdoor[facing=south,half=top,open=true]

# Torches
setblock {x+1} {y+3} {z+6} minecraft:wall_torch[facing=north]
setblock {x+5} {y+3} {z+6} minecraft:wall_torch[facing=north]

# Interior flower pot + torch
setblock {x+3} {y+1} {z+4} minecraft:flower_pot
setblock {x+3} {y+3} {z+6} minecraft:wall_torch[facing=north]

# Door (2-wide, cleared LAST)
fill {x+2} {y+1} {z} {x+3} {y+2} {z} minecraft:air
```

---

## D. Dashboard Wall

### Concept

A physical "monitor" display near the village made from a grid of redstone lamps. Each column represents a resource, and the lamp state (lit/unlit) shows health. New data shifts existing columns left, creating a scrolling history effect.

### Physical Design

```
Dimensions:  20 blocks wide × 8 blocks tall
Frame:       Polished blackstone (dark monitor bezel)
Screen:      Redstone lamp grid (16 × 6 usable area inside frame)
Location:    Behind the village, facing south toward it
```

### Placement Coordinates

Relative to the village:
- Village spans from `X=10` to `X=26` (2 columns × 10 spacing + 7 footprint)
- Dashboard sits behind (north of) the village at a comfortable viewing distance

```
Dashboard origin: (X=10, Y=SurfaceY+2, Z=-12)
Dashboard end:    (X=29, Y=SurfaceY+9, Z=-12)

That's: 20 wide (X=10 to X=29), 8 tall (Y=SurfaceY+2 to SurfaceY+9), 1 deep (Z=-12)
```

Facing south (toward the village at Z=0+), players approaching from the village will see it.

### Frame Construction

```
# Frame: polished blackstone border (20×8 rectangle)
# Bottom edge
fill 10 {SY+1} -12 29 {SY+1} -12 minecraft:polished_blackstone

# Top edge
fill 10 {SY+10} -12 29 {SY+10} -12 minecraft:polished_blackstone

# Left edge
fill 10 {SY+1} -12 10 {SY+10} -12 minecraft:polished_blackstone

# Right edge
fill 29 {SY+1} -12 29 {SY+10} -12 minecraft:polished_blackstone

# Back panel (behind the lamps — black concrete for contrast)
fill 11 {SY+2} -13 28 {SY+9} -13 minecraft:black_concrete

# Screen area: initial state — all redstone lamps (unlit)
fill 11 {SY+2} -12 28 {SY+9} -12 minecraft:redstone_lamp
```

Where `{SY}` = `VillageLayout.SurfaceY`.

### Redstone Lamp Grid Layout

The usable screen area is 18 × 8 blocks:
- **X:** 11 to 28 (18 columns, one per resource + history)
- **Y:** `SY+2` to `SY+9` (8 rows)

Each column represents a time slot or resource. Resources are assigned left-to-right; history scrolls left.

#### Lighting a Lamp

Redstone lamps need a redstone signal. The simplest approach: **swap the block** rather than wiring redstone:

```
# Lit (healthy): replace with glowstone (always lit, similar appearance)
setblock {col_x} {row_y} -12 minecraft:glowstone

# Unlit (unhealthy): replace with redstone_lamp (stays dark without signal)
setblock {col_x} {row_y} -12 minecraft:redstone_lamp

# Unknown/starting: replace with gray_concrete (neutral)
setblock {col_x} {row_y} -12 minecraft:gray_concrete
```

This avoids all redstone wiring complexity. The visual result is the same — bright vs dark grid cells.

### The `/clone` Scroll Technique

To create a scrolling timeline effect (newest data on the right, history scrolls left):

```
# Step 1: Clone the existing grid 1 column to the left
clone 12 {SY+2} -12 28 {SY+9} -12 11 {SY+2} -12

# Step 2: Write new data in the rightmost column (X=28)
setblock 28 {SY+2} -12 minecraft:glowstone    # resource 0: healthy
setblock 28 {SY+3} -12 minecraft:redstone_lamp # resource 1: unhealthy
setblock 28 {SY+4} -12 minecraft:glowstone    # resource 2: healthy
# ... one setblock per resource row
```

**`/clone` syntax:** `clone <x1> <y1> <z1> <x2> <y2> <z2> <destX> <destY> <destZ>`
- Source: columns 12-28 (everything except the leftmost column)
- Destination: columns 11-27 (shifted 1 left)
- The leftmost column (11) gets overwritten by column 12's data
- Column 28 is now a copy of 27 — we overwrite it with fresh data

### C# Implementation Sketch

```csharp
private const int DashboardX = 10;
private const int DashboardZ = -12;
private const int DashboardWidth = 20;   // frame included
private const int DashboardHeight = 10;  // frame included
private const int ScreenMinX = 11;       // usable screen
private const int ScreenMaxX = 28;
private const int ScreenMinY_Offset = 2; // offset from SurfaceY
private const int ScreenMaxY_Offset = 9;

public async Task BuildDashboardFrameAsync(CancellationToken ct)
{
    int sy = VillageLayout.SurfaceY;
    int z = DashboardZ;

    // Bottom frame
    await rcon.SendCommandAsync(
        $"fill {DashboardX} {sy+1} {z} {DashboardX+DashboardWidth-1} {sy+1} {z} minecraft:polished_blackstone", ct);
    // Top frame
    await rcon.SendCommandAsync(
        $"fill {DashboardX} {sy+DashboardHeight} {z} {DashboardX+DashboardWidth-1} {sy+DashboardHeight} {z} minecraft:polished_blackstone", ct);
    // Left frame
    await rcon.SendCommandAsync(
        $"fill {DashboardX} {sy+1} {z} {DashboardX} {sy+DashboardHeight} {z} minecraft:polished_blackstone", ct);
    // Right frame
    await rcon.SendCommandAsync(
        $"fill {DashboardX+DashboardWidth-1} {sy+1} {z} {DashboardX+DashboardWidth-1} {sy+DashboardHeight} {z} minecraft:polished_blackstone", ct);
    // Back panel
    await rcon.SendCommandAsync(
        $"fill {ScreenMinX} {sy+ScreenMinY_Offset} {z-1} {ScreenMaxX} {sy+ScreenMaxY_Offset} {z-1} minecraft:black_concrete", ct);
    // Initial screen: all redstone lamps
    await rcon.SendCommandAsync(
        $"fill {ScreenMinX} {sy+ScreenMinY_Offset} {z} {ScreenMaxX} {sy+ScreenMaxY_Offset} {z} minecraft:redstone_lamp", ct);
}

public async Task UpdateDashboardAsync(CancellationToken ct)
{
    int sy = VillageLayout.SurfaceY;
    int z = DashboardZ;

    // Scroll: clone columns 12-28 to 11-27
    await rcon.SendCommandAsync(
        $"clone {ScreenMinX+1} {sy+ScreenMinY_Offset} {z} {ScreenMaxX} {sy+ScreenMaxY_Offset} {z} {ScreenMinX} {sy+ScreenMinY_Offset} {z}", ct);

    // Write new column at X=28
    int row = sy + ScreenMinY_Offset;
    foreach (var (name, info) in monitor.Resources)
    {
        string block = info.Status switch
        {
            ResourceStatus.Healthy => "minecraft:glowstone",
            ResourceStatus.Unhealthy => "minecraft:redstone_lamp",
            _ => "minecraft:gray_concrete"
        };
        await rcon.SendCommandAsync(
            $"setblock {ScreenMaxX} {row} {z} {block}", ct);
        row++;
        if (row > sy + ScreenMaxY_Offset) break; // screen full
    }
}
```

### Dashboard Label Signs

Place signs below the dashboard identifying resources:

```
# Resource name signs along the bottom (below frame)
setblock 28 {SY} -12 minecraft:oak_sign[rotation=8]
data merge block 28 {SY} -12 {front_text:{messages:["","Resource 0","",""]}}
```

### Dashboard "Title Bar"

A sign or banner above the dashboard:

```
# Title sign centered above frame
setblock 18 {SY+11} -12 minecraft:oak_wall_sign[facing=south]
data merge block 18 {SY+11} -12 {front_text:{messages:["","§lASPIRE","§7Health Dashboard",""]}}
```

(`§l` = bold, `§7` = gray for subtitle)

---

## Summary: Command Counts & Performance

| Structure | Approx RCON Commands | Notes |
|-----------|---------------------|-------|
| Cylinder | ~60 | Round geometry requires per-row fills |
| Enhanced Watchtower | ~30 | +10 over current |
| Enhanced Warehouse | ~25 | +8 over current |
| Enhanced Workshop | ~25 | +8 over current |
| Enhanced Cottage | ~25 | +10 over current |
| Azure Banner | 2 | Flagpole + banner |
| Dashboard Frame (once) | 6 | Frame + back + screen |
| Dashboard Update (per tick) | 1 + N | 1 clone + N setblocks |

All building commands should use `CommandPriority.Low` (bulk building) to avoid starving health-critical commands. Build once, then only update health indicators and dashboard per cycle.
