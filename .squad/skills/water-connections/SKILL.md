---
name: "water-connections"
description: "Techniques for visually connecting water bodies (canals, lakes, rivers) in Minecraft"
domain: "minecraft-world-building"
confidence: "medium"
source: "earned"
---

## Context

Connecting separate water bodies in Minecraft requires careful multi-depth construction. A simple one-block opening creates a visible wall or step. Natural-looking water flow requires opening multiple blocks and filling water at all relevant depth levels.

## Patterns

### 1. Multi-depth junction openings

When connecting a canal to a lake (or two water bodies at different depths), open at least 2 Z-blocks deep and fill water at both depth levels.

```csharp
// Open the wall between canal and lake (2 blocks deep into lake)
await rcon.SendCommandAsync(
    $"fill {minX} {surfaceY-2} {lakeZ} {maxX} {surfaceY} {lakeZ+1} minecraft:air");

// Fill water at canal depth (shallow)
await rcon.SendCommandAsync(
    $"fill {minX} {surfaceY-1} {lakeZ} {maxX} {surfaceY-1} {lakeZ+1} minecraft:water");

// Fill water at lake depth (deep)
await rcon.SendCommandAsync(
    $"fill {minX} {surfaceY-2} {lakeZ} {maxX} {surfaceY-2} {lakeZ+1} minecraft:water");
```

### 2. Extend trunk canals into receiving bodies

Don't stop the trunk canal at the lake edge — extend it 2+ blocks into the lake interior for a clean visual connection.

```csharp
// Trunk canal runs past lake boundary
int trunkEndZ = lakeZ + 2;  // 2 blocks into lake
await rcon.SendCommandAsync(
    $"fill {trunkX-1} {surfaceY-2} {trunkStartZ} {trunkX+1} {surfaceY} {trunkEndZ} minecraft:air");
```

### 3. Branch canal to trunk canal connections

Where a branch canal (E-W) meets the trunk canal (N-S), remove the wall between them and ensure water flows at the correct depth.

```csharp
// Clear the wall between branch and trunk
await rcon.SendCommandAsync(
    $"fill {junctionX} {surfaceY-2} {branchZ-1} {junctionX} {surfaceY} {branchZ+1} minecraft:air");
// Fill water
await rcon.SendCommandAsync(
    $"fill {junctionX} {surfaceY-1} {branchZ-1} {junctionX} {surfaceY-1} {branchZ+1} minecraft:water");
```

### 4. Water depth conventions

| Feature | Floor (bottom) | Water surface | Air above |
|---------|---------------|---------------|-----------|
| Branch canal | SurfaceY - 2 | SurfaceY - 1 | SurfaceY |
| Trunk canal | SurfaceY - 2 | SurfaceY - 1 | SurfaceY |
| Lake | SurfaceY - 3 | SurfaceY - 1 | SurfaceY |
| Junction | Both depths | SurfaceY - 1 | SurfaceY |

## Anti-Patterns

- **Single-block openings** — creates a visible wall between water bodies
- **Mismatched water depths** — canal at Y-1, lake at Y-2, with no water between them at the junction
- **Stopping canals at the lake edge** — creates an abrupt boundary instead of flowing water
- **Forgetting to clear air above water** — leftover blocks from terrain create dams
