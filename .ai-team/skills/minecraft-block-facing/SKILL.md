---
name: "minecraft-block-facing"
description: "Correctly place wall-mounted blocks (levers, torches, signs, banners) in Minecraft via RCON"
domain: "minecraft-interactions"
confidence: "low"
source: "earned"
---

## Context
When placing wall-mounted blocks (levers, torches, ladders, signs, wall banners) in Minecraft via RCON `setblock` commands, the `facing` property determines both the visual direction and which adjacent block provides structural support. Getting this wrong causes blocks to float or immediately pop off.

## Patterns

1. **Facing = extension direction.** `facing=X` means the item visually extends/points in direction X.
2. **Support = opposite of facing.** The solid block the item attaches to is in the OPPOSITE direction from `facing`.
3. **Position the item in empty space, not inside the wall.** The item occupies its own block space; the support block is adjacent.

Direction reference (Minecraft coordinates):
- North = -Z, South = +Z, East = +X, West = -X

For a lever on a building's south-facing front wall (wall at Z-min side):
- Place lever at `wallZ - 1` (one block in front of the wall)
- Use `facing=north` (lever extends toward camera/player)
- Support block is to the south at `(wallZ - 1) + 1 = wallZ` (the wall itself)

## Examples

```csharp
// Lever on front wall (Z-min side), one block in front of wall face
var leverZ = door.FaceZ - 1;
await rcon.SendCommandAsync(
    $"setblock {x} {y} {leverZ} minecraft:lever[face=wall,facing=north,powered=false]");
// Support block is at leverZ + 1 = door.FaceZ (the wall)
```

```csharp
// Wall banner on east wall (X-max side)
var bannerX = wallX + 1;
await rcon.SendCommandAsync(
    $"setblock {bannerX} {y} {z} minecraft:purple_wall_banner[facing=east]");
// Support block at bannerX - 1 = wallX (the wall)
```

## Anti-Patterns

- **Placing the item AT the wall coordinate** — replaces the wall block, and support direction points to interior air (floating item).
- **Using `facing=south` when the wall is to the south** — support would be to the north (away from wall), causing the item to float.
- **Confusing `facing` with "the direction the wall is in"** — `facing` is the direction the item EXTENDS, not where the wall is.
