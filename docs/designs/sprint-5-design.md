# Sprint 5 Technical Design — "Grand Village"

> **Author:** Rhodey (Lead)
> **Date:** 2026-02-12
> **Requested by:** Jeffrey T. Fritz
> **Status:** 📐 Design — ready for implementation

---

## Table of Contents

1. [Vision Statement](#1-vision-statement)
2. [VillageLayout Changes](#2-villagelayout-changes)
3. [Building Redesigns](#3-building-redesigns)
4. [Minecart Rail Network Design](#4-minecart-rail-network-design)
5. [Extension Method API](#5-extension-method-api)
6. [RCON Performance Budget](#6-rcon-performance-budget)
7. [Phased Implementation Plan](#7-phased-implementation-plan)
8. [Migration Considerations](#8-migration-considerations)
9. [Risk Analysis](#9-risk-analysis)

---

## 1. Vision Statement

Sprint 5 transforms the resource village from a functional miniature model into a **living town players want to explore**. A player spawning into the world should see imposing towers on the horizon, walk through a village gate into a cobblestone plaza with buildings large enough to enter, discover crafting stations and resource information inside each structure, and follow minecart rails between connected services to understand how the application is wired together.

**The three pillars:**

1. **Walk-in Buildings** — Structures expand from 7×7 to 15×15 with furnished interiors
2. **Ornate Project Towers** — Multi-story watchtowers with staircases, battlements, and per-floor information displays
3. **Minecart Rail Network** — Powered rail connections between dependent resources, replacing/augmenting redstone wire

The experience shifts from "looking at buildings" to "exploring a town."

---

## 2. VillageLayout Changes

### Current Constants

```csharp
BaseX = 10, BaseZ = 0, BaseY = -60
Spacing = 12, Columns = 2, StructureSize = 7
DashboardX = BaseX - 15 = -5
```

### New Constants

```csharp
// Grid constants
public const int BaseX = 10;
public const int BaseZ = 0;
public const int BaseY = -60;        // unchanged
public const int Spacing = 24;       // was 12 — doubled for larger buildings + rail corridors
public const int Columns = 2;        // unchanged
public const int StructureSize = 15; // was 7 — more than doubled

// Derived
public const int DashboardX = BaseX - 25; // was -5, now -15 — moved further west
public const int FenceClearance = 6;      // was 4 — more breathing room

// Rail corridor
public const int RailCorridorWidth = 3;   // 3-block-wide rail channel between buildings
```

### Why These Numbers

| Metric | Old | New | Rationale |
|--------|-----|-----|-----------|
| StructureSize | 7 | 15 | 13×13 usable interior (walls eat 1 block each side). Player can walk comfortably. |
| Spacing | 12 | 24 | 15 (building) + 6 (gap for rails + walking) + 3 (rail corridor) = 24 |
| FenceClearance | 4 | 6 | Larger buildings need wider perimeter for visual breathing room |
| DashboardX | -5 | -15 | Further west so dashboard doesn't crowd enlarged buildings |

### Grid Coordinate Examples (New Layout)

| Index | Col | Row | Origin (X, Z) | Center (X, Z) |
|-------|-----|-----|----------------|----------------|
| 0 | 0 | 0 | (10, 0) | (17, 7) |
| 1 | 1 | 0 | (34, 0) | (41, 7) |
| 2 | 0 | 1 | (10, 24) | (17, 31) |
| 3 | 1 | 1 | (34, 24) | (41, 31) |
| 4 | 0 | 2 | (10, 48) | (17, 55) |
| 5 | 1 | 2 | (34, 48) | (41, 55) |

### Village Bounds (6 resources)

```
maxX = 10 + (1 × 24) + 14 = 48
maxZ = 0 + (2 × 24) + 14 = 62
Fence: (4, -6) to (54, 68)
Total footprint: ~50 × 74 blocks
```

### Forceload Area

Current: `forceload add -10 -10 80 80` (90×90)
New: `forceload add -20 -10 100 150` (120×160)

With 6 resources the village extends to ~(54, 68). Forceload needs headroom for dashboard, rail overshoot, and future growth.

### World Border

Current `MAX_WORLD_SIZE = 256` supports the new layout for up to ~8 resources (4 rows × 2 cols = Z extent ~86 blocks + fence = ~98). For 10+ resources, consider bumping to `MAX_WORLD_SIZE = 512`. This is a configuration change in `MinecraftServerBuilderExtensions.cs`.

### Maximum Resource Scaling

```
MAX_WORLD_SIZE = 256: max ~8 resources (4 rows)
MAX_WORLD_SIZE = 512: max ~20 resources (10 rows)
MAX_WORLD_SIZE = 1024: max ~42 resources (21 rows)
```

**Recommendation:** Default to `MAX_WORLD_SIZE = 512` for Sprint 5 (covers typical Aspire apps with 3-10 resources comfortably).

### Updated Helper Methods

```csharp
public static (int x, int y, int z) GetStructureCenter(int index)
{
    var (ox, oy, oz) = GetStructureOrigin(index);
    return (ox + 7, oy, oz + 7); // was ox+3, oz+3 — center of 15×15
}

public static (int x, int y, int z) GetRailEntrance(int index)
{
    var (ox, oy, oz) = GetStructureOrigin(index);
    return (ox + 7, SurfaceY + 1, oz - 1); // centered in front of building, one block south
}
```

---

## 3. Building Redesigns

All buildings expand from 7×7 footprint to **15×15 footprint**. Interior usable space goes from ~5×5 (barely visible) to **13×13** (a real room).

### 3.1 Watchtower (Project Resources) — "Grand Tower"

**Dimensions:** 15×15 base, 20 blocks tall (was 10), 3 interior floors

**Exterior:**
- Stone brick walls with polished andesite corner buttresses (2×2 corner pillars rising full height)
- Arrow slit windows (1-wide glass panes) at regular intervals on each floor
- Battlements at rooftop: alternating stone brick and stone brick stairs (crenellation)
- Language-colored wool band at each floor boundary (floors 1→2, 2→3, roof)
- Standing banner array on roof — 4 banners, one at each corner post
- Iron door entrance (2 wide, 3 tall) with stone brick archway

**Interior:**
- **Ground floor (y+1 to y+6):** Entrance hall with oak staircase (spiral around wall interior), crafting table, resource name sign, 4 torches
- **Second floor (y+7 to y+12):** Open room with enchanting table (centered), bookshelves lining walls, sign displaying endpoint URLs
- **Third floor (y+13 to y+18):** Observation deck with glass pane windows on all sides, sign with health history, lectern with written book (future: actual data)
- **Staircase:** Oak stairs spiraling along the inner north wall, connecting all 3 floors. Each flight is 6 blocks of stairs + 1 landing.

**RCON command estimate:** ~85-100 commands
- Foundation: 1 `/fill`
- Walls (3 sections × 4 sides): ~12 `/fill`
- Corner buttresses (4): 4 `/fill`
- Interior air clear (3 floors): 3 `/fill`
- Staircases (18 stairs + 3 landings): ~21 `/setblock`
- Windows (12 arrow slits): 12 `/setblock`
- Battlements (4 sides): 4 `/fill` + 4 `/fill` for stairs
- Interior furnishing (tables, torches, signs): ~15 `/setblock`
- Roof, banners, health lamp: ~10 commands

### 3.2 Warehouse (Container Resources) — "Grand Warehouse"

**Dimensions:** 15×15 base, 8 blocks tall (was 5)

**Exterior:**
- Iron block frame with deepslate brick infill panels
- Large 5-wide × 4-tall cargo bay entrance (front face)
- Purple stained glass clerestory windows (strip near roofline on all sides)
- Loading dock: stone brick platform extending 2 blocks from entrance with fence railings

**Interior:**
- **Main floor:** 4×2 barrel grid (8 barrels), 2 chest rows, item frames on walls
- Interior columns (iron blocks) at quarter-points for structural detail
- Lanterns hanging from ceiling on chains
- Resource name sign and container image sign

**RCON command estimate:** ~45-55 commands
- Foundation + walls: 3 `/fill`
- Cargo door: 1 `/fill`
- Windows: 4 `/fill`
- Loading dock: 3 `/fill`
- Interior (barrels, chests, columns, lanterns): ~25 `/setblock`
- Signs + frames: ~8 `/setblock`

### 3.3 Workshop (Executable Resources) — "Grand Workshop"

**Dimensions:** 15×15 base, 10 blocks tall (was 6), peaked roof

**Exterior:**
- Oak plank walls with spruce log frame (corner posts + horizontal beams)
- A-frame peaked roof with spruce stair shingles
- Large chimney (2×2 cobblestone column) with campfire and smoke particles
- Cyan stained glass windows (2 per side), flower boxes under front windows

**Interior:**
- **Workshop floor:** Crafting table, smithing table, stonecutter, anvil, grindstone
- **Loft level (y+6):** Half-floor accessible by ladder, storage barrels, bookshelf
- Furnace against back wall, brewing stand in corner
- Lanterns on walls, torches in corners

**RCON command estimate:** ~55-65 commands
- Foundation + walls + frame: ~8 `/fill`
- Roof (A-frame, 4 layers): ~8 `/fill`
- Chimney: 2 `/fill`
- Windows + flower boxes: ~12 `/setblock`
- Interior furnishing: ~20 `/setblock`
- Loft + ladder: ~5 commands

### 3.4 Cylinder (Database Resources) — "Grand Silo"

**Dimensions:** 15×15 base (radius 7 circle), 12 blocks tall (was 8)

**Exterior:**
- Polished deepslate and smooth stone circular walls (7-block radius approximation)
- Copper accent band at mid-height and top
- Domed roof with deepslate slab layers
- Single iron door entrance (1 wide, 2 tall)

**Interior:**
- **Lower floor (y+1 to y+5):** Ring of iron blocks around perimeter (server racks), copper block center island, 4 redstone lamps
- **Upper floor (y+6 to y+10):** Accessible via ladder, bookshelf ring, enchanting table, signs with connection string info
- **Central column:** Copper pillar from floor to ceiling (data spindle aesthetic)

**RCON command estimate:** ~120-140 commands
- Circular floor (7 rows of `/fill` per layer): ~7 `/fill`
- Walls (10 layers × 8 segments each): ~80 commands (the expensive one — circles don't `fill` efficiently)
- Interior air: ~14 `/fill`
- Dome (3 layers): ~21 `/fill`
- Interior furnishing: ~15 `/setblock`

### 3.5 AzureThemed (Azure Resources) — "Grand Azure Pavilion"

**Dimensions:** 15×15 base, 8 blocks tall (was 5)

**Exterior:**
- Light blue concrete walls with blue concrete pilaster strips at corners and midpoints
- Flat roof with light blue stained glass skylight (3×3 center)
- Azure banners on all four corners of rooftop
- Blue concrete trim band at wall top

**Interior:**
- Similar to Warehouse interior but with azure color accents
- Light blue carpet floor, blue stained glass internal windows
- Brewing stand and cauldron (cloud services aesthetic)

**RCON command estimate:** ~50-60 commands

### 3.6 Cottage (Unknown/Other Resources) — "Grand Cottage"

**Dimensions:** 15×15 base, 8 blocks tall (was 5)

**Exterior:**
- Cobblestone lower walls, oak plank upper walls
- Language-colored wool trim band at roof level
- Cobblestone slab pitched roof
- Flower pots and window boxes on front face

**Interior:**
- Bed, crafting table, bookshelf, furnace, 2 chests
- Potted flowers on windowsills
- 4 torches for lighting

**RCON command estimate:** ~40-50 commands

---

## 4. Minecart Rail Network Design

### Overview

The rail network connects resources with dependency relationships using powered minecart tracks. It runs alongside (and eventually replaces) the existing redstone wire connections from `RedstoneDependencyService`.

### Rail Routing Strategy

Rails follow the same **L-shaped path** as existing redstone wires (X-first, then Z), but offset by 1 block east to avoid overlapping the redstone wire. This allows both systems to coexist during migration.

```
Rail Y-level: SurfaceY + 1 (one block above ground, on the cobblestone path surface)
Redstone Y-level: SurfaceY (ground level — existing behavior)
Rail offset: +1 block in X from redstone wire path
```

### Rail Types and Placement Rules

| Block | Placement Rule | Purpose |
|-------|---------------|---------|
| `minecraft:powered_rail` | Every 8th block | Keeps minecarts moving at full speed |
| `minecraft:rail` | All other positions | Standard track |
| `minecraft:redstone_torch` | Under every powered rail | Powers the rail segment |
| `minecraft:detector_rail` | At building entrances | Triggers signals when minecart passes |

### Powered Rail Spacing

Minecraft powered rails boost minecarts for ~8 blocks on flat ground. Pattern:

```
[powered] [rail] [rail] [rail] [rail] [rail] [rail] [rail] [powered] ...
    ^                                                            ^
    redstone_torch underneath                  redstone_torch underneath
```

### Station Design (Per Building)

Each building entrance gets a small rail station:

```
Station footprint: 3×5 blocks (width × depth), centered on building entrance

Layout (looking south, Z increases):
  Z-2: [stone_brick] [detector_rail] [stone_brick]
  Z-1: [stone_brick] [powered_rail]  [stone_brick]   ← arrival/departure
  Z+0: [building entrance wall]
  
  Side fences on stone_brick blocks for railing effect
```

Station is placed at `GetRailEntrance(index)`, offset 1-2 blocks south of the building front wall.

### L-Shaped Rail Path Calculation

Reuses the same algorithm as `RedstoneDependencyService.CalculateWireSegment()` but:
1. Y-level is `SurfaceY + 1` (on top of cobblestone path, not inside it)
2. Powered rail every 8 blocks instead of repeater every 15
3. Redstone torch placed at `(x, SurfaceY, z)` — one block below each powered rail
4. Detector rail at each endpoint (station entrance)

```csharp
private static RailSegment CalculateRailPath(int parentIndex, int childIndex)
{
    var (px, py, pz) = VillageLayout.GetRailEntrance(parentIndex);
    var (cx, cy, cz) = VillageLayout.GetRailEntrance(childIndex);
    var railY = VillageLayout.SurfaceY + 1;
    
    // L-shaped: walk X first, then Z (same as redstone)
    var positions = new List<(int x, int y, int z, RailType type)>();
    var blockCount = 0;
    
    // Phase 1: X-axis
    var dx = px < cx ? 1 : -1;
    for (var x = px; x != cx; x += dx)
    {
        var type = (blockCount % 8 == 0) ? RailType.Powered : RailType.Normal;
        positions.Add((x, railY, pz, type));
        blockCount++;
    }
    
    // Phase 2: Z-axis
    var dz = pz < cz ? 1 : -1;
    for (var z = pz; z != cz; z += dz)
    {
        var type = (blockCount % 8 == 0) ? RailType.Powered : RailType.Normal;
        positions.Add((cx, railY, z, type));
        blockCount++;
    }
    
    // Endpoint detector
    positions.Add((cx, railY, cz, RailType.Detector));
    
    return new RailSegment(positions);
}
```

### Minecart Spawning

After rail construction, spawn one `chest_minecart` per dependency connection:

```
/summon minecraft:chest_minecart <startX> <railY+0.5> <startZ>
```

Chest minecarts will ride the powered rails automatically. They loop if the track forms a circuit; otherwise they bounce back and forth between endpoints (powered rails are bidirectional).

### Health-Reactive Rails

Mirror the `RedstoneDependencyService` health behavior:
- **Unhealthy parent:** Remove powered rails on that connection (minecart stops)
- **Recovered:** Restore powered rails (minecart resumes)

This reuses the same `_connectionState` tracking pattern.

### RCON Command Estimate (Rail Network)

Per connection (average 30-block path):
- 30 rail blocks: 30 `/setblock`
- ~4 powered rails: 4 `/setblock` + 4 redstone torch `/setblock` = 8
- 2 detector rails: 2 `/setblock`
- 1 minecart spawn: 1 `/summon`
- 2 stations: ~10 `/setblock` each = 20

**Total per connection: ~61 commands**

For a 6-resource app with 4 dependency connections: ~244 commands.

---

## 5. Extension Method API

### New Extension Methods

```csharp
/// <summary>
/// Enables the Grand Village layout with enlarged, walkable buildings.
/// Replaces the standard 7×7 village layout with 15×15 structures
/// that have furnished interiors, proper lighting, and multiple floors.
/// </summary>
public static IResourceBuilder<MinecraftServerResource> WithGrandVillage(
    this IResourceBuilder<MinecraftServerResource> builder)

/// <summary>
/// Enables the minecart rail network connecting dependent resources.
/// Powered rails with automated chest minecarts run between buildings
/// that have dependency relationships. Complementary to redstone wiring.
/// </summary>
public static IResourceBuilder<MinecraftServerResource> WithMinecartRails(
    this IResourceBuilder<MinecraftServerResource> builder)
```

### Env Var Pattern

Following established convention:
- `ASPIRE_FEATURE_GRAND_VILLAGE` → `WithGrandVillage()`
- `ASPIRE_FEATURE_MINECART_RAILS` → `WithMinecartRails()`

### Interaction with Existing Features

- `WithGrandVillage()` replaces the standard `StructureBuilder` building methods with enlarged versions. It does NOT change `WithRedstoneDependencyGraph()` or `WithRedstoneDashboard()`.
- `WithMinecartRails()` works alongside `WithRedstoneDependencyGraph()`. Both can be active simultaneously (rails offset by 1 block from redstone wire).
- `WithAllFeatures()` should include both new methods.

### Service Registration

```csharp
// In Program.cs
if (Environment.GetEnvironmentVariable("ASPIRE_FEATURE_GRAND_VILLAGE") == "true")
{
    // GrandVillage mode sets VillageLayout constants at startup
    VillageLayout.ConfigureGrandLayout(); // sets Spacing=24, StructureSize=15
}

if (Environment.GetEnvironmentVariable("ASPIRE_FEATURE_MINECART_RAILS") == "true")
{
    builder.Services.AddSingleton<MinecartRailService>();
}
```

### VillageLayout Configuration Approach

Rather than changing compile-time constants, introduce a runtime configuration method:

```csharp
internal static class VillageLayout
{
    // Default values (backward compatible)
    public static int Spacing { get; private set; } = 12;
    public static int StructureSize { get; private set; } = 7;
    public static int FenceClearance { get; private set; } = 4;
    
    // Fixed constants (don't change between modes)
    public const int BaseX = 10;
    public const int BaseZ = 0;
    public const int BaseY = -60;
    public const int Columns = 2;
    
    /// <summary>
    /// Switches to Grand Village layout with larger structures and wider spacing.
    /// Must be called before any structure placement.
    /// </summary>
    public static void ConfigureGrandLayout()
    {
        Spacing = 24;
        StructureSize = 15;
        FenceClearance = 6;
    }
}
```

**This preserves backward compatibility.** Without `WithGrandVillage()`, the layout is identical to Sprint 4.

---

## 6. RCON Performance Budget

### Per-Building Command Counts

| Structure | Sprint 4 (7×7) | Sprint 5 (15×15) | Multiplier |
|-----------|----------------|-------------------|------------|
| Watchtower | ~20 | ~95 | 4.75× |
| Warehouse | ~12 | ~50 | 4.2× |
| Workshop | ~18 | ~60 | 3.3× |
| Cylinder | ~88 | ~130 | 1.5× |
| AzureThemed | ~14 | ~55 | 3.9× |
| Cottage | ~15 | ~45 | 3.0× |

### Village Build Time Estimates

At 10 commands/sec (current rate limiter):

| Scenario | Buildings | Rail Connections | Total Commands | Build Time |
|----------|-----------|-----------------|----------------|------------|
| 4 resources, no rails | 4 | 0 | ~280 | ~28 sec |
| 4 resources + rails | 4 | 3 | ~463 | ~46 sec |
| 6 resources + rails | 6 | 4 | ~604 | ~60 sec |
| 8 resources + rails | 8 | 6 | ~826 | ~83 sec |
| 10 resources + rails | 10 | 8 | ~1048 | ~105 sec |

### Performance Optimization Strategies

1. **Maximize `/fill` usage.** Every `/fill` replaces N individual `/setblock` calls. A 15-block wall row is 1 command, not 15. The enlarged buildings actually benefit MORE from `/fill` than small ones.

2. **Batch rail construction.** Place all rails of one type in a single pass. Use `/fill` for straight rail segments where possible:
   ```
   /fill x1 y z x2 y z minecraft:rail
   ```
   Then overwrite powered rail positions. This reduces a 30-block rail path from 30 to ~6 commands.

3. **Increase rate limit during initial build.** Temporarily set `MaxCommandsPerSecond = 40` during village construction, then drop to 10 for steady-state health updates. This requires a `RconService.SetBurstMode(bool)` method.

4. **Parallel structure construction.** If two structures are in different chunks, they can theoretically be built simultaneously. This requires the RCON service to track chunk boundaries and parallelize.

**Recommended approach for Sprint 5:** Strategy #1 (maximize fill) + Strategy #2 (batch rails) + Strategy #3 (burst mode). These alone should bring a 6-resource village with rails to under 30 seconds.

### RCON Burst Mode Design

```csharp
internal class RconService
{
    private int _maxCommandsPerSecond = 10;
    
    /// <summary>
    /// Temporarily increases command throughput for initial construction.
    /// Automatically reverts after the specified duration.
    /// </summary>
    public IDisposable EnterBurstMode(int commandsPerSecond = 40, TimeSpan? duration = null)
    {
        var original = _maxCommandsPerSecond;
        _maxCommandsPerSecond = commandsPerSecond;
        // Returns IDisposable that restores original rate on dispose
    }
}
```

---

## 7. Phased Implementation Plan

### Phase 1: Layout Foundation (1 developer, ~2 days)
**Squad: Shuri (hosting library)**

- [ ] Convert `VillageLayout` constants to configurable properties
- [ ] Add `ConfigureGrandLayout()` method
- [ ] Add `WithGrandVillage()` extension method
- [ ] Add `WithMinecartRails()` extension method
- [ ] Update `GetStructureCenter()`, add `GetRailEntrance()`
- [ ] Update `GetVillageBounds()` and `GetFencePerimeter()` for new sizes
- [ ] Update forceload command to cover new area
- [ ] Bump `MAX_WORLD_SIZE` to 512

**No visual changes yet.** This phase makes the grid bigger and wires up the feature flags.

### Phase 2: Grand Buildings (2 developers, ~3-4 days)
**Squad: Rocket (Minecraft worker)**

Can be parallelized — each building is independent:

- [ ] **Track A:** Grand Watchtower + Grand Workshop (complex builds with multi-floor, stairs)
- [ ] **Track B:** Grand Warehouse + Grand Cottage + Grand Azure Pavilion (simpler enlargements)
- [ ] **Track C:** Grand Silo (cylinder geometry at 15×15 — mathematically intensive)
- [ ] Update `BuildFencePerimeterAsync()` for new dimensions
- [ ] Update `BuildPathsAsync()` for new dimensions
- [ ] Add RCON burst mode for initial construction

### Phase 3: Rail Network (1 developer, ~2-3 days)
**Squad: Rocket (Minecraft worker)**

- [ ] `MinecartRailService` — new service following `RedstoneDependencyService` pattern
- [ ] Rail path calculation (L-shaped, powered every 8 blocks)
- [ ] Station construction at building entrances
- [ ] Minecart spawning
- [ ] Health-reactive rail state (break/restore powered rails)
- [ ] Integration with `MinecraftWorldWorker` update loop

### Phase 4: Testing & Documentation (1 developer, ~2 days)
**Squad: Nebula (testing) + Rhodey (docs)**

- [ ] Unit tests for new `VillageLayout` calculations
- [ ] Unit tests for rail path calculations
- [ ] RCON command count assertions per structure type
- [ ] Integration test: verify building placement coordinates
- [ ] Update `docs/minecraft-constraints.md` for new dimensions
- [ ] Update `docs/architecture-diagram.md` for new layout
- [ ] User docs for `WithGrandVillage()` and `WithMinecartRails()`
- [ ] README update with Sprint 5 features

### Phase 5: Polish & Review (1 developer, ~1 day)
**Squad: Rhodey (lead)**

- [ ] API surface review — verify no internal leakage
- [ ] RCON performance profiling with 8-resource village
- [ ] BlueMap visual verification
- [ ] Demo script walkthrough
- [ ] Cut v0.4.0 release candidate

### Parallel Execution Map

```
Week 1:  [Phase 1: Layout]  →  [Phase 2A: Watchtower/Workshop]
                                [Phase 2B: Warehouse/Cottage/Azure]
                                [Phase 2C: Cylinder]

Week 2:  [Phase 3: Rails]   →  [Phase 4: Tests/Docs]
                                [Phase 5: Polish]
```

Phases 2A, 2B, and 2C can run concurrently. Phase 3 depends on Phase 1 (needs new layout). Phase 4 can start during Phase 3.

---

## 8. Migration Considerations

### Breaking Changes

| Change | Breaking? | Mitigation |
|--------|-----------|------------|
| `VillageLayout.Spacing` becomes property (was const) | ⚠️ Potentially | Only if external code references the const. Internal classes only — **not breaking for NuGet consumers**. |
| `VillageLayout.StructureSize` becomes property | ⚠️ Potentially | Same as above — internal only. |
| `MAX_WORLD_SIZE` increases from 256 to 512 | Additive | More world to load. Slightly higher memory use (~10 MB). |
| Forceload area increases | Additive | More chunks kept loaded. ~4× more area. |
| New env vars (`ASPIRE_FEATURE_GRAND_VILLAGE`, `ASPIRE_FEATURE_MINECART_RAILS`) | Additive | Off by default. No behavior change unless opted in. |

### Backward Compatibility

**Without `WithGrandVillage()`**: Village is identical to Sprint 4. 7×7 buildings, 12-block spacing. Zero regression.

**With `WithGrandVillage()`**: New layout activates. All existing features (beacons, particles, boss bars, holograms, etc.) use `VillageLayout.GetStructureOrigin()` and `GetStructureCenter()` — they adapt automatically because the coordinate methods change.

**Services that need manual updates for Grand mode:**
- `BeaconTowerService` — beacon placement height needs to account for taller buildings
- `ParticleEffectService` — `GetAboveStructure()` height offset may need increase
- `HologramManager` — hologram Y positions relative to structure height
- `RedstoneDependencyService` — wire paths are unaffected (use same origin points), but wire length increases with wider spacing
- `RedstoneDashboardService` — dashboard position unchanged (uses `DashboardX`)
- `ServiceSwitchService` — switch position relative to building entrance may shift

### Version Strategy

Sprint 5 ships as **v0.4.0** (minor bump — additive features, no public API breaks).

---

## 9. Risk Analysis

### High Risk

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| **RCON timeout during large builds** | Medium | High — village construction fails mid-build | Burst mode + `/fill` optimization. Add retry logic for failed commands. Implement construction checkpointing (track which structures completed). |
| **Chunk loading issues with larger forceload** | Medium | Medium — structures built in unloaded chunks disappear | Expand forceload command before any construction. Verify with `execute if loaded`. Increase `VIEW_DISTANCE` to 8 if needed. |
| **Cylinder at radius 7 — RCON explosion** | High | Medium — 130+ commands for one building | Pre-calculate circle coordinates. Use maximum `/fill` coverage per row. Consider "cylinder approximation" at 15×15 (octagon shape) instead of true circle. |

### Medium Risk

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| **Minecart entity accumulation** | Medium | Low-Medium — too many minecarts = lag | Cap at 1 minecart per connection. Despawn on circuit break. Add `/kill @e[type=chest_minecart]` cleanup on rebuild. |
| **Rail intersections/collisions** | Medium | Medium — L-shaped paths may cross | Offset each connection's rail path by 1 block in Z. Or use 3D routing (rails at different Y-levels for crossing paths). |
| **BlueMap render time** | Low | Low — longer initial render, but one-time cost | Trigger BlueMap update only after full construction completes (existing pattern). |
| **Interior lighting failures** | Low | Low — dark interiors if torches placed wrong | Use `lantern` blocks (always lit) instead of torches. Place glowstone in floors for ambient light. |

### Low Risk

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| **Player pathfinding in larger village** | Low | Low — harder to find buildings | Place signs at village entrance with building directory. Maintain cobblestone path network between all buildings. |
| **Staircase collision with structure walls** | Low | Low — visual glitch | Test staircase geometry in creative mode before coding. Document exact block coordinates. |

### Risk Not Taken

- **Adaptive building size based on resource count.** Would add complexity for minimal gain. Fixed 15×15 is simpler and covers all reasonable cases.
- **3D rail routing.** Over-engineered for v1. L-shaped flat paths with Z-offset for crossings is sufficient.
- **Automatic interior content from live data.** Signs with endpoint URLs require OTLP trace data — punted to Sprint 6 (Trace River epic).

---

## Appendix: RCON Command Reference

### Key Minecraft Commands Used

```bash
# Fill a volume with one block type
/fill x1 y1 z1 x2 y2 z2 <block>

# Fill hollow (walls only, air inside)
/fill x1 y1 z1 x2 y2 z2 <block> hollow

# Place single block
/setblock x y z <block>

# Summon entity
/summon minecraft:chest_minecart x y z

# Force-load chunks
/forceload add x1 z1 x2 z2

# Kill entities
/kill @e[type=chest_minecart,x=X,y=Y,z=Z,distance=..5]

# Rail block states
minecraft:rail[shape=north_south]
minecraft:powered_rail[powered=true,shape=north_south]
minecraft:detector_rail[powered=false,shape=north_south]
```

### Interior Furniture Blocks

```bash
# Functional blocks
minecraft:crafting_table
minecraft:enchanting_table
minecraft:smithing_table
minecraft:stonecutter
minecraft:anvil
minecraft:grindstone
minecraft:brewing_stand
minecraft:furnace[facing=north,lit=false]
minecraft:lectern[facing=south,has_book=false]
minecraft:barrel[facing=up]
minecraft:chest[facing=south]

# Lighting
minecraft:lantern[hanging=true]
minecraft:lantern[hanging=false]
minecraft:soul_lantern[hanging=true]
minecraft:torch
minecraft:wall_torch[facing=south]

# Decorative
minecraft:flower_pot
minecraft:bookshelf
minecraft:oak_sign[rotation=8]
minecraft:item_frame[facing=south]
```
