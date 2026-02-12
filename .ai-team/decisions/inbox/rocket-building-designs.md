# Decision: Sprint 4 Building Design Specifications

**By:** Rocket
**Date:** 2026-02-11
**Status:** Proposed

## What

Design specifications for four Sprint 4 building enhancements requested by Jeff:

### 1. Database Cylinder Building
- Radius-3 circle (7-block diameter) fits perfectly in the existing 7×7 grid cell
- Smooth stone walls + polished deepslate cap = "data center" aesthetic
- Domed roof (2-layer: smooth stone slab outer ring, polished deepslate slab inner cap)
- 7 blocks total height (floor + 4 wall layers + 2 dome layers)
- Door on south face (z+0), 3-wide × 2-tall opening
- Health lamp at (x+3, y+3, z+0) — above the door
- ~60 RCON commands to build (3x more than rectangular buildings due to per-row geometry)
- New structure type "Cylinder" in `GetStructureType` for database/postgres/mysql/sqlserver/redis/mongodb resource types

### 2. Azure Flag/Banner
- `light_blue_banner` base with white stripe (`str`) and base (`bs`) patterns
- NBT: `{Patterns:[{Color:0,Pattern:"str"},{Color:0,Pattern:"bs"}]}`
- Placed on rooftop flagpole (2-block oak fence + banner), same pattern as existing Watchtower flag
- Azure detection via `info.Type.Contains("azure")` or name match
- Roof Y varies by structure type (documented per-type)

### 3. Enhanced Building Palettes
- **Watchtower:** Chiseled stone floor, deepslate pillars, polished andesite band, battlements, bookshelves + lantern interior
- **Warehouse:** Orange concrete accent stripe (shipping container look), gray glass, iron trapdoor corrugated roof, chains + soul lanterns
- **Workshop:** Spruce timber frame, dark oak peaked roof, blast furnace + smithing table, redstone torches
- **Cottage:** Mossy cobblestone accents, stripped oak timber frame band, peaked oak stair roof, flower pots + awning

### 4. Dashboard Wall
- 20×10 block frame (polished blackstone) with 18×8 usable redstone lamp grid
- Placement: (X=10, Y=SurfaceY+2, Z=-12) — behind village, facing south
- Block-swap technique (glowstone=lit, redstone_lamp=unlit, gray_concrete=unknown) — no redstone wiring needed
- `/clone` scroll: copies columns 12-28 → 11-27, then writes fresh data at column 28
- Black concrete backing panel for contrast
- Title sign: "ASPIRE Health Dashboard"

## Why

Jeff wants Sprint 4 to include more visually distinct buildings, database-specific structures, Azure branding, and a health dashboard. These designs fit within the existing village grid system and follow established RCON patterns from Sprints 1-3.

## Implementation Notes

- Full RCON command sequences documented in `docs/designs/minecraft-building-reference.md`
- All commands use `CommandPriority.Low` for bulk building
- Door openings cleared LAST in all build sequences (learned from Sprint 3.1)
- Cylinder building is the most RCON-expensive structure (~60 commands vs ~20 for rectangular)
- Dashboard `/clone` is 1 command per scroll tick — very efficient
