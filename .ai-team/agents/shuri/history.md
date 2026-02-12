# Project Context

- **Owner:** Jeffrey T. Fritz (csharpfritz@users.noreply.github.com)
- **Project:** Aspire.Hosting.Minecraft — .NET Aspire integration for Minecraft servers
- **Stack:** C#, .NET 10, Docker, Aspire, OpenTelemetry, Minecraft Paper Server, RCON
- **Created:** 2026-02-10

## Key Facts

- Single packable NuGet package: Fritz.Aspire.Hosting.Minecraft (Rcon embedded via PrivateAssets, Worker is IsPackable=false)
- Version defaults to `0.1.0-dev`, CI overrides via `-p:Version` from git tag
- Deterministic builds, EnablePackageValidation enabled in Directory.Build.props (SourceLink removed per Jeff's request)
- All deps pinned to exact versions (no floating Version="*")
- Content files: bluemap/core.conf and otel/opentelemetry-javaagent.jar bundled with hosting package

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### Consolidated Summary: Sprints 1-3 (2026-02-10)

**Sprint 1 — NuGet hardening:**
- Pinned 6 floating deps to exact versions. Added GenerateDocumentationFile, EnablePackageValidation, Deterministic, ContinuousIntegrationBuild, EmbedUntrackedSources, Microsoft.SourceLink.GitHub to Directory.Build.props.
- Single package consolidation: Rcon embedded via PrivateAssets="All" + BuildOutputInPackage. Worker is IsPackable=false.
- PackageId renamed from Aspire.Hosting.Minecraft to Fritz.Aspire.Hosting.Minecraft (reserved namespace).
- Public API audit (#12): MinecraftHealthCheck -> internal. All Worker types -> internal. Public: MinecraftServerBuilderExtensions, MinecraftServerResource, 5 RCON types.

**Sprint 2 — XML docs, RCON throttle, config APIs:**
- XML doc comments on all public types/methods across both projects.
- RCON throttle: optional `minCommandInterval` param (default disabled). 250ms in production. Per-command-string deduplication.
- Configuration builder review: existing `With*()` fluent pattern is sufficient — no formal options class needed.
- Server properties API: `WithServerProperty(string, string)`, `WithServerProperties(Dictionary)`, 6 convenience methods (WithGameMode, WithDifficulty, WithMaxPlayers, WithMotd, WithWorldSeed, WithPvp).
- ServerProperty enum (24 members), MinecraftGameMode enum (4), MinecraftDifficulty enum (4). PascalCase->UPPER_SNAKE_CASE conversion.
- `WithServerPropertiesFile()` for bulk loading from disk.
- NuGet version changed to 0.1.0-dev with CI override via -p:Version.

**Sprint 3 — World border, dependency placement, rate limiting:**
- WorldBorderService (#28): Shrinks 200->100 blocks over 10s when >50% unhealthy. Restores over 5s. Red warning tint at 5 blocks. Opt-in via ASPIRE_FEATURE_WORLDBORDER.
- Ephemeral world by default: Removed named Docker volume from AddMinecraftServer(). Added WithPersistentWorld() for opt-in persistence.
- RCON rate-limiting (#29): CommandPriority enum (Low/Normal/High). Token bucket at 10 cmd/s. High bypasses limits. Low queued in bounded Channel<T> (100, DropOldest).
- Dependency placement (#29): ResourceInfo.Dependencies from ASPIRE_RESOURCE_{NAME}_DEPENDS_ON env vars. VillageLayout.ReorderByDependency() uses BFS topological sort. WithMonitoredResource() accepts params string[] dependsOn + auto-detects IResourceWithParent.

**Azure SDK Research:**
- Separate NuGet package recommended: Fritz.Aspire.Hosting.Minecraft.Azure (~5 MB Azure SDK deps).
- Packages: Azure.ResourceManager, Azure.Identity, Azure.ResourceManager.ResourceHealth, Azure.Monitor.Query.Metrics.
- Azure.Monitor.Query is deprecated — use Azure.Monitor.Query.Metrics instead.
- DefaultAzureCredential for auth. Polling for v1 (not Event Grid).
- ARM rate limits: 250 reads / 25 per sec per subscription per region — plenty for 10-50 resources at 30-60s intervals.
- Research doc: docs/epics/azure-sdk-research.md

### Team Updates

- 18 Minecraft interaction features proposed across 3 tiers — decided by Rocket
- 3-sprint roadmap adopted — decided by Rhodey
- CI/CD pipeline created (build.yml + release.yml) — decided by Wong
- Test infrastructure created — InternalsVisibleTo, 62 tests passing — decided by Nebula
- FluentAssertions fully removed — decided by Jeffrey T. Fritz, Nebula
- Release workflow extracts version from git tag — decided by Wong
- Sprint 2 API review complete — 5 recommendations for Sprint 3 — decided by Rhodey
- Beacon tower colors match Aspire dashboard palette — decided by Rocket
- Hologram line-add bug fixed — decided by Rocket
- Azure RG epic designed — Shuri owns Phases 1 and 3 (ARM client, auth, options, NuGet scaffold) — decided by Rhodey
- Azure monitoring ships as separate NuGet package — decided by Rhodey, Shuri

### Structure Build Validation (2026-02-10)

- Added validation to StructureBuilder after each structure type builds (Watchtower, Warehouse, Workshop, Cottage).
- Validation checks door air blocks and window blocks (glass_pane or stained_glass) at expected coordinates.
- `VerifyBlockAsync()` helper method uses `testforblock` RCON command to verify block placement.
- Logs warnings on validation failure for graceful degradation — does not throw exceptions.
- Location: src/Aspire.Hosting.Minecraft.Worker/Services/StructureBuilder.cs

### VillageLayout Unit Tests (2026-02-10)

- Created comprehensive unit tests for VillageLayout static methods in tests/Aspire.Hosting.Minecraft.Worker.Tests/Services/VillageLayoutTests.cs.
- Tests verify coordinate math correctness for GetStructureOrigin (with 1, 2, 4, 8, 10 resources), GetStructureCenter, GetVillageBounds, GetFencePerimeter.
- Tests verify topological sort correctness for ReorderByDependency with various dependency chains (simple, chained, diamond, multiple).
- ResourceInfo constructor requires: Name, Type, Url, TcpHost, TcpPort, Status, Dependencies (optional).
- All 32 tests pass successfully.
 Team update (2026-02-11): All sprints must include README and user documentation updates to be considered complete  decided by Jeffrey T. Fritz
 Team update (2026-02-11): All plans must be tracked as GitHub issues and milestones; each sprint is a milestone  decided by Jeffrey T. Fritz
 Team update (2026-02-11): Boss bar title now configurable via WithBossBar(title) parameter and ASPIRE_BOSSBAR_TITLE env var; ASPIRE_APP_NAME no longer affects boss bar  decided by Rocket
 Team update (2026-02-11): Village structures now use idempotent build pattern (build once, then only update health indicators)  decided by Rocket

### Sprint 4: Issues #62 and #63 (WithAllFeatures + env var tightening)

- **Issue #63 — Tighten feature env var checks:** Changed all 15 feature registration checks in `Program.cs` from `!string.IsNullOrEmpty(builder.Configuration["ASPIRE_FEATURE_*"])` to `builder.Configuration["ASPIRE_FEATURE_*"] == "true"`. Also updated the PEACEFUL check in `ExecuteAsync` from `!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(...))` to `== "true"`. Verified all `With*()` extension methods in `MinecraftServerBuilderExtensions.cs` already set env vars to `"true"` — no changes needed there.
- **Issue #62 — WithAllFeatures() convenience method:** Added `WithAllFeatures()` extension method to `MinecraftServerBuilderExtensions.cs` that calls all 17 feature methods: WithParticleEffects, WithTitleAlerts, WithWeatherEffects, WithBossBar, WithSoundEffects, WithActionBarTicker, WithBeaconTowers, WithFireworks, WithGuardianMobs, WithDeploymentFanfare, WithWorldBorderPulse, WithAchievements, WithHeartbeat, WithRedstoneDependencyGraph, WithServiceSwitches, WithPeacefulMode, and WithRconDebugLogging. Uses the same guard clause pattern (checks `WorkerBuilder` is not null) and includes XML doc comments listing all enabled features.
- **Key decision:** WithAllFeatures() includes WithPeacefulMode() and WithRconDebugLogging() since they are opt-in features gated behind the same guard pattern. Placed the method logically between WithPeacefulMode() and WithServerProperty() methods.
- **Build:** 0 errors, 1 pre-existing warning (CS8604 nullable in MinecraftServerResource). All 62 tests pass (Worker.Tests host crash is pre-existing, unrelated).

### Fresh Server Lifecycle (2026-02-11)

- Added `.WithLifetime(ContainerLifetime.Session)` to `AddMinecraftServer()` builder chain in `MinecraftServerBuilderExtensions.cs`.
- `ContainerLifetime.Session` is actually the Aspire default for containers, but making it explicit documents the intent that the Minecraft server should be destroyed and recreated each Aspire session — no Docker volume or container state carries over.
- The `ContainerLifetime` enum lives in `Aspire.Hosting.ApplicationModel` (already imported). Available since Aspire.Hosting 9.x, confirmed in our 13.1.1 dependency.
- Without `WithPersistentWorld()`, world data lives in the container's writable layer at `/data`. Session lifetime ensures Docker Desktop doesn't cache the container between runs.
- Build: 0 errors, 2 pre-existing warnings (CS8604 nullable, xUnit1026 unused param).
 Team update (2026-02-12): SourceLink removed from Directory.Build.props per user directive (v0.4.0 release)  decided by Jeffrey T. Fritz
 Team update (2026-02-12): Sprint 4 and Sprint 5 feature sets finalized; Sprint 4 scope confirmed: Redstone Dashboard, Enhanced Buildings, Dragon Egg monument, DX polish, documentation  decided by Rhodey

### Milestone 5: VillageLayout Configurable Properties (#77)

- Converted `Spacing`, `StructureSize` from `const int` to `static int { get; private set; }` with defaults matching Sprint 4 values (Spacing=24, StructureSize=7).
- Added `FenceClearance` property (default=10, matching existing hardcoded 10-block fence gap).
- `ConfigureGrandLayout()` sets StructureSize=15 and FenceClearance=6. Spacing stays 24 (already correct from Sprint 4).
- `ResetLayout()` (internal) restores defaults — needed for test isolation since properties have `private set`.
- `GetStructureCenter()` now uses `StructureSize / 2` instead of hardcoded 3. For StructureSize=7, 7/2=3 (integer division) — backward compatible.
- Added `GetRailEntrance(index)`: returns position centered on structure front face, one block south (Z-1) at SurfaceY+1.
- `GetFencePerimeter()` now uses `FenceClearance` property instead of hardcoded 10.
- `Program.cs` calls `ConfigureGrandLayout()` when `ASPIRE_FEATURE_GRAND_VILLAGE == "true"`, placed before service registrations.
- 11 new tests added (329 total Worker tests pass). Tests cover both standard and grand layout configurations.
- Key file paths: `src/Aspire.Hosting.Minecraft.Worker/Services/VillageLayout.cs`, `tests/.../Services/VillageLayoutTests.cs`.

### Milestone 5: Grand Warehouse (#81)

- Redesigned `BuildWarehouseAsync()` to branch on `VillageLayout.StructureSize >= 15` — grand mode dispatches to `BuildGrandWarehouseAsync()`, standard mode preserves existing 7×7 behavior unchanged.
- Grand warehouse: 15×15 footprint, 8 blocks tall (y+1 to y+7 walls, y+8 roof). Iron block corner pillars + floor/roof frame with deepslate brick infill panels.
- 5-wide × 4-tall cargo bay entrance on front face (z-min), centered at x+5 to x+9.
- Purple stained glass clerestory windows at y+7 on all four walls (front wall skips cargo bay area).
- Stone brick loading dock extends 2 blocks south (z-1, z-2) from entrance, with oak fence railings on sides.
- Interior: 4 iron block columns at quarter-points (4,4 / 10,4 / 4,10 / 10,10), floor to y+6.
- Interior: 4×2 barrel grid (8 barrels) at z+11/z+12, 2 chest rows (8 chests) at z+11/z+12.
- Interior: 5 hanging lanterns at ceiling (y+7), resource name wall sign on back wall.
- Updated `PlaceHealthIndicatorAsync`: grand warehouse lamp at y+5 (above 4-tall cargo door).
- Updated `PlaceAzureBannerAsync`: grand warehouse roofY = y+9.
- Signature changed: `BuildWarehouseAsync` now accepts `ResourceInfo` for interior sign text.
- RCON command count: 43 commands (within ~45-55 budget).
- Build: 0 errors, 329 Worker tests pass.

### Milestone 5: Grand Silo — Two-Floor Database Cylinder (#83)

- **BuildGrandCylinderAsync** added to `StructureBuilder.cs`: 15×15 footprint (radius 7), 12 blocks tall, dispatched when `VillageLayout.StructureSize >= 15`.
- Pre-calculated circle coordinates in `(dz, x1, x2)[]` tuples — one `/fill` per row per layer. Avoids individual `/setblock` for circular geometry.
- **Wall materials:** Smooth stone (y+1 to y+4), cut copper accent band (y+5 to y+6), polished deepslate (y+7 to y+9), cut copper top band (y+10).
- **Dome roof:** 3-layer dome — deepslate tile slab (y+11, full circle), polished deepslate slab (y+12, smaller cap), polished deepslate slab (y+13, peak).
- **Central copper pillar:** Single `/fill` from y+0 to y+12 at (x+7, z+7) — the "data spindle" aesthetic.
- **Lower floor (y+1 to y+5):** Iron block server rack ring via 4 `/fill` segments, 3×3 copper center island, 4 redstone lamps at cardinal positions.
- **Upper floor (y+6 to y+10):** Polished deepslate disc at y+6, bookshelf ring via 4 `/fill` segments, enchanting table, 2 oak wall signs.
- **Ladder access:** 6 ladder blocks (y+1 to y+6) on the east face of the central pillar.
- **Iron door entrance:** At (x+7, z+4) with air clearance at y+3.
- Interior circle uses a separate `interiorRows` tuple array (11 rows) with smaller radii for the hollowed interior.
- **Helper method updates:** `PlaceAzureBannerAsync` roof height for grand cylinder = y+13. `PlaceHealthIndicatorAsync` lamp at (x+7, y+4, z+4) for grand cylinder (front circular wall). `PlaceSignAsync` places sign at z+3 for grand cylinder (in front of circular entrance).
- **Backward compatible:** Standard 7×7 cylinder is untouched when `StructureSize < 15`.
- Build: 0 errors. 329 tests pass (0 failed).

### Milestone 5: WithGrandVillage and WithMinecartRails Extensions (#79)

- Added `WithGrandVillage()` extension method: guard clause pattern (checks WorkerBuilder not null), sets `ASPIRE_FEATURE_GRAND_VILLAGE=true` on worker, fluent return. XML docs reference `WithAspireWorldDisplay`.
- Added `WithMinecartRails()` extension method: same guard clause pattern, sets `ASPIRE_FEATURE_MINECART_RAILS=true` on worker. MinecartRailService registration in Program.cs is stubbed (service doesn't exist yet).
- Updated `WithAllFeatures()` to chain both new methods at the end of the feature list. Updated XML doc `<see cref>` list and test assertion (17→19 feature env vars).
- Added `ASPIRE_FEATURE_MINECART_RAILS` check in Worker `Program.cs` (after the existing `ASPIRE_FEATURE_GRAND_VILLAGE` check, before service registrations).
- Updated demo AppHost `Program.cs` to chain `.WithGrandVillage().WithMinecartRails()` under a "Milestone 5" comment section.
- Updated `WithAllFeatures_SetsAllFeatureEnvVars` test: added both new env vars to expected list, count from 17 to 19. All 19 hosting tests pass, all 329 worker tests pass.

📌 Team update (2026-02-12): VillageLayout constants converted to configurable properties (#77) with defaults matching existing behavior, ConfigureGrandLayout() for Milestone 5 Grand Village, ResetLayout() for test isolation — decided by Shuri
