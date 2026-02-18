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
- Grand Village is now the default and only village layout — small village removed

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### Grand Village Becomes Default (2026-02-17)

- Removed `WithGrandVillage()` extension method entirely from `MinecraftServerBuilderExtensions.cs` — grand village (15×15 buildings) is now always-on, no feature flag needed.
- Removed `.WithGrandVillage()` from `WithAllFeatures()` method chain in `MinecraftServerBuilderExtensions.cs`.
- Updated `WithAllFeatures_SetsAllFeatureEnvVars` test — reduced count from 21 to 20 feature env vars, removed `ASPIRE_FEATURE_GRAND_VILLAGE` from expected list.
- Deleted entire `samples/MinecraftAspireDemo/` directory — GrandVillageDemo is now the only sample.
- Updated `Aspire-Minecraft.slnx` — removed all MinecraftAspireDemo project entries.
- Updated integration test fixture (`MinecraftAppFixture.cs`) — changed from `Projects.MinecraftAspireDemo_AppHost` to `Projects.GrandVillageDemo_AppHost`.
- Updated integration test `.csproj` — changed ProjectReference from MinecraftAspireDemo.AppHost to GrandVillageDemo.AppHost.
- Updated README.md — removed WithGrandVillage references, updated demo instructions to use GrandVillageDemo, clarified that Grand Village buildings are now the default.

📌 Team update (2026-02-18): Test improvement triage assigned — Shuri to implement #94 (WorldSaveDirectory + AnvilTestHelper), 2-3 days effort after Rocket completes #93. Add WorldSaveDirectory property to MinecraftAppFixture, ensure Docker volume mount is wired, create AnvilTestHelper static class with LoadRegionAsync()/GetBlockAsync() convenience wrappers. Depends on #93 (AnvilRegionReader) completion — decided by Rhodey
- Updated CONTRIBUTING.md — changed sample reference from MinecraftAspireDemo to GrandVillageDemo.
- Updated docs/ files — replaced MinecraftAspireDemo references with GrandVillageDemo in blog posts and design docs.
- Key insight: Grand village was always intended as the production experience — small village was scaffolding during development.


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

### Python and Node.js Sample Projects (2026-02-15)

- Added Python (http.server) and Node.js (http module) minimal API projects to MinecraftAspireDemo sample.
- Both are Executable resources in Aspire → mapped to Workshop building type by GetStructureType.
- Aspire.Hosting.Python v13.1.1: `AddPythonApp(name, appDirectory, scriptPath)` — 3-param non-obsolete overload.
- Aspire.Hosting.JavaScript v13.1.1: `AddNodeApp(name, appDirectory, scriptPath)` — returns IResourceBuilder<NodeAppResource> which implements IResourceWithEndpoints (inherits from ExecutableResource → JavaScriptAppResource).
- PythonAppResource extends ExecutableResource, NodeAppResource extends JavaScriptAppResource extends ExecutableResource.
- WithMonitoredResource accepts both via covariant IResourceBuilder<IResourceWithEndpoints> overload.
- Key paths: samples/MinecraftAspireDemo/MinecraftAspireDemo.PythonApi/, samples/MinecraftAspireDemo/MinecraftAspireDemo.NodeApi/

### Grand Village Demo Sample (2026-02-15)

- Created samples/GrandVillageDemo/ as a self-contained Aspire sample on milestone-5 branch.
- Includes all resource types (Project, Container/Database, Azure, Python, Node.js) so every grand building variant renders.
- Uses WithAllFeatures() which chains WithGrandVillage() and WithMinecartRails() along with all Sprint 1-3 features.
- Different ports (15272 for dashboard, 5280/5281 for services, 5300/5400 for Python/Node) to avoid conflicts with the existing sample.
- Key path: samples/GrandVillageDemo/GrandVillageDemo.AppHost/Program.cs

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

### Milestone 5: Grand Village Fence, Paths, Forceload (#84)

