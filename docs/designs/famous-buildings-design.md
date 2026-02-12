# Famous Buildings Feature — Technical Design

> **Author:** Rhodey (Lead)
> **Date:** 2026-02-12
> **Requested by:** Jeffrey T. Fritz
> **Status:** 📐 Design — ready for review

---

## Table of Contents

1. [Vision Statement](#1-vision-statement)
2. [API Surface Design](#2-api-surface-design)
3. [FamousBuilding Enum](#3-famousbuilding-enum)
4. [Building Model System](#4-building-model-system)
5. [Integration with Existing System](#5-integration-with-existing-system)
6. [Data Flow Architecture](#6-data-flow-architecture)
7. [Sprint Sizing & Phasing](#7-sprint-sizing--phasing)
8. [Risk Analysis](#8-risk-analysis)

---

## 1. Vision Statement

Users should be able to assign a famous real-world building to any Aspire resource so that when the Minecraft village is built, that resource is represented by a recognizable landmark instead of the default auto-detected structure type. A Redis cache becomes the Colosseum; a Postgres database becomes a Pyramid.

**Jeff's requested syntax:**

```csharp
var redis = builder.AddRedis("cache");

var minecraft = builder.AddMinecraftServer("mc")
    .WithAspireWorldDisplay<Projects.Worker>()
    .WithMonitoredResource(redis);

// NEW — famous building override on the resource itself
redis.AsMinecraftFamousBuilding(FamousBuilding.BigBenClockTower);
```

This is a fundamentally different API shape from existing `With*()` methods. Today, all configuration methods chain off `IResourceBuilder<MinecraftServerResource>` (the Minecraft server). Jeff's request puts configuration on the **monitored resource itself** — the Redis, Postgres, or project resource. This is the right call: the building choice belongs to the resource being visualized, not to the Minecraft server.

---

## 2. API Surface Design

### 2.1 The Extension Method

```csharp
namespace Aspire.Hosting.Minecraft;

/// <summary>
/// Extension methods for assigning famous building representations to Aspire resources
/// displayed in the Minecraft world.
/// </summary>
public static class FamousBuildingExtensions
{
    /// <summary>
    /// Assigns a famous real-world building as the Minecraft structure for this resource.
    /// Overrides the default structure type detection (Watchtower, Warehouse, etc.).
    /// The resource must be registered via <see cref="MinecraftServerBuilderExtensions.WithMonitoredResource"/>
    /// for the building to appear in the Minecraft world.
    /// </summary>
    /// <typeparam name="T">The resource type.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="building">The famous building to construct for this resource.</param>
    /// <returns>The resource builder for chaining.</returns>
    public static IResourceBuilder<T> AsMinecraftFamousBuilding<T>(
        this IResourceBuilder<T> builder,
        FamousBuilding building)
        where T : IResource
    {
        builder.WithAnnotation(new FamousBuildingAnnotation(building));
        return builder;
    }
}
```

### 2.2 Design Decisions

**Q: Should this work on `IResourceBuilder<T> where T : IResource` (any resource) or only specific types?**

**A: Any resource.** The method should work on `IResourceBuilder<T> where T : IResource`. Rationale:
- The famous building is a visual override — it's meaningful for any resource that gets rendered in the village.
- Restricting to `IResourceWithEndpoints` would exclude resources registered via the second `WithMonitoredResource` overload (resources without endpoints).
- The constraint `IResource` matches the broadest possible Aspire resource type. The building annotation is inert unless the resource is also registered via `WithMonitoredResource`.

**Q: Why not chain off `MinecraftServerResource`?**

**A: Because the building choice belongs to the resource, not the server.** Jeff's syntax `redis.AsMinecraftFamousBuilding(...)` reads naturally. It says "this Redis resource should look like Big Ben." Putting it on the Minecraft server builder would require passing the resource as a parameter, which is less clean:
```csharp
// ❌ Less natural — building choice is on the wrong object
minecraft.WithFamousBuilding(redis, FamousBuilding.BigBenClockTower);

// ✅ Natural — the resource declares its own appearance
redis.AsMinecraftFamousBuilding(FamousBuilding.BigBenClockTower);
```

**Q: What if `AsMinecraftFamousBuilding` is called but the resource isn't monitored?**

**A: The annotation is silently ignored.** No runtime error. The annotation sits on the resource and is never read because no Minecraft worker is looking at it. This follows .NET convention — annotations are additive metadata, not runtime contracts.

### 2.3 Annotation Type

```csharp
namespace Aspire.Hosting.Minecraft;

/// <summary>
/// Annotation that stores the user's famous building selection on an Aspire resource.
/// Read by <see cref="MinecraftServerBuilderExtensions.WithMonitoredResource"/> to
/// propagate the selection to the worker via environment variables.
/// </summary>
internal sealed class FamousBuildingAnnotation(FamousBuilding building) : IResourceAnnotation
{
    public FamousBuilding Building { get; } = building;
}
```

### 2.4 How the Selection Flows to the Worker

The existing `WithMonitoredResource` method already inspects the resource and sets environment variables on the worker (`ASPIRE_RESOURCE_{NAME}_TYPE`, `_URL`, `_HOST`, `_PORT`, `_DEPENDS_ON`). We add one more env var:

```csharp
// Inside WithMonitoredResource (IResourceWithEndpoints overload):
if (resource.Resource.Annotations.OfType<FamousBuildingAnnotation>().FirstOrDefault() is { } famous)
{
    workerBuilder.WithEnvironment(
        $"ASPIRE_RESOURCE_{name.ToUpperInvariant()}_FAMOUS_BUILDING",
        famous.Building.ToString());
}
```

The same logic is added to the second `WithMonitoredResource` overload (for `IResource`).

**On the worker side**, `AspireResourceMonitor.DiscoverResources()` reads this env var:

```csharp
var famousBuilding = Environment.GetEnvironmentVariable(
    $"ASPIRE_RESOURCE_{upperName}_FAMOUS_BUILDING") ?? "";

_resources[name] = new ResourceInfo(name, type, url, host, port,
    ResourceStatus.Unknown, dependencies, famousBuilding);
```

`ResourceInfo` gains a `string FamousBuilding` property (empty string = use auto-detection).

### 2.5 Ordering Guarantee

`AsMinecraftFamousBuilding()` can be called before or after `WithMonitoredResource()`. This works because:
- `AsMinecraftFamousBuilding()` adds an annotation to the resource.
- `WithMonitoredResource()` reads annotations from the resource at call time.
- As long as `AsMinecraftFamousBuilding()` is called **before** `WithMonitoredResource()`, the annotation is present when read.

If called after `WithMonitoredResource()`, the annotation would be missed. To handle this, `WithMonitoredResource` should use a deferred environment callback (the existing `WithEnvironment(context => ...)` pattern) so annotations are read at app-build time, not at method-call time:

```csharp
workerBuilder.WithEnvironment(context =>
{
    if (resource.Resource.Annotations.OfType<FamousBuildingAnnotation>().FirstOrDefault() is { } famous)
    {
        context.EnvironmentVariables[$"ASPIRE_RESOURCE_{name.ToUpperInvariant()}_FAMOUS_BUILDING"] =
            famous.Building.ToString();
    }
});
```

This ensures order-independence.

---

## 3. FamousBuilding Enum

### 3.1 Selection Criteria

Each famous building must:
1. **Be recognizable at 15–30 block scale** — distinctive silhouette visible from ground level.
2. **Have a unique shape** — no two buildings should look similar in Minecraft.
3. **Be constructable with standard Minecraft blocks** — no custom resource packs.
4. **Be culturally diverse** — represent multiple continents and civilizations.
5. **Be fun** — buildings that make you smile when you see them in a blocky village.

### 3.2 Enum Definition

```csharp
namespace Aspire.Hosting.Minecraft;

/// <summary>
/// Famous real-world buildings that can represent Aspire resources in the Minecraft village.
/// Each building is constructed at a fixed size with deterministic block placement.
/// Assign to a resource via <see cref="FamousBuildingExtensions.AsMinecraftFamousBuilding{T}"/>.
/// </summary>
public enum FamousBuilding
{
    /// <summary>
    /// Big Ben Clock Tower (London, UK).
    /// Tall rectangular tower with clock face and pointed spire.
    /// ~25 blocks tall, 7×7 base. Stone brick with gold accents.
    /// Recognizable features: clock face (white concrete + black signs), gothic arch windows,
    /// gold spire (gold blocks + lightning rod).
    /// </summary>
    BigBenClockTower,

    /// <summary>
    /// Eiffel Tower (Paris, France).
    /// Iconic lattice tower with tapered profile.
    /// ~30 blocks tall, 11×11 base tapering to 3×3 top. Iron bars + iron blocks.
    /// Recognizable features: four angled legs converging upward, observation platforms
    /// at three levels, iron bar lattice walls.
    /// </summary>
    EiffelTower,

    /// <summary>
    /// Roman Colosseum (Rome, Italy).
    /// Oval amphitheater with tiered arched walls.
    /// ~10 blocks tall, 15×15 oval footprint. Sandstone + stone brick.
    /// Recognizable features: three tiers of arched openings, open-air center arena,
    /// partial ruin (one quadrant deliberately incomplete for authenticity).
    /// </summary>
    Colosseum,

    /// <summary>
    /// Great Pyramid (Giza, Egypt).
    /// Stepped pyramid with smooth sandstone faces.
    /// ~15 blocks tall, 15×15 base. Smooth sandstone + sandstone stairs.
    /// Recognizable features: perfect pyramid shape, entrance on north face,
    /// gold block capstone (pyramidion).
    /// </summary>
    Pyramid,

    /// <summary>
    /// Taj Mahal (Agra, India).
    /// Domed mausoleum with four minarets.
    /// ~18 blocks tall, 13×13 base. White concrete + quartz.
    /// Recognizable features: central onion dome (quartz stairs/slabs), four corner
    /// minaret towers, reflective pool in front (water source blocks), arched entrance.
    /// </summary>
    TajMahal,

    /// <summary>
    /// Lighthouse (generic coastal lighthouse).
    /// Cylindrical tower with lantern room at top.
    /// ~20 blocks tall, 7×7 circular base. White concrete + red concrete stripes.
    /// Recognizable features: red-and-white spiral stripe pattern, glass lantern room,
    /// glowstone light source, small keeper's cottage at base.
    /// </summary>
    Lighthouse,

    /// <summary>
    /// Windmill (Dutch style).
    /// Cylindrical tower with rotating sail arms.
    /// ~15 blocks tall, 7×7 base. Cobblestone + dark oak planks.
    /// Recognizable features: four sail arms extending from center (oak fence + white wool),
    /// conical roof, working door, small balcony platform.
    /// </summary>
    Windmill,

    /// <summary>
    /// Medieval Castle Tower (generic European castle keep).
    /// Square fortified tower with battlements and arrow slits.
    /// ~20 blocks tall, 11×11 base. Stone brick + mossy stone brick.
    /// Recognizable features: crenellated battlements, arrow slit windows,
    /// drawbridge entrance (dark oak trapdoors), corner turrets, banner array.
    /// </summary>
    CastleTower,

    /// <summary>
    /// Japanese Pagoda (five-story style).
    /// Tiered tower with curved roofs at each level.
    /// ~25 blocks tall, 9×9 base. Crimson planks + dark oak stairs.
    /// Recognizable features: five overhanging roof tiers (dark oak stairs),
    /// each progressively smaller, lanterns at eaves, fence post finial.
    /// </summary>
    Pagoda,

    /// <summary>
    /// Statue of Liberty (New York, USA).
    /// Statue on pedestal holding torch aloft.
    /// ~25 blocks tall, 9×9 pedestal base. Oxidized copper + copper blocks.
    /// Recognizable features: green oxidized copper body, raised torch arm
    /// (glowstone flame), crown with spikes (iron bars), stone pedestal base.
    /// </summary>
    StatueOfLiberty,

    /// <summary>
    /// Greek Parthenon (Athens, Greece).
    /// Rectangular temple with columned facade.
    /// ~10 blocks tall, 15×11 base. Quartz blocks + quartz pillars.
    /// Recognizable features: row of columns on all sides, triangular pediment
    /// (quartz stairs), open interior with statue niche, stepped base platform.
    /// </summary>
    Parthenon,

    /// <summary>
    /// Sydney Opera House (Sydney, Australia).
    /// Distinctive sail-shaped roof shells.
    /// ~12 blocks tall, 15×9 base. White concrete + white concrete stairs.
    /// Recognizable features: three interlocking sail shells (white concrete stairs
    /// layered at angles), waterfront platform, glass curtain wall entrance.
    /// </summary>
    OperaHouse,

    /// <summary>
    /// Mayan Pyramid (Chichén Itzá, Mexico).
    /// Stepped pyramid with flat-topped temple.
    /// ~15 blocks tall, 15×15 base. Mossy stone brick + chiseled stone brick.
    /// Recognizable features: nine stepped tiers, flat-topped temple room,
    /// central staircase on front face, vine accents (leaf blocks on sides).
    /// </summary>
    MayanPyramid,

    /// <summary>
    /// Leaning Tower (Pisa, Italy).
    /// Cylindrical bell tower with deliberate tilt.
    /// ~22 blocks tall, 7×7 base. White concrete + quartz pillars.
    /// Recognizable features: off-center lean (~3 blocks offset at top),
    /// tiered colonnades (quartz pillars at each floor), open bell chamber at top.
    /// </summary>
    LeaningTower,

    /// <summary>
    /// Chinese Great Wall Watchtower (China).
    /// Fortified gatehouse section of the Great Wall.
    /// ~12 blocks tall, 13×9 base. Stone brick + cobblestone.
    /// Recognizable features: wide wall segments extending from sides,
    /// crenellated parapet, arched gate passage, watchtower room on top.
    /// </summary>
    GreatWallTower
}
```

### 3.3 Geographic Diversity

| Region | Buildings |
|--------|-----------|
| Europe | Big Ben, Eiffel Tower, Colosseum, Parthenon, Leaning Tower |
| Asia | Taj Mahal, Pagoda, Great Wall Tower |
| Africa | Pyramid |
| Americas | Statue of Liberty, Mayan Pyramid |
| Oceania | Opera House |
| Generic | Lighthouse, Windmill, Castle Tower |

### 3.4 Minecraft Buildability Rating

| Building | Complexity | RCON Commands (est.) | Notes |
|----------|-----------|---------------------|-------|
| Pyramid | Low | ~60 | Mostly `/fill` — layer-by-layer squares |
| Windmill | Medium | ~80 | Cylindrical base + sail arm geometry |
| Lighthouse | Medium | ~85 | Cylindrical + stripe pattern |
| Parthenon | Medium | ~90 | Columns are individual `/setblock` runs |
| Castle Tower | Medium | ~95 | Similar to existing Grand Watchtower |
| Big Ben | Medium-High | ~110 | Tall + clock face detail |
| Colosseum | High | ~130 | Oval geometry, arched openings |
| Pagoda | High | ~120 | Five separate roof tiers |
| Eiffel Tower | High | ~140 | Tapered lattice geometry |
| Taj Mahal | High | ~150 | Dome + minarets |
| Statue of Liberty | Very High | ~180 | Irregular human form at block scale |
| Opera House | Very High | ~160 | Curved sail shells |
| Mayan Pyramid | Low-Medium | ~70 | Stepped squares, very `/fill` friendly |
| Leaning Tower | High | ~130 | Offset circular geometry per layer |
| Great Wall Tower | Medium | ~100 | Rectangular, `/fill` friendly |

---

## 4. Building Model System

### 4.1 Approach: Hybrid — C# Methods with Data-Driven Layer Maps

Jeff said "very fixed model" — every build must be deterministic and identical. Three options were evaluated:

| Approach | Pros | Cons |
|----------|------|------|
| **A. Pure C# (like current StructureBuilder)** | Simple, debuggable, no file I/O, no deserialization | Verbose (150+ lines per building), hard to visualize during authoring |
| **B. JSON layer maps** | Easy to author with visual tools, separates design from code | Adds file loading, parsing, and a schema to maintain |
| **C. NBT schematics** | Industry-standard Minecraft format, can export from creative builds | Complex NBT parsing, large binary files, hard to diff/review |

**Recommendation: Option A (Pure C#) with structured helpers.**

Rationale:
- The current `StructureBuilder` already uses this pattern and it works well.
- 15 buildings × ~100 lines each ≈ 1,500 lines — manageable in 2–3 source files.
- C# methods are easy to test: assert that a building produces exactly N RCON commands at exactly the right coordinates.
- No runtime file loading, no deserialization bugs, no missing resource files.
- Building methods can use shared helpers (`FillWall`, `PlaceColumn`, `BuildDome`, `BuildPyramidLayer`) to reduce code volume.

### 4.2 Code Organization

```
src/Aspire.Hosting.Minecraft.Worker/Services/
├── StructureBuilder.cs              (existing — auto-detected structures)
├── FamousBuildingBuilder.cs         (NEW — famous building construction)
├── FamousBuildingModels/            (NEW — one file per building)
│   ├── BigBenModel.cs
│   ├── EiffelTowerModel.cs
│   ├── ColosseumModel.cs
│   ├── PyramidModel.cs
│   ├── TajMahalModel.cs
│   ├── LighthouseModel.cs
│   ├── WindmillModel.cs
│   ├── CastleTowerModel.cs
│   ├── PagodaModel.cs
│   ├── StatueOfLibertyModel.cs
│   ├── ParthenonModel.cs
│   ├── OperaHouseModel.cs
│   ├── MayanPyramidModel.cs
│   ├── LeaningTowerModel.cs
│   └── GreatWallTowerModel.cs
└── BuildingHelpers.cs              (NEW — shared geometry helpers)
```

### 4.3 Building Model Interface

```csharp
namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Defines a famous building model that can be constructed in the Minecraft world.
/// Each implementation produces a deterministic sequence of RCON commands to build
/// the structure at a given origin point.
/// </summary>
internal interface IFamousBuildingModel
{
    /// <summary>The famous building this model constructs.</summary>
    FamousBuilding Building { get; }

    /// <summary>Width (X-axis) of the building footprint in blocks.</summary>
    int Width { get; }

    /// <summary>Depth (Z-axis) of the building footprint in blocks.</summary>
    int Depth { get; }

    /// <summary>Height (Y-axis) of the building in blocks above origin.</summary>
    int Height { get; }

    /// <summary>
    /// Constructs the building at the given origin point using RCON commands.
    /// Origin is the southwest corner at ground level (same convention as VillageLayout).
    /// </summary>
    Task BuildAsync(RconService rcon, int x, int y, int z, CancellationToken ct);
}
```

### 4.4 Shared Geometry Helpers

```csharp
namespace Aspire.Hosting.Minecraft.Worker.Services;

/// <summary>
/// Reusable geometry primitives for building construction.
/// All methods emit RCON setblock/fill commands.
/// </summary>
internal static class BuildingHelpers
{
    /// <summary>Fill a rectangular volume with a single block type.</summary>
    public static Task FillAsync(RconService rcon, int x1, int y1, int z1,
        int x2, int y2, int z2, string block, CancellationToken ct);

    /// <summary>Fill the outline of a rectangle (walls only, hollow inside).</summary>
    public static Task FillHollowAsync(RconService rcon, int x1, int y1, int z1,
        int x2, int y2, int z2, string block, CancellationToken ct);

    /// <summary>Build a circular ring of blocks at a given Y level.</summary>
    public static Task BuildCircleAsync(RconService rcon, int cx, int cz, int y,
        int radius, string block, CancellationToken ct);

    /// <summary>Build a filled circular disc at a given Y level.</summary>
    public static Task BuildDiscAsync(RconService rcon, int cx, int cz, int y,
        int radius, string block, CancellationToken ct);

    /// <summary>Build a dome (hemisphere) rising from a given Y level.</summary>
    public static Task BuildDomeAsync(RconService rcon, int cx, int cz, int baseY,
        int radius, string block, CancellationToken ct);

    /// <summary>Build a pyramid (square, layer-by-layer) from base to apex.</summary>
    public static Task BuildPyramidAsync(RconService rcon, int cx, int cz, int baseY,
        int baseSize, string block, CancellationToken ct);

    /// <summary>Place a single block.</summary>
    public static Task SetBlockAsync(RconService rcon, int x, int y, int z,
        string block, CancellationToken ct);

    /// <summary>Build a column (vertical line) of blocks.</summary>
    public static Task BuildColumnAsync(RconService rcon, int x, int z,
        int fromY, int toY, string block, CancellationToken ct);
}
```

### 4.5 Example: Pyramid Model

```csharp
internal sealed class PyramidModel : IFamousBuildingModel
{
    public FamousBuilding Building => FamousBuilding.Pyramid;
    public int Width => 15;
    public int Depth => 15;
    public int Height => 15;

    public async Task BuildAsync(RconService rcon, int x, int y, int z, CancellationToken ct)
    {
        // Layer-by-layer pyramid: each layer is a filled square, shrinking by 1 on each side
        for (var layer = 0; layer < 15; layer++)
        {
            var inset = layer; // each layer shrinks by 1 block per side
            var halfRemaining = 15 - (2 * inset);
            if (halfRemaining <= 0) break;

            await BuildingHelpers.FillAsync(rcon,
                x + inset, y + layer, z + inset,
                x + 14 - inset, y + layer, z + 14 - inset,
                "minecraft:smooth_sandstone", ct);
        }

        // Capstone
        await BuildingHelpers.SetBlockAsync(rcon, x + 7, y + 14, z + 7,
            "minecraft:gold_block", ct);

        // Entrance (north face, ground level)
        await BuildingHelpers.FillAsync(rcon,
            x + 6, y + 1, z, x + 8, y + 3, z + 2,
            "minecraft:air", ct);
    }
}
```

---

## 5. Integration with Existing System

### 5.1 Override Behavior

When a resource has a `FamousBuildingAnnotation`, the worker must skip auto-detection and use the famous building instead.

**Current flow (StructureBuilder):**
```
ResourceInfo.Type → GetStructureType() → "Watchtower"/"Warehouse"/etc. → Build method
```

**New flow:**
```
ResourceInfo.FamousBuilding is not empty?
  YES → FamousBuildingBuilder.BuildAsync(famousBuilding, x, y, z)
  NO  → GetStructureType() → existing flow (unchanged)
```

Modified `BuildResourceStructureAsync`:

```csharp
private async Task BuildResourceStructureAsync(ResourceInfo info, int index, CancellationToken ct)
{
    var (x, y, z) = VillageLayout.GetStructureOrigin(index);

    if (!string.IsNullOrEmpty(info.FamousBuilding))
    {
        // Famous building override — skip auto-detection
        await _famousBuildingBuilder.BuildAsync(info.FamousBuilding, x, y, z, ct);
    }
    else
    {
        // Existing auto-detection flow
        var structureType = GetStructureType(info.Type);
        switch (structureType) { /* existing cases */ }
    }

    // Health indicator + sign placement continues as normal
    await PlaceHealthIndicatorAsync(x, y, z, /* ... */);
    await PlaceSignAsync(x, y, z, info, ct);
}
```

### 5.2 Village Layout Implications

Famous buildings have variable sizes (7×7 to 15×15). Two approaches:

**Option A: Fixed grid slot (recommended).** Every building gets the same grid slot regardless of actual size. Smaller buildings (7×7 Windmill) sit in a 15×15 slot with empty space. Larger buildings fill the slot. This keeps `VillageLayout` unchanged and avoids per-resource spacing calculations.

**Option B: Adaptive spacing.** The village grid adapts slot sizes based on each building's `Width`/`Depth`. This adds significant complexity to `VillageLayout` and breaks the simple 2×N grid math.

**Decision: Option A.** The grid is already designed for 15×15 (Sprint 5 Grand Village). Famous buildings must fit within the 15×15 footprint. Buildings that would naturally be larger (Eiffel Tower at true scale) are scaled down to fit. The enum descriptions specify dimensions within this constraint.

### 5.3 Interaction with Grand Village

`AsMinecraftFamousBuilding()` works with or without `WithGrandVillage()`:

- **Without Grand Village (7×7 grid):** Famous buildings are capped at 7×7 footprint. Buildings larger than 7×7 in the enum are scaled down. A configuration warning is logged recommending `WithGrandVillage()` for the best experience.
- **With Grand Village (15×15 grid):** Full-size famous buildings render as designed.

This means building models should implement two size variants, or a single model that adapts to `VillageLayout.StructureSize`.

**Recommended approach:** Famous buildings require `WithGrandVillage()`. If `StructureSize < 15`, the worker logs a warning and falls back to the auto-detected structure type, ignoring the famous building override. This avoids the complexity of dual-size models and keeps the feature clean.

### 5.4 Health Indicators & Signs

Famous buildings still get:
- **Health indicator lamp** — positioned at a consistent location (e.g., beside the entrance door) regardless of building shape.
- **Resource name sign** — placed at the entrance of the building.
- **Azure banner** — if the resource is Azure-typed AND has a famous building, the azure banner is placed on the famous building's roof.

The `IFamousBuildingModel` interface provides `Width`, `Depth`, `Height` so the caller knows where to place these additive elements.

---

## 6. Data Flow Architecture

### 6.1 End-to-End Flow

```
AppHost (compile-time)                    Worker (runtime)
─────────────────────                     ───────────────────
1. redis.AsMinecraftFamousBuilding(       
     FamousBuilding.Pyramid)             
   → Adds FamousBuildingAnnotation        
     to redis resource                    
                                          
2. minecraft.WithMonitoredResource(redis) 
   → Reads FamousBuildingAnnotation       
   → Sets env var on worker:              
     ASPIRE_RESOURCE_CACHE_FAMOUS_         
     BUILDING=Pyramid                     
                                         3. AspireResourceMonitor.DiscoverResources()
                                            → Reads ASPIRE_RESOURCE_CACHE_
                                              FAMOUS_BUILDING=Pyramid
                                            → Stores in ResourceInfo.FamousBuilding
                                          
                                         4. StructureBuilder.BuildResourceStructureAsync()
                                            → info.FamousBuilding == "Pyramid"
                                            → Delegates to FamousBuildingBuilder
                                            → PyramidModel.BuildAsync(rcon, x, y, z)
                                            → RCON: /fill, /setblock commands
```

### 6.2 Env Var Convention

Following the established pattern:

```
ASPIRE_RESOURCE_{NAME}_TYPE=PostgresServer           (existing)
ASPIRE_RESOURCE_{NAME}_URL=http://localhost:5432      (existing)
ASPIRE_RESOURCE_{NAME}_DEPENDS_ON=cache              (existing)
ASPIRE_RESOURCE_{NAME}_FAMOUS_BUILDING=Pyramid        (NEW)
```

The value is the enum member name as a string. The worker parses it with `Enum.TryParse<FamousBuilding>`. If parsing fails, the value is ignored and auto-detection proceeds.

---

## 7. Sprint Sizing & Phasing

### 7.1 This Is a Two-Sprint Feature

**Sprint A: API + Enum + Infrastructure (Size M, ~3 days)**
- `FamousBuilding` enum with XML docs
- `FamousBuildingAnnotation` class
- `AsMinecraftFamousBuilding<T>()` extension method
- `WithMonitoredResource` modifications to read and propagate the annotation
- `ResourceInfo` updated with `FamousBuilding` property
- `AspireResourceMonitor` updated to read the env var
- `StructureBuilder` updated with override branch
- `IFamousBuildingModel` interface + `BuildingHelpers` class
- `FamousBuildingBuilder` dispatcher
- 2–3 starter building models (Pyramid, Castle Tower, Lighthouse) as proof of concept
- Unit tests for enum propagation, annotation flow, override logic
- User documentation for the API

**Sprint B: Building Models (Size L, ~5-7 days)**
- Remaining 12 building models implemented and tested
- RCON command count assertions per model
- BlueMap visual verification
- README and user-docs updates
- Performance profiling with mixed village (auto-detected + famous buildings)

### 7.2 Dependencies on Sprint 5

- **Grand Village layout (Sprint 5 Phase 1)** must land first. Famous buildings assume 15×15 grid slots.
- **RCON burst mode (Sprint 5 Phase 2)** is recommended. Famous buildings at ~100-180 commands per building will strain the 10 cmd/sec rate limit. A 6-building village with all famous buildings: ~600-900 commands = 60-90 seconds at 10/sec, vs. 15-22 seconds at 40/sec burst.
- **Building helpers (Sprint 5)** — the `BuildingHelpers` geometry primitives are useful for both Grand Village buildings and famous buildings. Ideally extracted during Sprint 5 for reuse.

### 7.3 Recommended Sprint Schedule

```
Sprint 5: Grand Village (current)
  → Delivers: 15×15 grid, burst mode, building helpers
  
Sprint 6 (Sprint A): Famous Buildings API + 3 starter buildings
  → Delivers: enum, extension method, infrastructure, Pyramid/Castle/Lighthouse
  → This sprint also includes Trace River (OTLP) if scope allows, else standalone

Sprint 7 (Sprint B): Remaining famous building models
  → Delivers: 12 more building models, full test coverage, docs
  → Can overlap with other features since building models are independent
```

---

## 8. Risk Analysis

### High Risk

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| **Building models look bad at block scale** | Medium | High — disappointing visuals undermine the feature | Prototype 3 buildings in Minecraft Creative mode before writing code. Take screenshots. Get Jeff's approval on visual quality before committing to all 15. |
| **Scope creep to custom buildings** | High | Medium — users will immediately ask for custom block-by-block definitions | The enum is the scope. Custom building definitions (JSON/schematic import) is a separate feature, deliberately not in this design. Push back firmly. |

### Medium Risk

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| **RCON budget explosion** | Medium | Medium — complex buildings (Taj Mahal, Eiffel Tower) at 150+ commands each | Cap at 200 commands per building. If a model exceeds this, simplify the design. Use `/fill` aggressively. |
| **Ordering sensitivity** | Low | Medium — `AsMinecraftFamousBuilding` called after `WithMonitoredResource` | Solved by deferred env var callback (Section 2.5). Must be implemented correctly. |
| **Grid overflow for buildings > 15×15** | Low | High — buildings outside grid slot overlap neighbors | Hard-cap all building models at 15×15 footprint. `IFamousBuildingModel.Width/Depth` enforced in `FamousBuildingBuilder`. |

### Low Risk

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| **Enum evolution** | Low | Low — adding new buildings is additive | New enum values are non-breaking. Removing values is breaking but unlikely. |
| **API naming bikeshed** | Medium | Low | Jeff specified the syntax. Use it exactly. |

---

## Appendix A: Full API Usage Example

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure resources
var redis = builder.AddRedis("cache");
var postgres = builder.AddPostgres("db").AddDatabase("mydb");
var api = builder.AddProject<Projects.WebApi>("api");
var frontend = builder.AddProject<Projects.Frontend>("frontend");

// Assign famous buildings to resources
redis.AsMinecraftFamousBuilding(FamousBuilding.Colosseum);
postgres.AsMinecraftFamousBuilding(FamousBuilding.Pyramid);
api.AsMinecraftFamousBuilding(FamousBuilding.BigBenClockTower);
// frontend gets auto-detected structure (no famous building assigned)

// Minecraft server with world display
var minecraft = builder.AddMinecraftServer("mc")
    .WithBlueMap()
    .WithAspireWorldDisplay<Projects.Worker>()
    .WithGrandVillage()          // Required for famous buildings at full size
    .WithMonitoredResource(redis)
    .WithMonitoredResource(postgres)
    .WithMonitoredResource(api)
    .WithMonitoredResource(frontend)
    .WithAllFeatures();

builder.Build().Run();
```

**Result in Minecraft:**
- `cache` → Roman Colosseum (sandstone amphitheater)
- `db` → Great Pyramid (sandstone pyramid with gold capstone)
- `api` → Big Ben Clock Tower (stone brick tower with clock face)
- `frontend` → Auto-detected Watchtower (standard Sprint 5 building)

## Appendix B: File Manifest

| File | Package | Change Type |
|------|---------|-------------|
| `src/Aspire.Hosting.Minecraft/FamousBuilding.cs` | Hosting lib | **NEW** — enum |
| `src/Aspire.Hosting.Minecraft/FamousBuildingAnnotation.cs` | Hosting lib | **NEW** — annotation |
| `src/Aspire.Hosting.Minecraft/FamousBuildingExtensions.cs` | Hosting lib | **NEW** — extension method |
| `src/Aspire.Hosting.Minecraft/MinecraftServerBuilderExtensions.cs` | Hosting lib | **MODIFIED** — read annotation in `WithMonitoredResource` |
| `src/Aspire.Hosting.Minecraft.Worker/Services/ResourceInfo.cs` | Worker | **MODIFIED** — add `FamousBuilding` property |
| `src/Aspire.Hosting.Minecraft.Worker/Services/AspireResourceMonitor.cs` | Worker | **MODIFIED** — read `_FAMOUS_BUILDING` env var |
| `src/Aspire.Hosting.Minecraft.Worker/Services/StructureBuilder.cs` | Worker | **MODIFIED** — add override branch |
| `src/Aspire.Hosting.Minecraft.Worker/Services/FamousBuildingBuilder.cs` | Worker | **NEW** — dispatcher |
| `src/Aspire.Hosting.Minecraft.Worker/Services/BuildingHelpers.cs` | Worker | **NEW** — geometry primitives |
| `src/Aspire.Hosting.Minecraft.Worker/Services/IFamousBuildingModel.cs` | Worker | **NEW** — interface |
| `src/Aspire.Hosting.Minecraft.Worker/Services/FamousBuildingModels/*.cs` | Worker | **NEW** — 15 model files |
