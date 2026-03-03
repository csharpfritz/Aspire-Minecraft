# SKILL: Fill-Overlap Detection

## Purpose

Detect when RCON `/fill` commands in StructureBuilder silently overwrite blocks placed by earlier fills. Catches the class of bugs where architectural layering isn't properly sequenced.

## When to Use

- After adding a new building type
- After modifying fill command sequences in any building method
- When reviewing structural changes for geometric correctness

## Pattern

```csharp
// 1. Parse fill commands into bounding boxes
record FillBox(int MinX, int MinY, int MinZ, int MaxX, int MaxY, int MaxZ, string Block, string RawCommand);

FillBox? ParseFillCommand(string command)
{
    // Parse "fill x1 y1 z1 x2 y2 z2 minecraft:block [modifiers]"
    // Normalize min/max coordinates
}

// 2. Check all pairs for overlaps
bool BoxesOverlap(FillBox a, FillBox b)
{
    return a.MinX <= b.MaxX && a.MaxX >= b.MinX
        && a.MinY <= b.MaxY && a.MaxY >= b.MinY
        && a.MinZ <= b.MaxZ && a.MaxZ >= b.MinZ;
}

// 3. Filter intentional overlaps
// - Air/cave_air fills (clearing)
// - Hollow fills (shell only)
// - Same block type (redundant)
// - Smaller detail over larger structure
// - Interior furnishing inside wall volumes
// - Same material family upgrades
// - Wool/banner decorative trim

// 4. Assert zero unintentional overlaps
```

## Files

- `tests/Aspire.Hosting.Minecraft.Worker.Tests/Services/FillOverlapDetectionTests.cs` — full implementation
- Helper: `BuildAndDetectOverlaps(name, type, grandLayout)` — one-line test setup
- `IsIntentionalLayering(earlier, later)` — whitelist logic

## Key Insight

Minecraft building technique deliberately uses architectural layering (fill large region, then overlay details). The test must distinguish between this intentional pattern and actual bugs where a fill silently destroys intended blocks.
