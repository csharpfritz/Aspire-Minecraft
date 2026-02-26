---
name: "building-protection"
description: "Prevents subsystems from modifying blocks within placed building volumes using 3D bounding box clipping"
domain: "minecraft-world-building"
confidence: "medium"
source: "earned"
---

## Context

When multiple subsystems modify the Minecraft world (buildings, canals, rails, paths), later systems can accidentally destroy blocks placed by earlier ones. The BuildingProtectionService provides a central registry of 3D bounding boxes that other subsystems must respect.

## Patterns

### 1. Register buildings after placement

After a building is constructed, register its 3D bounding box with the protection service. Include buffer zones for entrances and aesthetic spacing.

```csharp
// Registration in StructureBuilder after building construction
protectionService.Register(
    resourceName,
    minX: x - 1,         // 1-block buffer on sides
    minY: surfaceY,       // ground level
    minZ: z - 2,          // 2-block entrance buffer (south)
    maxX: x + StructureSize,
    maxY: surfaceY + 25,  // full building height
    maxZ: z + StructureSize
);
```

### 2. ClipFill for protected excavation

When a subsystem needs to `/fill` an area that might overlap buildings, use `ClipFill` to get non-overlapping sub-boxes.

```csharp
var subBoxes = protectionService.ClipFill(minX, minY, minZ, maxX, maxY, maxZ);
foreach (var (bMinX, bMinY, bMinZ, bMaxX, bMaxY, bMaxZ) in subBoxes)
{
    await rcon.SendCommandAsync(
        $"fill {bMinX} {bMinY} {bMinZ} {bMaxX} {bMaxY} {bMaxZ} minecraft:air");
}
```

### 3. Axis-aligned box subtraction

`SubtractBox` yields up to 6 non-overlapping remainder sub-boxes when removing a protected region from a fill volume:
- X left / X right (full Y and Z range of overlap)
- Y below / Y above (clamped to X overlap range)
- Z front / Z back (clamped to X+Y overlap range)

### 4. Initialization order matters

Buildings must be placed BEFORE canals/rails so the protection registry is populated when those systems run.

## Anti-Patterns

- **Checking protection after the fact** — always clip BEFORE sending /fill commands
- **Forgetting buffer zones** — buildings need clearance for entrances and aesthetics
- **Relying on Y-range non-overlap** — canal depth (SurfaceY-2 to SurfaceY) may not overlap building Y (SurfaceY+1 to SurfaceY+26) today, but future features might
