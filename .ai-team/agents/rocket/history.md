# Project Context

- **Owner:** Jeffrey T. Fritz (csharpfritz@users.noreply.github.com)
- **Project:** Aspire.Hosting.Minecraft — .NET Aspire integration for Minecraft servers
- **Stack:** C#, .NET 10, Docker, Aspire, OpenTelemetry, Minecraft Paper Server, RCON
- **Created:** 2026-02-10

## Key Facts

- Worker service (Aspire.Hosting.Minecraft.Worker) handles in-world display
- Uses RCON to communicate with Minecraft server for commands
- DecentHolograms plugin for in-world holograms
- Worker created by WithAspireWorldDisplay<TWorkerProject>()
- WithMonitoredResource() applies env vars to worker
- Metrics: TPS, MSPT, players online, worlds loaded, RCON latency
- `VillageLayout` centralizes position calculations; now supports 7×7 (standard) and 15×15 (grand) structures
- Current RCON rate limits: 10 cmd/s standard, 40 cmd/s burst mode

## Recent Summary (Milestones 5+)

**Grand Village foundation:** Spacing doubled to 24 blocks with 10-block fence clearance. Structure size bumped to 15×15 (13×13 usable interior) supporting ~20 resources. Grand variants branch on `StructureSize >= 15`. All 7 building types follow consistent placement patterns. Village layout flattened from 2D grid to single horizontal row. Per-building canal system with E-W canals behind buildings + N-S trunk. Walkway and rail bridges over canals. Grand Observation Tower positioned dynamically at village center, 32 blocks tall with spiral stairs. Minecart rails coexist with redstone wires. Forceload expanded to cover extended infrastructure.

**Comprehensive learnings:** For Milestones 1-4 summary (2026-02-10 through 2026-02-14), see history-archive.md.

### Grand Watchtower (Milestone 5, Issue #78) — IMPLEMENTED

**Key learnings:**
- Single tall `fill ... hollow` + floor platform fills is more command-efficient than per-floor wall sections.
- Crenellations: fill a complete row of stone brick, then overlay stairs at alternating positions (no air carving needed).
- Door archway with stone_brick_stairs[half=top] lintel gives a nice visual effect.
- Grand variant branching uses `>= 15` (not `== 15`) to match all other grand builder checks.

