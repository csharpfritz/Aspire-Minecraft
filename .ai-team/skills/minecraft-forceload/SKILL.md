---
name: "minecraft-forceload"
description: "Ensures all build areas are chunk-loaded before placing blocks via /fill commands"
domain: "minecraft-world-building"
confidence: "high"
source: "earned"
---

## Context

Minecraft's `/fill` command **silently fails** in unloaded chunks — no error is returned, the blocks simply aren't placed. This makes forceload bugs extremely difficult to diagnose because the server reports success.

Any system that places blocks in the world MUST ensure the target chunks are forceloaded before issuing `/fill` commands.

## Patterns

### 1. Forceload AFTER computing final bounds

The village layout may change during initialization (e.g., `PlanNeighborhoods()` spreads buildings into 4 quadrants with `ZoneGap=20`). Forceload must happen AFTER the final layout is computed, not before.

```csharp
// ❌ WRONG — bounds computed before layout finalization
var bounds = layout.GetFencePerimeter(10);
await ForceloadArea(bounds);
layout.PlanNeighborhoods(); // spreads buildings beyond forceloaded area

// ✅ CORRECT — bounds computed after layout finalization  
layout.PlanNeighborhoods();
var bounds = layout.GetFencePerimeter(resourceCount);
await ForceloadArea(bounds);
```

### 2. Small initial forceload for terrain probing

`DetectSurfaceAsync` needs a few chunks loaded to probe the terrain height. Use a minimal forceload around the probe point before the full village forceload.

```csharp
// Small area for terrain detection
await rcon.SendCommandAsync($"forceload add {baseX - 5} {baseZ - 5} {baseX + 5} {baseZ + 5}");
var surfaceY = await DetectSurfaceAsync();

// Full area after layout planning
await rcon.SendCommandAsync($"forceload add {minX} {minZ} {maxX} {maxZ}");
```

### 3. Forceload must cover ALL build areas

Any area where blocks will be placed needs forceloading — not just the main village grid:
- Fence perimeter (extends beyond buildings by `FenceClearance`)
- Lake (at southern edge, `LakeGap` beyond the last building row)
- Canals (trunk canal extends from buildings to the lake)
- Paths between buildings

### 4. Log forceload coordinates

Always log the forceload area for debugging, since failures are silent.

```csharp
logger.LogInformation("Forceloading area: ({MinX},{MinZ}) to ({MaxX},{MaxZ})", minX, minZ, maxX, maxZ);
```

## Anti-Patterns

- **Computing forceload bounds from a partial layout** — if any initialization step changes the layout extent, forceload must come after ALL such steps
- **Assuming /fill will report errors** — Minecraft silently ignores fills in unloaded chunks
- **Forgetting peripheral areas** — lakes, canals, and decorations outside the main building grid need forceloading too
- **Single forceload for everything** — use a small initial forceload for terrain probing, then a full forceload after layout finalization