- **Fence perimeter** now uses `VillageLayout.GateWidth` (3 standard, 5 grand) instead of hardcoded `gateX + 2`. Gate position adapts via `BaseX + StructureSize`. Fence clearance uses `VillageLayout.FenceClearance` property.
- **Paths** cover full fence interior via `GetFencePerimeter()`. Grand layout adds a 3-wide stone brick central boulevard between columns using `VillageLayout.IsGrandLayout`.
- **Forceload** coordinates computed dynamically from `VillageLayout.GetFencePerimeter(10)` with 10-block margin — replaces hardcoded `forceload add -20 -20 120 120`.
- **MAX_WORLD_SIZE** bumped from 256 to 512 in `MinecraftServerBuilderExtensions.cs` to support Grand Village with up to 20 resources.
- **New VillageLayout properties:** `GateWidth` (default 3, grand 5), `IsGrandLayout` (default false) — both configured by `ConfigureGrandLayout()` and reset by `ResetLayout()`.
- **Test coverage:** Added `GateWidth` and `IsGrandLayout` assertions to `DefaultLayout_MatchesSprint4Values`, `ConfigureGrandLayout_SetsGrandValues`, and `ResetLayout_RestoresDefaults` tests.
- Standard layout (no `WithGrandVillage`) is unaffected — all 393 unit tests pass (45 Rcon + 19 Hosting + 329 Worker).

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

📌 Team update (2026-02-16): Feature monitoring services moved to continuous loop (affects Program.cs) — decided by Coordinator

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

### Milestone 5: RCON Burst Mode No-Op Fix (#85)