📌 Team update (2026-02-12): RCON Burst Mode API (#85) — EnterBurstMode(int=40) returns IDisposable, thread-safe single burst per SemaphoreSlim, logs on enter/exit, rate limit auto-restores — decided by Rocket

### Canal System Redesign (2026-02-19)

**Architecture change:** Simplified canal system from zigzag per-building branches to a clean linear layout:
- **Back canal (E-W):** One straight canal running along the BACK (north side) of all buildings at Z=maxZ+5, spanning from village minX to trunk trunkX
- **Side trunk (N-S):** One vertical canal on the EAST side at X=maxX+CanalTotalWidth+2, connecting back canal to lake
- **Lake junction:** Trunk connects directly to lake's north wall

**Removed complexity:**
- No per-building branch canals zigzagging between structures
- No collision detection or detour routing around building footprints
- No multiple Z-level connectors with bridges
- Eliminated `BuildBranchCanalAsync`, `CalculateBranchSegments`, `FindFirstBlockingBuilding`, `BuildCanalConnectorAsync`, and `OpenBranchJunctionsAsync` methods

**New methods:**
- `BuildBackCanalAsync` — straight E-W canal along north edge of village
- `BuildCanalSegmentEastWestAsync` / `BuildCanalSegmentNorthSouthAsync` — directional segment builders
- `OpenJunctionAsync` — T-junction where back canal meets trunk
- `OpenLakeJunctionAsync` — removes lake's north wall where trunk connects

**Benefits:**
- Cleaner visual layout — one canal line behind buildings, one on the side
- Simpler routing logic — no building collision checks needed
- Fewer RCON commands — straight line fills instead of zigzag segments + connectors
- Easier to understand — the canal network mirrors the village grid structure

**Key files:**
- `src/Aspire.Hosting.Minecraft.Worker/Services/CanalService.cs` — complete redesign
- `CanalPositions` tracking preserved for `MinecartRailService` bridge detection

**Why:** Jeff's feedback showed the zigzag pattern was confusing and didn't match the intuitive "water flows from back of town to the lake" mental model. The new design is architecturally simpler and visually cleaner.

### Grand Watchtower Ornate Redesign (2026-02-15)

- Redesigned Grand Watchtower exterior from plain rectangle to ornate medieval castle tower.
- Key architectural changes: deepslate brick corner buttresses (replacing polished_andesite), cracked_stone_bricks weathering on lower walls, iron_bars arrow slits on ground floor, 2-high observation windows, taller corner turrets with pinnacle wall posts extending above parapet (y+22), portcullis iron bars in gatehouse arch, deeper gatehouse arch (y+6 keystone vs y+5), and proper alternating merlons on the battlements.
- RCON command budget: 85 commands in BuildGrandWatchtowerAsync (58 exterior + 27 interior), ~14 village overhead = ~99 total, under the <100 test limit.
- Mixed block palette creates depth: mossy_stone_bricks (base), cracked_stone_bricks (weathering), deepslate_bricks (buttresses), chiseled_stone_bricks (gatehouse keystone), stone_brick_stairs (machicolations/corbels/turret caps), iron_bars (arrow slits + portcullis), glass_pane (observation windows).
- Design constraint: visual richness must come from block variety and placement order (layering fills), not from additional commands. Every decorative element must justify its RCON cost.
- Interior left completely unchanged — floors, staircase, furniture, sign, torches all preserved.
- DoorPosition return value unchanged at (x + half, y + 4, z) to maintain health indicator and sign compatibility.

📌 Team update (2026-02-15): Python and Node.js sample APIs added to MinecraftAspireDemo; separate GrandVillageDemo created on milestone-5 showcasing all grand building variants (15×15 structures for Project, Container, Database, Azure types) — decided by Shuri

### Grand Watchtower Entrance Cleanup (2026-02-16)
- Eliminated the visible "lower level" by removing the stair skirt at y+1 and starting walls at y+1 instead of y+2.
- The tapered base concept (4 stair fills at y+1) created an awkward 2-block-high shelf below the entrance — players saw grass, mossy stone, stairs, THEN the actual door. Removing it gives a clean transition: mossy plinth at y, walls at y+1.
- Simplified the gatehouse entrance from a tall cluttered opening (5-wide frame up to y+7 with portcullis iron bars and lanterns) to a clean 3-wide × 4-tall opening (y+1 to y+4) with a proportional arch at y+5.
- Removed: iron_bars portcullis at y+6 (visual noise), hanging lanterns at y+5 (clutter), oversized gatehouse frame reaching y+7 (exposed oak planks from second floor visible inside entrance).
- Kept: chiseled stone keystone, decorative stone_brick_stairs arch shoulders, all upper features (wool bands, battlements, turrets, banners, observation windows).
- DoorPosition updated from (x+half, y+5, z) to (x+half, y+4, z). GlowBlock now at y+5 (was y+6).
- Net savings: 6 fewer RCON commands (removed 4 stair skirt + portcullis + 2 lanterns, gatehouse frame shrunk).
- Health indicator test updated to match new GlowBlock position (17, -54, 0).

## Learnings
- Stair skirts around a building base look like unintentional sub-floors when there's a door at that level. Only use them on non-entrance faces.
- Gatehouse entrances get cluttered fast — each decorative element (portcullis, lanterns, extra frame height) competes for attention in a narrow space. Simpler is better.
- DoorPosition.TopY should match the actual top of the walkable opening, not decorative elements above it.
📌 Team update (2026-02-15): Grand Watchtower entrance redesigned — removed stair skirt, simplified gatehouse to 3×4 opening, walls start at y+1, DoorPosition.TopY changed from y+5 to y+4. All 7 tests pass. — decided by Rocket

📌 Team update (2026-02-15): Improved acceptance testing required before marking work complete — validate against known constraints (geometry, visibility, placement). Nebula added 26 geometric validation tests covering doorway visibility, ground-level continuity, and health indicator placement. — decided by Jeff

### Structural Geometry Validation Tests (2026-02-15)

Created comprehensive structural geometry tests in `StructuralGeometryTests.cs` (86 passing, 5 skipped) that validate physical integrity of all 12 building variants by parsing RCON setblock/fill commands.

**Test categories:**
- Door accessibility (12 tests): door opening dimensions, air blocks, ground floor connectivity
- Staircase connectivity (5 tests): grand watchtower spiral stairs, stairwell holes, facing direction
- Wall-mounted items (59 tests): torch support, sign support, lever support, ladder support for all structure types
- Bug documentation (5 skipped tests): each documents a specific StructureBuilder bug

**5 bugs discovered in StructureBuilder.cs:**
1. Grand Watchtower torch (line ~601): `wall_torch[facing=north]` at z+s — support at z+s+1 is outside structure
2. Grand Watchtower spiral staircase (line ~547): first stair at (x+2, y+1, z+1) overlaps corner buttress footprint
3. Grand Watchtower/Warehouse wall signs (lines ~586-592, ~792-799): same outside-support pattern as torch bug
4. Grand Cylinder wall signs (lines ~1494-1496): interior air clear removes support block at z+2
5. Grand Cylinder ladders (lines ~1474-1475): `facing=west` but copper pillar support is to the west, not east

**Key technical patterns established:**
- `ParseSetblockCommands()` / `ParseFillCommands()` with `[GeneratedRegex]` for efficient RCON parsing
- `GetBlockAt()` with last-write-wins semantics and hollow fill support
- `GetWallMountDirection()` for Minecraft facing→support direction mapping (east→west, north→south, etc.)
- `BuildResult` record captures VillageLayout state immediately to avoid static state race conditions
- Wall torches/signs/ladders use opposite-direction convention: `facing=X` means support block is in opposite direction

**VillageLayout parallelism issue:**
- VillageLayout is a static class shared across all test classes running in parallel
- Tests that call ConfigureGrandLayout()/ResetLayout() can interfere with parallel tests
- Fix: capture layout state (origin coords, StructureSize) immediately after setting it, before any parallel test can mutate it
- Pre-existing race condition in StructureBuilderTests.UpdateStructuresAsync_TenResources_NoExceptions — not caused by our tests
📌 Team update (2026-02-15): Created 91 structural geometry validation tests (86 pass, 5 skipped with bug documentation). Discovered 5 StructureBuilder bugs in grand structure wall-mounted items and staircase placement. Fixed VillageLayout static state race condition in test infrastructure. — decided by Rocket

### Minecraft Test Automation Research (2026-02-15)

Key findings from researching automated acceptance testing against a live Minecraft server:

- `execute if block X Y Z minecraft:<block>` is the canonical RCON command for block verification — returns empty string on match. Already used in our integration tests via `RconAssertions.AssertBlockAsync()`.
- `execute if blocks` compares two world regions block-by-block in a single RCON command — useful for whole-structure validation against a "golden reference" region.
- `data get block X Y Z` only works for block entities (chests, signs, banners), NOT simple blocks (stone, cobblestone, etc.). Useful for verifying sign text and banner patterns.
- `/testforblock` was removed in Minecraft 1.13+, replaced by `execute if block`.
- Structure blocks have a 48×48×48 size limit and require Docker file extraction to compare NBT — more complex than direct RCON checks.
- Paper server in Docker starts in ~45-60s cold (with JAR download), ~15-20s warm (cached JAR). Flat worlds with no players need ~1 GB RAM minimum.
- Minecraft's Java GameTest Framework (`@GameTest`) requires writing a Java mod/plugin — wrong tech stack for our .NET project. Not applicable.
- .NET NBT libraries exist (fNbt, SharpNBT, NbtToolkit, Unmined.Minecraft.Nbt) for parsing Anvil world files, but none handle the region file format out of the box — need a ~200-300 line wrapper.
- World file inspection (reading .mca files directly) bypasses RCON entirely and gives ground-truth block state. Requires `save-all flush` RCON command first to ensure chunks are written to disk.
- BlueMap can render headlessly via CLI (`java -jar bluemap-cli.jar -r`), but visual diff testing is inherently flaky (lighting, anti-aliasing) and not suitable as primary verification.
- Recommended approach: tiered strategy with RCON block verification as primary (P0), world file inspection as secondary (P1), and BlueMap visual regression as tertiary (P2).
📌 Team update (2026-02-15): Minecraft automated acceptance testing strategy — gap analysis and solution roadmap consolidated. Current state: 372 tests but zero world-state verification. Root cause: tests verify RCON command strings are correct but not what actually exists in the Minecraft world (command-ordering and fill-overlap bugs escape). P0 recommendations: fix integration test CI, add fill-overlap detection (unit test), expand RCON block verification (integration test). With CI optimizations, integration tests will run in ~2 minutes. — decided by Nebula, Rocket


 Team update (2026-02-16): Minecart lifecycle finalizedspawn on HTTP request, 3s timeout-based despawn at destination, max 5 carts/rail. NBT Age tracking with 5s polling cycle. ~1-2 RCON cmd/sec sustainable. Affects MinecartRailService implementation  decided by Rhodey

### Tech Branding Color System Update (2026-02-16)

Updated the `GetLanguageColor` method in StructureBuilder.cs to modernize tech stack color palette and apply Docker branding to Container resources:

**Color changes:**
- Rust: brown → red (matches Rust logo)
- Go: cyan → light_blue (matches Go gopher branding)
- Docker Container types: NEW cyan/aqua branding (Docker whale logo color)

**New language support:**
- PHP: magenta (Laravel/Symfony)
- Ruby: pink (Ruby/Rails)
- Elixir/Erlang: lime (Phoenix framework)

**Warehouse building enhancements:**
- Standard Warehouse: added language-colored stripe at y+2 (hollow fill) and banner on roof
- Grand Warehouse: added two hollow stripe bands (y+3, y+5) and four corner banners on roof (wall_banner facing N/S)
- Both now match Workshop buildings with tech branding visual identity
- Container types (AddContainer()) get aqua stripes/banners automatically

**RCON budget impact:** Standard Warehouse +2 commands, Grand Warehouse +6 commands — both well under burst mode limits.

**Final color palette:**
.NET Project = purple | JavaScript/Node = yellow | Python = yellow + blue secondary | Rust = red | Go = light_blue | Java/Spring = orange | Docker Container = cyan | PHP = magenta | Ruby = pink | Elixir/Erlang = lime | Default = white

📌 Team update (2026-02-16): Tech branding color system updated — Rust→red, Go→light_blue, Container→cyan, +3 new languages (PHP/Ruby/Elixir). Warehouse buildings now have language-colored stripes and banners matching Workshop aesthetic. — decided by Rocket

### Grand Village is Now the Only Option (2026-02-17)
- Removed small village structures entirely  no more conditional dispatch
- Grand layout values are now permanent defaults:
  - StructureSize = 15 (was 7 default)
  - Spacing = 36 (was 24 default)
  - GateWidth = 5 (was 3 default)
  - FenceClearance = 10 (unchanged)
- Removed ConfigureGrandLayout() method from VillageLayout.cs
- Removed ResetLayout() method from VillageLayout.cs
- Removed IsGrandLayout property from VillageLayout.cs
- Removed ASPIRE_FEATURE_GRAND_VILLAGE environment variable check from Program.cs
- All Build*Async methods in StructureBuilder.cs now directly call their BuildGrand*Async variants
- Central boulevard is always built now (removed IsGrandLayout guard, kept rows > 0 check)
- Rationale: Jeff's directive to focus on the grand design only. Simplifies codebase by removing two code paths for every structure type.

### NBT Library Evaluation for MCA Inspector (2026-02-18)

Evaluated 3 NBT library candidates (fNbt, SharpNBT, Unmined.Minecraft.Nbt) for issue #95 — prerequisite for AnvilRegionReader (#93) and fixture integration (#94).

**Decision:** Recommended **fNbt 1.0.0** (BSD-3-Clause, .NET Standard 2.0, most actively maintained, largest community).

**Key findings:**
- All 3 libraries are NBT-only parsers — none handle Anvil region (.mca) file format directly
- We must write our own AnvilRegionReader (~80 lines) for the MCA header/chunk offset/decompression layer
- Block state extraction from chunk NBT requires custom bit-unpacking logic for the 1.18+ palette format
- fNbt wins on maintenance (v1.0.0 Jul 2025), community adoption, NuGet availability, and .NET Standard 2.0 breadth
- SharpNBT viable but stale (last release Sep 2023, targets .NET 7 only)
- Unmined.Minecraft.Nbt too niche (pre-release, GitHub Packages only, 10 stars)
- Full evaluation with comparison table and conceptual API code written to `.ai-team/decisions/inbox/rocket-nbt-library-evaluation.md`

### AnvilRegionReader Implementation (Issue #93)

Implemented `AnvilRegionReader` in `tests/Aspire.Hosting.Minecraft.Integration.Tests/Helpers/AnvilRegionReader.cs` for reading Minecraft Anvil (.mca) region files. Used for test verification of in-world block placement.

**Key files:**
- `tests/Aspire.Hosting.Minecraft.Integration.Tests/Helpers/AnvilRegionReader.cs` — Full MCA reader with `GetBlockAt(worldX, worldY, worldZ)` API
- `tests/Aspire.Hosting.Minecraft.Integration.Tests/Aspire.Hosting.Minecraft.Integration.Tests.csproj` — Added fNbt 1.0.0 package reference

**Implementation details:**
- `AnvilRegionReader.Open(filePath)` parses `r.{x}.{z}.mca` filename and reads 4KB offset header
- `GetBlockAt()` converts world coords → region/chunk/section/block offsets, handles negative Y (1.18+ format, -64 to 319)
- Chunk decompression supports zlib (type 2), gzip (type 1), and uncompressed (type 3)
- Block state extraction navigates sections list → block_states compound → palette + packed long array
- Bit-unpacking uses 1.18+ format: indices don't span long boundaries, minimum 4 bits per entry
- `BlockState` record holds name + properties dictionary with human-readable `ToString()`
- Static helpers: `WorldToRegion()` and `GetRegionFilePath()` for coordinate/path lookups
- Edge cases: out-of-bounds Y returns null, missing chunks return null, wrong region returns null

## Learnings
- fNbt `NbtFile.LoadFromStream()` with `NbtCompression.None` works after manual decompression — don't double-decompress
- MCA sector offsets are 3-byte big-endian integers (shift+OR, not BitConverter) — Java heritage means all binary is big-endian
- 1.18+ section Y index for negative coords: use floor division `(worldY - 15) / 16` not truncation
- Palette bit-packing: indices per long = `64 / bitsPerEntry` (integer division), unused bits are padding at the high end
- `ZLibStream` (System.IO.Compression) handles the raw zlib format Minecraft uses — no need for third-party decompression

### Canal System Bug Fixes (2026-02-19)

**Three canal rendering bugs fixed in CanalService.cs:**

1. **Branch-to-trunk junction connectivity:** Trunk canal walls (built AFTER branches) sealed off every branch canal connection. Added OpenBranchJunctionsAsync post-pass that carves openings in the trunk's west wall at each branch canal's Z-level  air to remove wall, blue_ice for floor continuity, water to connect flows.

2. **Detour routing stays at original Z:** Branch canals that detoured south around blocking buildings never returned to their original Z-level, creating permanent southward drift. Fixed by resetting currentZ = z after each detour, producing clean U-shaped routes that return to the correct Z for trunk connection.

3. **Bridge elevation:** Connector bridges were placed at SurfaceY (same height as canal walls), creating flat ground-level surfaces. Raised to SurfaceY + 1 with oak_fence railings at SurfaceY + 2 for proper elevated walkway appearance.

## Learnings
- Build-order matters for overlapping fill commands: when trunk overwrites branch endpoints, a post-pass junction-carving step is needed rather than trying to prevent the overlap
- Canal junction pattern: air fill to remove wall, blue_ice floor, water to connect  three RCON commands per junction
- Bridge elevation needs to be at least SurfaceY + 1 (one above wall tops) with fence railings at +2 for visual walkway effect
- Detour routing must reset to original Z after passing each blocker to avoid permanent drift toward detour Z
- `CommunityToolkit.Aspire.Hosting.Java` AddSpringApp injects `JAVA_TOOL_OPTIONS=-javaagent:/opentelemetry-javaagent.jar` by default. If the container image stores the OTEL agent at a different path (e.g. `/agents/opentelemetry-javaagent.jar`), you MUST set `OtelAgentPath` in `JavaAppContainerResourceOptions` to match the image layout — otherwise the JVM fails on startup with "Error opening zip file or JAR manifest missing"
- The `aliencube/aspire-spring-maven-sample` image bundles its OTEL agent at `/agents/opentelemetry-javaagent.jar`, not at the root path the CommunityToolkit defaults to


 Team update (2026-02-19): Canal system junction fix  post-pass carving for branch-trunk connections, detour Z-reset for consistent junctions, bridge elevation to SurfaceY+1 with fence railings. ErrorBoatService and MinecartRailService should assume branch canals arrive at original entrance Z.  decided by Rocket

 Team update (2026-02-19): Canal system redesigned  single back canal + side trunk replaces per-building branches  decided by Rocket



## Learnings
- Village layout flattened from 2x2 zone quadrants to single horizontal row  all buildings now at BaseZ, incrementing X by Spacing
- Zone placement changed from NW/NE/SW/SE 2-column grids to sequential single-row zones: [Zone1][gap][Zone2][gap][Zone3][gap][Zone4] all at same Z
- GetStructureOrigin(int index) fallback simplified: x = BaseX + (index * Spacing), z = BaseZ (constant)  no more row/column math
- GetVillageBounds fallback changed from 2D grid (rows  Columns) to single row: maxZ = BaseZ + StructureSize - 1 (only one structure deep)
- Back canal now runs behind ALL buildings in single row instead of per-zone perimeters  simplified to one E-W canal + one N-S trunk
- VillageLayout.Columns constant still exists (value=2) for backward compatibility, but layout logic ignores it in favor of single row
- Team update (2026-02-19): Village layout flattened to single row  all zones placed sequentially along X axis at BaseZ, back canal runs behind all buildings  decided by Rocket

### Per-Building Canal System Rework (2026-02-23)

**Architecture change:** Replaced single shared back canal with individual per-building canals:
- Each building gets its own E-W canal running along its BACK (Z-max / north) side
- Short individual canals: start at building's west edge, run east to the trunk
- Single N-S trunk canal on the EAST side collects all per-building canals
- Trunk connects to a massive 8040 block lake (vs old 2012) at the south end
- Lake sized for creeper boat ErrorBoatService landings

**Key changes:**
- VillageLayout.LakeWidth: 20  80 blocks (town-width)
- VillageLayout.LakeLength: 12  40 blocks (deep landing zone)
- CanalService.BuildCanalNetworkAsync: builds per-building canals in a loop, not one shared canal
- CanalService.BuildPerBuildingCanalAsync: new method  canal at uilding.oz + StructureSize + 2 (behind building)
- CanalService.OpenPerBuildingJunctionAsync: opens trunk's west wall where each building canal arrives
- Program.cs forceload area expanded to cover larger lake (lakeX - 10 to lakeX + LakeWidth + 10)

**Canal positioning formula:**
- Building back Z: oz + StructureSize + 2 (2 blocks north of building's Z-max edge)
- Canal runs E-W from building's ox to trunk at maxX + CanalTotalWidth + 2
- Junction opened by removing trunk's west wall section where building canal arrives

**Key files:**
- src/Aspire.Hosting.Minecraft.Worker/Services/CanalService.cs  per-building canal logic
- src/Aspire.Hosting.Minecraft.Worker/Services/VillageLayout.cs  lake size constants
- src/Aspire.Hosting.Minecraft.Worker/Program.cs  expanded forceload area

**Design rationale:** Per-building canals are visually cleaner, better match the 2-column grid layout, and provide dedicated "addresses" for each resource. The massive lake gives ErrorBoatService plenty of room for creeper boat spawns and dramatic arrivals.

### Canal/Beacon/Health Rework (2026-02-24)

**Canal layout flipped from east-side to west-side trunk:**
- Trunk canal X: changed from `maxX + CanalTotalWidth + 2` (east) to `minX - CanalTotalWidth - 2` (west)
- Per-building canals still run behind buildings (Z-max), but now extend WESTWARD to the trunk
- Junction logic: opens trunk's EAST wall (not west) where building canals arrive from the east
- Trunk Z range: now spans from southernmost building canal to the lake (was only covering northernmost to lake)
- Program.cs forceload updated to cover west-side trunk area instead of east

**Beacon towers moved from behind buildings to right (east/+X) side:**
- GetBeaconOrigin: `(sx, sy, sz + StructureSize + 1)` → `(sx + StructureSize + 1, sy, sz)`
- Keeps beacons clear of the west-side canal network
- Both overloads updated (index-based and resourceName-based)

**Health detection diagnostic logging added to AspireResourceMonitor:**
- DiscoverResources: logs URL, TcpHost, TcpPort for each resource at Debug level
- PollHealthAsync: logs every resource's health check result every cycle (not just changes)
- CheckHttpHealthAsync: logs HTTP status code on success, exception type + message on failure
- HttpClient "aspire-monitor" configured with SocketsHttpHandler: PooledConnectionLifetime=30s, SSL cert bypass
- SSL bypass needed because dev certificates get rejected by default HttpClient validation

## Learnings

### Canal-Lake Junction Connection (2026-02-18)
**Issue:** Trunk canal endpoint needed to visually connect to lake even after OpenLakeJunctionAsync removes the north wall.
**Fix:** Extended trunk canal 2 blocks into the lake (lakeZ + 2) in BuildTrunkCanalAsync to ensure seamless water flow beyond the wall opening.
**Why:** The lake north wall is at lakeZ, and trunk ending exactly at lakeZ created ambiguity about whether connection was complete. Extending into the lake interior ensures obvious visual continuity.
**Files:** `CanalService.cs:201-214`


### Grand Observation Tower Implementation (2026-02-26)

**New file:** `src/Aspire.Hosting.Minecraft.Worker/Services/GrandObservationTowerService.cs`
- Standalone service (not part of StructureBuilder) per Rhodey's architecture plan
- 21x21 footprint, 32 blocks tall above SurfaceY, placed at x=20-40, z=-11 to 10
- 5 themed floors: Entrance Hall, Library, Armory/Beacon, Observation Gallery, Rooftop
- Continuous counter-clockwise spiral staircase (5 flights, individual setblock for facing)
- Safety fences on inside edges, wall torches every 2 steps for lighting
- Stairwell holes cut in floor platforms for continuous traversal
- Constructor injection: RconService, BuildingProtectionService, ILogger
- Three public methods: ForceloadAsync, RegisterProtection, BuildTowerAsync

**Program.cs changes:**
- DI registration: `AddSingleton<GrandObservationTowerService>()`
- Constructor parameter added to MinecraftWorldWorker
- Init sequence: forceload + protection + build, AFTER terrain probe/neighborhoods, BEFORE structures

**Architecture decisions:**
- Tower uses absolute coordinates (not relative to BaseX/BaseZ) since it's independent of village grid
- Burst mode (40 cmd/s) for construction efficiency
- Protection zone has 1-block X buffer and 2-block Z buffer matching StructureBuilder patterns
- Corner buttresses extend 2 blocks above main walls (y+33) for dramatic silhouette
- Compass markers use colored wool (N=red, S=blue, E=green, W=yellow)

## Learnings

### Spiral Staircase RCON Pattern (2026-02-26)
**Key insight:** Individual `setblock` commands are required for stairs because each step needs a `facing` direction property. Cannot use `fill` for staircase runs.
**Pattern:** Loop over step count, incrementing both position and Y per iteration. Facing direction matches the wall the flight runs along (east for south wall, south for east wall, west for north wall, north for west wall).
**Safety:** Oak fences on the inside edge of each flight prevent players from falling into the central shaft. Fence Y is stair Y + 1 (one block above the step).
**Files:** `GrandObservationTowerService.cs`

### Bridge Geometry Over Canals (2026-02-26)
**Canal clearance:** Water surface is at SurfaceY-1. Bridge deck at SurfaceY+2 gives 2 full air blocks (SurfaceY and SurfaceY+1) for boat passage underneath. This is the minimum the user requires.
**Walkway bridge shape:** 2-step ramp (stairs at SurfaceY+1 then SurfaceY+2) → flat deck at SurfaceY+2 over the 5-block canal width → 2-step ramp down. Total bridge length = 9 blocks (2 ramp + 5 canal + 2 ramp).
**Stair facing for ramps:** `facing=north` stairs at south approach → player walks north and steps up. `facing=south` stairs at north exit → symmetric descent. Works for both directions of travel.
**Rail elevation smoothing:** Rails auto-slope between 1-block height changes. Use iterative smoothing passes to ensure no adjacent rail positions differ by more than 1 block of elevation. Two passes handle both approach and exit sides of each canal span.
**Powered rails on bridges:** Use `redstone_block` instead of `redstone_torch` under powered rails on elevated bridge segments — torch placement conflicts with support column blocks.
**Bridge locations:** Boulevard bridges (one per row at the central boulevard) and entrance bridges (one per building at the entrance center X). Both cross the E-W canals behind buildings. Bridges at the same canal Z deduplicate automatically.
**Files:** `BridgeService.cs`, `MinecartRailService.cs`

### Initialization Order: Canals → Bridges → Rails (2026-02-26)
**Order matters:** CanalPositions must be populated before BridgeService runs (bridge locations derive from canal geometry). BridgeService must run before MinecartRailService so rail bridges don't interfere with walkway bridges. Both run AFTER StructureBuilder so building protection zones are registered.
**Files:** `Program.cs` (MinecraftWorldWorker main loop)

### BridgeService DI Registration Fix (2026-02-27)
**Bug:** BridgeService was fully implemented and wired into MinecraftWorldWorker (optional constructor param, init/update calls), but never registered in DI. The `bridges` parameter was always null, so no walkway bridges were ever placed in-world.
**Fix:** Added `builder.Services.AddSingleton<BridgeService>()` inside the canals feature flag block in Program.cs. Bridges only make sense over canals, so they share the `ASPIRE_FEATURE_CANALS` gate.
**Files:** `src/Aspire.Hosting.Minecraft.Worker/Program.cs`

### Rail Bridge Water-Level Blockage Fix (2026-02-27)
**Bug:** In `PlaceRailConnectionAsync()`, when a rail crosses a canal at elevation 2, the lowest support block at `adjustedY - 2` (which lands at canal water level SurfaceY-1) was solid `stone_bricks`. This blocked boat passage through the canal underneath the bridge.
**Fix:** Changed the lowest support block from `minecraft:stone_bricks` to `minecraft:oak_fence`. Fences have a narrow collision model that boats can pass through while still visually supporting the bridge deck. The `adjustedY - 1` support directly under the rail stays as stone_bricks for structural integrity.
**Key insight:** When building over navigable waterways, always use fence-type blocks at the water level — they support weight visually without blocking entity passage.
**Files:** `src/Aspire.Hosting.Minecraft.Worker/Services/MinecartRailService.cs`

 **Team update (2026-02-26):** 
- BridgeService enabled via DI registration in Program.cs  was implemented but never wired into the Worker's dependency injection container, causing walkway bridges to never build
- Track bridge water-level fix  replaced stone_bricks at canal water level with oak_fence to allow boat passage under rail bridges
- Walkway and rail bridges over canals implemented  both bridge types arch 2 blocks above canal water for boat clearance
- Grand Observation Tower service implemented  standalone 2121, 32-block tall structure at south entrance with spiral staircases and themed floors

### Tower Repositioning & Entrance Rotation (2026-02-27)
**Position calculation:** Tower horizontally centered on village x-axis at x=35 (midpoint between column 0 at x=10-24 and column 1 at x=46-60). TowerOriginX = 35 - 10 = 25, spanning x=25-45. Tower placed 15 blocks north of the northern fence (z=-10), so tower max-Z = -25, TowerOriginZ = -45.
**Entrance rotation:** Entrance moved from min-Z wall (z1) to max-Z wall (z2) so it faces south toward the village. Door facing changed from `south` to `north`, welcome sign from `z1+1/facing=south` to `z2-1/facing=north`, exterior lanterns from z1 to z2. Decorative arch above entrance also moved to z2.
**Minecraft door facing:** `facing=north` on a door at the max-Z wall means the flat/front face points north (into the building), which is correct for an entrance on the south wall.
**Files:** `GrandObservationTowerService.cs`, `GrandObservationTowerTests.cs`


## Learnings

### Dynamic Tower Position from Village Layout (2026-02-27)
**Pattern:** Tower X/Z origin is now computed dynamically from `VillageLayout.GetFencePerimeter(resourceCount)` instead of hardcoded constants. X centers on the village midpoint, Z is placed `NorthGap` (15) blocks north of the fence's north edge minus the tower depth.
**API:** `SetPosition(int resourceCount)` must be called before `ForceloadAsync`/`BuildTowerAsync`. An `EnsurePositionSet()` guard throws `InvalidOperationException` if forgotten. `NorthGap` is exposed as `internal const` for test use.
**Test approach:** Tests use a fixed `TestResourceCount = 4` and compute expected tower coordinates via the same `VillageLayout.GetFencePerimeter` formula. No hardcoded coordinate assertions  all derived from the layout engine.
**Files:** `GrandObservationTowerService.cs`, `Program.cs` (line ~270), `GrandObservationTowerTests.cs`

### Gate Centering, Tower Entrance, and Walkway (2026-02-27)
**Gate centering:** Fence gate X position in `StructureBuilder.BuildFencePerimeterAsync` changed from `BaseX + StructureSize` (aligned with boulevard) to `villageCenterX - gateWidth / 2` (centered on village midpoint). This aligns the gate directly opposite the tower entrance.
**Walkway:** 5-wide cobblestone path from `TowerMaxZ + 1` to `fenceMinZ - 1`, with `stone_brick_wall` borders and lanterns every 4 blocks. Built inside `GrandObservationTowerService.BuildWalkwayAsync` since the tower already stores both its own position and the fence position from `SetPosition`. Forceload extended south to cover the walkway gap.
**Tower entrance:** Upgraded from single oak door to double doors (hinge=right + hinge=left side by side). Added stone brick threshold at `z2 + 1` outside the door. Entrance lanterns moved to walkway level at `z2 + 1`. Door placement moved from `BuildFloor1EntranceHallAsync` to `BuildExteriorAsync` for correct ordering (clear air → place doors before interior fills).
**Coordinate references:** `_villageCenterX` and `_fenceMinZ` are stored in `SetPosition` and reused by walkway logic. Walkway Z range: `[TowerMaxZ + 1, fenceMinZ - 1]`. Gate width = `VillageLayout.GateWidth` (5 blocks).
**Files:** `StructureBuilder.cs` (line ~270), `GrandObservationTowerService.cs`, `GrandObservationTowerTests.cs`

### Spiral Staircase Redesign — Centered, Wide, Accessible (2026-02-26)
**Problem:** Original spiral stairs were cramped in corners (starting at x1+3, right against buttresses at x1-x1+2), 1 block wide, with small 3×4 stairwell holes. Players had difficulty navigating tight corners and narrow passages.
**Solution:** Redesigned all 5 flights to be 2 blocks wide, centered on each wall (using midX=x1+10 and midZ=z1+10), with 5×5 stairwell holes and 2×3 landing platforms at each floor transition.
**Coordinates:**
- Flight 1 (South→East): x1+7 to x1+13, z1+2-3, y+1→y+7, facing=east. Landing at x1+13-14, z1+2-4.
- Flight 2 (East→North): x2-2-3, z1+8 to z1+12, y+8→y+12, facing=south. Landing at x2-4-2, z1+12-13.
- Flight 3 (North→West): x2-8 to x2-12, z2-2-3, y+13→y+17, facing=west. Landing at x2-13-12, z2-4-2.
- Flight 4 (West→South): x1+2-3, z2-7 to z2-13, y+18→y+24, facing=north. Landing at x1+2-4, z2-14-13.
- Flight 5 (South→Roof): x1+7 to x1+13, z1+2-3, y+25→y+31, facing=east. Landing at x1+13-14, z1+2-4.
**Safety fences:** Placed on the inside edge of each 2-wide flight (z1+4, x2-4, z2-4, x1+4) at y+2, y+9, y+14, y+19, y+26.
**Wall torches:** Every other step along the back wall of each flight (z1+1, x2-1, z2-1, x1+1) for lighting.
**Key insight:** Centering on midX/midZ moves stairs away from 3×3 corner buttresses, creating clear approach paths. 2-wide stairs allow comfortable walking without precision jumps. Large stairwell holes (5×4-5) enable easy ascent/descent without head bumps.
**Files:** `GrandObservationTowerService.cs` — `BuildFlight[1-5]Async`, `ClearStairwellHolesAsync`, `BuildStairFencesAsync`, `BuildStaircaseLightingAsync`

### Fruit Stand at Town Center, Tower Floor Platform Edges (2026-02-27)
**Fruit stand relocation:** Moved from fixed position `BaseX + 12, BaseZ - 8` to dynamically calculated town center. Position computed as `(fMinX + fMaxX) / 2, (fMinZ + fMaxZ) / 2` from `VillageLayout.GetVillageBounds()`. Stand stores `_standX` and `_standZ` as nullable ints computed at first spawn. 5-wide stand centered with `-2` offset on X for proper centering.
**Tower floor platforms:** Extended from inset `x1+2 to x2-2, z1+2 to z2-2` to edges `x1+1 to x2-1, z1+1 to z2-1`. Floors now reach 1 block from interior walls instead of 2, eliminating the edge gap. Corner buttresses (3×3 deepslate at each corner) remain unaffected — they're placed after or coexist with floors. Floors affected: y+7, y+12, y+17, y+24 oak planks/deepslate tiles.
**Rationale:** Fruit stand at town center provides centralized landmark, visible from all village quadrants. Tower floor edge extension improves walkability — players no longer step off platform edge near walls.
**Files:** `VillagerService.cs` (lines 25-50), `GrandObservationTowerService.cs` (`BuildFloorPlatformsAsync`), tests pass without modification.
## Learnings

### Villager Spawn Position Bug Fix (2026-02-26)

**Issue:** Three villagers (Fowler, Brady, Scott) with OffsetZ=-1 spawned one block south of the fruit stand, landing in the canal. Additionally, the stand itself was positioned at (fMinZ + fMaxZ) / 2, which placed it right at the canal line (canals run at oz + StructureSize + 4 behind each building row).

**Fix:**
1. Changed front villagers from OffsetZ=-1 to OffsetZ=0  keeps them on the stand platform (which spans sz to sz+2)
2. Repositioned fruit stand from center Z to MinZ - 5 (southern boulevard area, well clear of canals)

**Key learning:** When spawning entities or placing structures in village center, check against canal positions. Canals run behind buildings at predictable Z coordinates. Use MinZ - offset to place things in the southern boulevard area, not center which can overlap canal zones.

### Tower Stairwell Hole Ordering Bug Fix (2026-02-26)

**Issue:** The spiral staircase build order was:
1. Build flights (stairs + landing platforms)
2. Clear stairwell holes (punch openings in floor platforms)
3. Build fences + lighting

Result: Stairwell holes destroyed the top stair steps and landing platforms with AIR, making the tower unclimbable.

**Fix:** Moved ClearStairwellHolesAsync() to run BEFORE all flight methods:
1. Clear stairwell holes (punch openings)
2. Build flights (stairs fill in solid parts where needed)
3. Build fences + lighting

**Key learning:** When building multi-level structures with floor openings, the order matters. Clear holes FIRST, then build solid features that will occupy part of that hole space. This gives you both the opening for head clearance AND the solid stairs/platforms for walking.

### Fruit Stand Center Regression Fix (2026-02-27)

**Issue:** The fruit stand was at `fMinZ - 5`, which is 5 blocks south of the northern fence edge — right inside the town gate, NOT at the village center. The previous fix overshot: it moved from an overlapping canal center to the gate area.

**Root cause:** `_standZ = fMinZ - 5` calculates from the MINIMUM Z bound, which is the northernmost edge. Also, `resourceCount = 10` was hardcoded, ignoring the actual number of Aspire resources.

**Fix:**
1. Added `SetResourceCount(int resourceCount)` method (same pattern as `GrandObservationTowerService.SetPosition`)
2. Changed Z calculation to `_standZ = (fMinZ + fMaxZ) / 2` — the true vertical center of the village bounds
3. Updated `Program.cs` to call `villagers.SetResourceCount(actualResourceCount)` before `SpawnVillagersAsync`

**Key learning:** Services that position things relative to village bounds need the real resource count — never hardcode it. Follow the `SetPosition`/`SetResourceCount` pattern used by `GrandObservationTowerService`. Also, `fMinZ` is the NORTH edge, not the center — always use `(min + max) / 2` for true center.

### Bridge Placement Fix — Dynamic Column Detection + Fence Bounds (2026-02-27)

**Issue:** BridgeService computed `boulevardCenterX` using a static formula: `BaseX + StructureSize + (Spacing - StructureSize) / 2 = 35`. When neighborhoods put all buildings in column 0 (e.g., SmallVillageDemo with 2 resources), the "boulevard" doesn't exist and X=35 is outside the east fence (fMaxX=34). Bridges straddled the fence and sat 10 blocks away from actual canal water.

**Root causes:**
- A: `boulevardCenterX` was a static formula ignoring actual building positions
- B: No validation against fence perimeter before building bridges
- C: Only "boulevard bridges" were supported — no awareness of actual layout geometry

**Fix (2 parts):**
1. **Dynamic column detection:** Collect actual building X origins via `GetStructureOrigin()`, find unique column positions. If <2 columns exist, skip boulevard bridges entirely (no boulevard to bridge). If 2+ columns, compute boulevard center(s) as midpoint between each pair of adjacent columns' structure edges. This handles standard 2-column, neighborhood multi-zone, and single-column layouts.
2. **Fence bounds checking:** Before building each bridge, compute its full extent (deck + ramps) and validate all corners are strictly inside `GetFencePerimeter()`. Skip any bridge that would overlap or extend past the fence.

**MinecartRailService check:** Rail bridges use `CanalPositions` from CanalService for dynamic crossing detection — no hardcoded boulevard X. Not affected by this bug.

**Key learning:** Never hardcode layout geometry (column positions, boulevard centers) in per-feature services. Always derive positions from actual building placements via `VillageLayout.GetStructureOrigin()`. The village grid is dynamic — neighborhoods, different resource counts, and zone layouts all change where buildings actually end up. Any placement formula that assumes "column 0 at BaseX, column 1 at BaseX + Spacing" will break when the layout doesn't fill both columns.
 Team update (2026-02-27): BridgeService now uses dynamic column detection + fence bounds checking to handle single-column layouts correctly (SmallVillageDemo with 2 resources). Boulevard bridges skip placement when outside fence bounds. All 558 tests pass.  decided by Rocket

 Team update (2026-02-27): Minecart-boats design review completed. 8 decisions finalized: native physics (no RCON tp), fix 4 critical bugs first (spawn position, rail type, canal entrance, boat propulsion), powered rails before ramps, entity lifecycle checks, rail path dedup, accept best-effort movement testing, ErrorBoatServiceCanalService dependency, forceload audit for canal transit  decided by Rhodey

### ErrorBoatService Critical Bug Fix (2026-02-27)

**Issue:** Three bugs made error boats useless:
1. `VillageLayout.GetCanalEntrance` returned `(ox - 2, CanalY, oz + StructureSize / 2)` — west side of building at Z midpoint. But canals actually run E-W *behind* buildings at `oz + StructureSize + 4`, starting from the building's east edge (`ox + StructureSize`). Boats spawned on dry land.
2. Boats summoned without `{Motion}` NBT — still water on blue ice doesn't push entities. Boats just sat there.
3. No CanalService dependency — boats could spawn before canals were built, landing on raw terrain.

**Fix:**
1. Fixed both `GetCanalEntrance` overloads in VillageLayout to return `(ox + StructureSize, CanalY, oz + StructureSize + 4)` — matching where CanalService actually builds per-building canals.
2. Added `{Motion:[-0.5,0.0,0.0]}` to boat summon command — westward push toward the trunk canal on blue ice.
3. Injected `CanalService?` (nullable) into ErrorBoatService; gated `SpawnBoatsForChangesAsync` on `canals.CanalPositions.Count > 0`.
4. Spawn Y now uses `CanalY` from the entrance tuple (water level) instead of `SurfaceY` (grass level).

**Key learning:** Always cross-reference layout helper methods against the service that actually builds the geometry. `GetCanalEntrance` was written with an early design assumption (west-side canals) that didn't match the final `CanalService` implementation (back-of-building E-W canals). When a helper returns coordinates, verify them against the fill commands that create the physical structure.


### 2026-02-27: Error boat timing bug  buffering pattern fixes race condition

Bug: ErrorBoatService.SpawnBoatsForChangesAsync() was silently failing when the API health flipped to unhealthy on the first worker loop iteration. The service has a gate (canals.CanalPositions.Count == 0) that exits early when canals aren't ready. Canals are built AFTER structures in the main loop, so on iteration 1: health poll  change detected  ErrorBoatService exits (no canals)  structures build  canals build. By iteration 2, the API is already unhealthy and no new change event fires.

Root cause: Temporal dependency  health changes detected before canals exist, and changes are lost because the monitor doesn't re-emit events for already-unhealthy resources.

Fix: Added a pending-change buffer to ErrorBoatService. When canals aren't ready, unhealthy transitions are buffered. After canals.InitializeAsync() completes, Program.cs calls errorBoats.SpawnBoatsForChangesAsync(empty) to replay buffered changes.

Files changed:
- src/Aspire.Hosting.Minecraft.Worker/Services/ErrorBoatService.cs
- src/Aspire.Hosting.Minecraft.Worker/Program.cs (line 354-356)
-  Team update (2026-02-27): ErrorBoatService uses buffering pattern to handle initialization race  health changes arrive before canals build, so service buffers unhealthy transitions and replays after CanalService.InitializeAsync() completes  decided by Rocket

## Learnings

### 2026-02-27: Error boat position and rotation fixes

**Issue:** Boats spawned underwater and faced the wrong direction.

1. **Underwater spawn:** `VillageLayout.GetCanalEntrance()` returned `CanalY` (water/ice level at `SurfaceY - 1`), so boats spawned *inside* the water block instead of floating on top.

2. **Wrong rotation:** The summon command had westward motion (`Motion:[-0.5,0.0,0.0]`) but no `Rotation` NBT, so boats faced random directions despite moving west.

**Fix:**

1. Changed both `GetCanalEntrance` overloads to return `CanalY + 1` instead of `CanalY` — boats now spawn on the water surface.

2. Added `Rotation:[270f,0f]` to the summon NBT in ErrorBoatService — boats face west (270°) toward the trunk canal, matching their motion vector.

**Key learning:** Minecraft entities spawn at the Y coordinate you specify. Water blocks are solid placement surfaces, so entities inside water appear underwater. To float on water, spawn at water_level + 1. Rotation NBT uses `[yaw, pitch]` format where yaw=270 is west-facing (0=south, 90=west, 180=north, 270=west). Visual orientation should match motion direction for best UX.

### 2026-02-27: Error boat creeper passenger and motion improvements

**Issue:** Two visual bugs: (1) Creeper passenger was invisible — the `Passengers` NBT tag in `/summon` doesn't work reliably on Paper servers. (2) Boats barely moved — `Motion:[-0.5,0.0,0.0]` gave a brief push but boats stopped almost immediately due to high water friction.

**Root causes:**
1. Paper server NBT handling: The `Passengers:[{...}]` tag on entity spawn is unreliable on Paper. It may be processed before the passenger entity exists, causing it to be ignored.
2. Boat physics: Boats on water have HIGH friction. Blue ice is under the water layer, so boats don't interact with it (ice friction only applies when entities are directly on ice blocks). A motion value of -0.5 gives ~2 block movement before stopping.

**Fix:**
1. **Separate summon + /ride approach:** Spawn boat and creeper separately with temporary tags (`eb_new`, `ec_new`), then use `/ride @e[type=creeper,tag=ec_new] mount @e[type=boat,tag=eb_new]` to mount the creeper. Clean up temp tags afterward. The `/ride` command (available since 1.20.2) is reliable on Paper.
2. **Stronger motion:** Changed from `Motion:[-0.5,0.0,0.0]` to `Motion:[-3.0,0.0,0.5]` — 6x westward force to push boats through water friction toward trunk canal, slight southward component (+Z) to flow toward lake after reaching trunk.
3. **Persistent tagging:** Boats keep the `error_boat` tag for reliable cleanup targeting — `kill @e[type=boat,tag=error_boat,...]` won't accidentally kill player boats.

**Commands issued (7 total per boat):**
```
summon minecraft:oak_boat X Y Z {Rotation:[270f,0f],Tags:["error_boat","eb_new"]}
summon minecraft:creeper X Y Z {NoAI:1b,Silent:1b,Tags:["ec_new"]}
ride @e[type=creeper,tag=ec_new,limit=1] mount @e[type=boat,tag=eb_new,limit=1]
data merge entity @e[type=boat,tag=eb_new,limit=1] {Motion:[-3.0,0.0,0.5]}
tag @e[tag=eb_new] remove eb_new
tag @e[tag=ec_new] remove ec_new
```

**Key learning:** For complex entity spawns (passengers, initial motion) on Paper servers:
- **Don't rely on NBT tags alone** — use separate summons + explicit mount commands
- **Apply motion after spawn** — `data merge entity` is more reliable than spawn-time Motion NBT
- **Use temporary tags** for multi-command setup, persistent tags for lifecycle management
- **Water friction is STRONG** — boats need much higher initial velocity (3-5 blocks/tick) to travel visible distances through canals before stopping

**Files:** `ErrorBoatService.cs` — `SpawnBoatCoreAsync`, `CleanupBoatsAsync`

### 2026-02-28: Atomic boat spawn and trigger-error decoupling

**Issue:** Three bugs after initial testing: (1) No creeper visible in boat. (2) Boat doesn't move. (3) API stuck offline permanently after clicking "Trigger Error".

**Root causes:**
1. **Missing creeper:** Multi-command RCON approach (summon boat → summon creeper → /ride) has timing issues. The `/ride` command executes before the server has fully spawned both entities on the game thread. Entity spawning from RCON isn't instantaneous even though commands are sequential.
2. **No motion:** `data merge entity` with `Motion` on boats in water is immediately overridden by Minecraft's physics engine. Boats on water recalculate velocity every game tick — the injected Motion value lasts ~0 ticks.
3. **Stuck offline:** `trigger-error` endpoint set `isHealthy = false` and never reset it. The `/health` endpoint returned 503 forever, causing permanent rain/lightning. Error boats are OTel-driven (via webhook), NOT health-driven — these are separate demo concerns.

**Fix:**
1. **Atomic spawn with Passengers NBT:** Reverted to SINGLE `summon` command with `Passengers:[{id:"minecraft:creeper",...}]` NBT. This spawns boat + passenger in one server tick. Earlier hypothesis that "Passengers doesn't work on Paper" was WRONG — the creeper was invisible because the boat was underwater (spawned at CanalY instead of CanalY+1). Now that the boat spawns ON the water surface, the Passengers approach works perfectly.
2. **Motion at spawn time:** Included `Motion:[-5.0,0.0,0.5]` directly in the summon NBT. When Motion is set during entity creation, it gets the FIRST physics tick before water dampening applies. With a high value like -5.0, the boat visibly slides several blocks westward before friction stops it. This is the best we can do with water-surface boats.
3. **Remove isHealthy toggle:** Deleted `isHealthy = false;` line from `trigger-error` endpoint in Program.cs. Clicking "Trigger Error" now spawns a boat (via OTel + webhook) WITHOUT breaking the health endpoint. To demo weather effects (rain/lightning from unhealthy service), users stop the service in the Aspire dashboard.

**Single atomic command (replaces 6 separate commands):**
```csharp
await rcon.SendCommandAsync(
    $"summon minecraft:oak_boat {cx} {cy} {cz} {{Rotation:[270f,0f],Motion:[-5.0,0.0,0.5],Passengers:[{{id:\"minecraft:creeper\",NoAI:1b,Silent:1b}}],Tags:[\"error_boat\"]}}",
    CommandPriority.Normal, ct);
```

**Key learnings:**
- Multi-command RCON entity spawning has timing issues — prefer atomic `summon` with NBT when possible
- `data merge entity` Motion on boats in water is immediately overridden by physics — set Motion at spawn time instead
- Passengers NBT in summon DOES work on Paper servers — earlier hypothesis was wrong (boats were underwater, not a Paper limitation)
- Increased Motion to -5.0 (from -3.0) for visible westward travel through high water friction
- Error boats (OTel-driven) and health status (weather effects) are separate demo concerns — don't couple them

**Files:** `ErrorBoatService.cs` — `SpawnBoatCoreAsync`; `GrandVillageDemo.ApiService/Program.cs` — `/trigger-error` endpoint
