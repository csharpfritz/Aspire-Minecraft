# Decision: Resource Village Layout & Themed Structures

**By:** Rocket
**Issue:** #25
**Date:** 2026-02-10

## What

Replaced simple 3×3×2 colored block platforms with themed mini-buildings per Aspire resource type, arranged in a 2×N grid with 10-block spacing.

## Structure Mapping

| Resource Type | Structure | Block Palette |
|---|---|---|
| Project | Watchtower (10 tall) | Stone brick + blue wool/banner |
| Container | Warehouse (5 tall) | Iron block + purple stained glass |
| Executable | Workshop (7 tall) | Oak planks + cyan stained glass + cobblestone chimney |
| Unknown/Other | Cottage (5 tall) | Cobblestone + light blue wool trim |

## Layout

- **Old:** Linear row, Spacing=6, 3×3×2 platforms
- **New:** 2-column grid, Spacing=10, 7×7 footprint structures, cobblestone paths

## Centralized Coordinates (VillageLayout)

Created `VillageLayout` static class to centralize position calculations. All services that place per-resource items (particles, beacons, guardians) now use the same grid math.

- `GetStructureOrigin(index)` → corner position
- `GetStructureCenter(index)` → center position (offset +3,+3)
- `GetAboveStructure(index, height)` → position above the structure

## Health Indicator

Replaced torch-on-top with redstone lamp block in front wall:
- Healthy → `minecraft:glowstone` (always lit)
- Unhealthy → `minecraft:redstone_lamp` (unlit, dark)
- Unknown → `minecraft:sea_lantern` (distinct starting color)

## Why

- Simple colored platforms weren't visually interesting for demos/conferences
- Themed structures give each resource type a distinct visual identity
- 2×N grid scales better than linear row (10 resources = 5 rows vs 60 blocks wide)
- `VillageLayout` prevents coordinate drift bugs between services