- `EnterBurstMode()` changed from throwing `InvalidOperationException` when already active to returning a singleton `NoOpDisposable.Instance` — callers don't need try/catch and nested `using` blocks are safe.
- Added `NoOpDisposable` private sealed class with static singleton pattern inside `RconService`.
- Burst mode is still thread-safe: `_burstModeSemaphore.Wait(0)` guards against concurrent activation; only the first caller gets the real `BurstModeScope` that restores the rate limit on dispose.
- Fence/Paths/Forceload (#84) were already correctly implemented in the prior sprint — verified no hardcoded values remain. Gate uses `BaseX + StructureSize`, fence uses `FenceClearance`, forceload uses `GetFencePerimeter(10)` dynamically, `MAX_WORLD_SIZE` is 512.

📌 Team update (2026-02-15): JAR files (e.g., opentelemetry-javaagent.jar) OK to keep in repo under lib/ folder; no need for build-time downloads — decided by Jeffrey T. Fritz

### ExecutableResource Subclass Detection Fix (2026-02-15)

- **Root cause:** `WithMonitoredResource()` sends the concrete class name via `GetType().Name.Replace("Resource", "")` — e.g., `PythonAppResource` → `"PythonApp"`, `NodeAppResource` → `"NodeApp"`. But `GetStructureType()` only matched the exact string `"executable"` in its switch statement.
- **Fix:** Added `IsExecutableResource()` predicate (same pattern as `IsDatabaseResource`/`IsAzureResource`) that matches `"executable"`, `"pythonapp"`, `"nodeapp"`, and `"javascriptapp"` via `contains`-based string matching. Hooked it into `GetStructureType()` before the switch, returning `"Workshop"`.
- **Pattern:** When Aspire resource types use inheritance (e.g., `PythonAppResource extends ExecutableResource`), the env var carries the concrete type name, not the base. The worker must use contains-matching to catch all subclasses, same as database and Azure resources already do.
- **Key file:** `src/Aspire.Hosting.Minecraft.Worker/Services/StructureBuilder.cs`
- **Tests added:** 5 new tests in `StructureBuilderTypeTests.cs` + 3 new `InlineData` entries in `StructureBuilderTests.cs`. All pass.

 Team update (2026-02-16): Grid ordering unificationall services placing elements on village grid must use VillageLayout.ReorderByDependency(). Affects StructureBuilder, BeaconTowerService, GuardianMobService, ParticleEffectService, ServiceSwitchService, RedstoneDependencyService, MinecartRailService  decided by Rocket

### Village Redesign Phase 1: Spacing, Canal/Lake Infrastructure (2026-02-16)

- **ConfigureGrandLayout()** now sets `Spacing = 36` (was 24) and `FenceClearance = 10` (was 6) to accommodate canals between structures and lake outlet space.
- **ResetLayout()** already correctly resets Spacing to 24 — no change needed there.
- Added canal constants: `CanalTotalWidth` (5), `CanalWaterWidth` (3), `CanalDepth` (2), `CanalY` (SurfaceY - 1).
- Added lake constants: `LakeWidth` (20), `LakeLength` (12), `LakeBlockDepth` (3), `LakeGap` (20).
- Added `GetCanalEntrance(int index)` — returns position on east side of building at CanalY.
- Added `GetLakePosition(int resourceCount)` — returns lake northwest corner, centered on village X-axis, placed LakeGap blocks beyond last row.
- **MAX_WORLD_SIZE** bumped from 512 to 768 in `MinecraftServerBuilderExtensions.cs` to support the expanded village footprint.
- XML doc comments updated to reflect 36-block grand spacing and 10-block grand fence clearance.
- Tests updated: `ConfigureGrandLayout_SetsGrandValues`, `ConfigureGrandLayout_SetsCorrectPropertyValues`, `GetStructureOrigin_GrandLayout_ReturnsCorrectCoordinates`, `GetFencePerimeter_GrandLayout` — all 59 VillageLayout tests pass, 19 hosting tests pass.
- Standard (non-grand) layout is completely unaffected — Spacing stays 24, FenceClearance stays 10.

📌 Team update (2026-02-17): Village Redesign Phase 1 implementation complete — Grand spacing 24→36, canal/lake infrastructure methods added, integrated with CanalService and ErrorBoatService — decided by Shuri

### Village Redesign Phase 2: World Expansion & Feature Adjustments (2026-02-17)

- **MAX_WORLD_SIZE** changed from 768 to 29999984 (Minecraft's maximum) in `MinecraftServerBuilderExtensions.cs` — eliminates the invisible wall Jeff was hitting.
- **VIEW_DISTANCE** increased from 6 to 12, **SIMULATION_DISTANCE** from 4 to 8 — better visibility in the expanded world.
- **WorldBorderService** diameters increased: NormalDiameter 200→2000, CriticalDiameter 100→1000. Updated XML docs to match.
- **Sample app** (`MinecraftAspireDemo.AppHost/Program.cs`): Removed `.WithRedstoneDependencyGraph()` from the main feature chain. Added `.WithCanals()` and `.WithErrorBoats()` to the `useGrandVillage` block.
- **WithAllFeatures()**: Removed `.WithRedstoneDependencyGraph()` from the chained method list. Updated XML doc `<see cref>` list. The extension method and `RedstoneDependencyService.cs` are preserved for manual opt-in.
- **Test update**: `WithAllFeatures_SetsAllFeatureEnvVars` count adjusted from 21→20 ASPIRE_FEATURE_ env vars. Removed `ASPIRE_FEATURE_REDSTONE_GRAPH` from expected list.
- Build: 0 errors, pre-existing warnings only (CS8604 nullable, xUnit1026 unused param).

📌 Team update (2026-02-17): Redstone Dependency Graph removed from defaults — decided by Shuri

### Fix: Canal/Rail Forceload and Command Priority (2026-02-17)

- **BUG 1 — Forceload coverage:** Village forceload at line 197 only covered `GetFencePerimeter(10)`, but canals extend east (trunk canal at `maxX + CanalTotalWidth + 2`) and the lake extends south (`maxZ + LakeGap`). Added two additional `forceload add` commands after `DiscoverResources()` that cover: (1) the canal corridor from village edge to trunk canal + width, and (2) the lake area with 5-block margin. Only runs when `canals is not null` and resources exist.
- **BUG 2 — CommandPriority.Low drop:** The RCON queue (bounded Channel<T>, DropOldest) was silently dropping canal/rail commands. Changed all build commands in `CanalService.BuildBranchCanalAsync`, `BuildTrunkCanalAsync`, `BuildLakeAsync` from `CommandPriority.Low` to `CommandPriority.Normal`. Same fix in `MinecartRailService.PlaceRailConnectionAsync` and `PlaceStationAsync`. Added `rcon.EnterBurstMode(40)` to `MinecartRailService.InitializeAsync` (CanalService already had it).
- **Queue capacity:** Increased `BoundedChannelOptions` capacity from 100 to 500 in `RconService.cs` constructor to provide headroom for legitimate Low-priority commands from other features.
- **Key insight:** Normal-priority commands wait briefly (100ms) for a rate token instead of being queued; combined with burst mode (40 cmd/s during init), this ensures all commands execute. Low priority is only appropriate for fire-and-forget commands where dropping is acceptable.
- **Files changed:** `Program.cs`, `CanalService.cs`, `MinecraftRailService.cs`, `RconService.cs`
- Build: 0 errors. All 134 relevant tests pass (45 Rcon + 19 Hosting + 70 Worker). One pre-existing failure in StructureBuilderTests is unrelated.

### Fix: Duplicate HTTP Endpoint on Java Spring Container Resources (2026-02-17)

- **Root cause:** `AddSpringApp()` (from CommunityToolkit.Aspire.Hosting.Java) internally calls `AddJavaApp()`, which chains `.WithHttpEndpoint(port: options.Port, targetPort: options.TargetPort, name: JavaAppContainerResource.HttpEndpointName)`. Both demo AppHost `Program.cs` files then called `.WithHttpEndpoint(targetPort: 8080, port: 5500)` on the result, creating a **second** unnamed HTTP endpoint — causing a duplicate endpoint allocation error at runtime.
- **Fix:** Removed the explicit `.WithHttpEndpoint()` call from both `GrandVillageDemo.AppHost/Program.cs` and `MinecraftAspireDemo.AppHost/Program.cs`. Instead, set `Port = 5500` directly in the `JavaAppContainerResourceOptions` object passed to `AddSpringApp()`. The `TargetPort` defaults to 8080 (matching the Spring Boot container), so only `Port` needs to be overridden.
- **Port audit:** Verified no within-app port conflicts exist. MinecraftAspireDemo uses Python=5100, Node=5200, Java=5500. GrandVillageDemo uses Python=5300, Node=5400, Java=5500. Both share Java port 5500 but are separate Aspire apps.
- **Key insight:** `AddSpringApp`/`AddJavaApp` always auto-registers a named HTTP endpoint (`JavaAppContainerResource.HttpEndpointName`). Never chain `.WithHttpEndpoint()` after `AddSpringApp()` for port configuration — use `JavaAppContainerResourceOptions.Port` and `.TargetPort` instead.
- Build: 0 errors, 0 warnings.

 Team update (2026-02-17): Never chain .WithHttpEndpoint() after AddSpringApp() / AddJavaApp()  these methods auto-register named HTTP endpoints via JavaAppContainerResourceOptions.Port and TargetPort. Adding .WithHttpEndpoint() creates duplicate endpoints, causing runtime allocation errors. Configure host-side port via options instead.  decided by Shuri

 Team update (2026-02-18): AnvilRegionReader placed in integration test project (#93)  Uses fNbt 1.0.0, custom MCA binary I/O, returns BlockState records. Unblocks #94 (WorldSaveDirectory fixture) and enables block verification tests without RCON.  decided by Rocket

