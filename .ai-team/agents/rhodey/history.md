# Project Context

- **Owner:** Jeffrey T. Fritz (csharpfritz@users.noreply.github.com)
- **Project:** Aspire.Hosting.Minecraft — .NET Aspire integration for Minecraft servers
- **Stack:** C#, .NET 10, Docker, Aspire, OpenTelemetry, Minecraft Paper Server, RCON
- **Created:** 2026-02-10

## Key Facts

- Three NuGet packages: Aspire.Hosting.Minecraft (hosting lib), Aspire.Hosting.Minecraft.Rcon (RCON client), Aspire.Hosting.Minecraft.Worker (in-world display)
- Current version: 0.5.0 (v0.5.0-dev in CI)
- Directory.Build.props sets shared NuGet metadata (author, license, repo URL, README)
- Uses itzg/minecraft-server Docker image with Paper server
- RCON for server communication, BlueMap for 3D web maps, DecentHolograms for in-world display
- OpenTelemetry Java agent injected for JVM metrics
- 35 public extension methods, 5 public types, 434 unit tests

## Recent Summary (Milestones 1-5)

**v0.1.0-v0.5.0 evolution:** Started with Sprint 1 NuGet hardening (pinned deps, SourceLink, deterministic builds) and 5 feature toggles (particles, titles, weather, boss bar, sounds). Sprint 2 added 5 more features (action bar ticker, beacon towers, fireworks, guardian mobs, deployment fanfare) with consistent API pattern. Sprint 3 implemented Resource Village (4 themed structures), village fence, 3 new features (heartbeat, achievements, redstone dependency graph), and service switches. Sprint 4 focused on DX polish (WithAllFeatures convenience), dashboard wall visualization (health history tracker with `/clone` scrolling), enhanced building designs (database cylinders, Azure-themed cottages), language-based color coding for wool trim/banners, and easter egg horses. Milestone 5 (Grand Village) redesigned buildings to 15×15 footprint with 3 interior floors, added RCON burst mode for faster construction (40 cmd/s), implemented ornate watchtower exterior (medieval castle aesthetic), and set up minecart rail network foundation.

**Key learnings:** RCON dedup throttle (250ms) requires unique strings to avoid duplicate command drops. `/fill ... hollow` is most efficient wall building. Redstone signal propagation unreliable on Paper — use self-luminous blocks (glowstone/sea_lantern). Terrain detection via binary search with `setblock` probe. Single tall hollow fill + floor fills more efficient than per-floor sections. Grand Village requires 15×15 buildings (11×11 interior) with 24-block spacing to avoid world border issues.

**Architecture decisions:** Separate Azure NuGet package (no Azure.ResourceManager in main). Polling (30s) beats Event Grid for developer experience. All feature toggles use consistent env var pattern (`ASPIRE_FEATURE_*`). Dedicated RCON rate limiter (10 cmd/s standard, burst mode 40 cmd/s). BlueMap integration testing uses RCON block verification + shared Aspire test fixture. Anonymous horseSpawner provides easter egg discovery.

### 2026-02-10: API Surface Freeze & Demo Review for v0.2.0

- **API surface is frozen at 31 public methods** across `MinecraftServerBuilderExtensions`, plus 4 public types (`MinecraftServerResource`, `ServerProperty`, `MinecraftGameMode`, `MinecraftDifficulty`) and 6 RCON types. Full listing committed to `docs/api-surface.md`.
- **All 13 feature methods (5 Sprint 1 + 5 Sprint 2 + 3 Sprint 3) follow identical patterns:** same signature shape, guard clause, env var naming, fluent return, XML docs. `WithRedstoneDependencyGraph` (Sprint 3, in progress by Rocket) also follows the pattern.
- **No internal type leakage detected.** `WorkerBuilder`, `MonitoredResourceNames`, annotations, and all Worker service types are properly `internal`.
- **XML documentation is complete** on all public types and methods — no gaps found.
- **Demo AppHost cleaned up:** features now grouped by sprint with clear comment headers. `WithWorldBorderPulse` moved from Sprint 2 group to Sprint 3 where it belongs. Server config, integrations, and monitored resources each have their own section.
- **README updated** with 3 missing Sprint 3 features (World Border Pulse, Heartbeat, Achievements) in both the feature list and the code sample.
- **Build passes:** 0 errors, 1 pre-existing warning (CS8604 nullable in MinecraftServerResource).

📌 Team update (2026-02-10, consolidated): API surface frozen for v0.2.0 (Rhodey). Azure SDK separate package recommended (Shuri). Sprint branches with PRs directive (Jeff).

### 2026-02-10: Core Architecture Documentation

- **docs/architecture-diagram.md created** — comprehensive visual documentation of the Minecraft coordinate system, village layout, and structure placement. Includes Y-level breakdown (Y=-64 to Y=-59), 2×N grid layout with exact coordinate calculations, structure footprint visualization, fence perimeter math, and 4-resource village example. Uses ASCII art for accessibility. This is the reference for contributors learning the coordinate math.
- **docs/minecraft-constraints.md created** — authoritative source of truth for all Minecraft building rules. Documents Y-level conventions (BaseY=-60 as grass surface), structure size limits (7×7 footprint, heights: watchtower 10 blocks, warehouse/cottage 5 blocks, workshop 7 blocks), RCON rate limits (10 cmd/sec, 250ms throttle, high-priority bypass), Z-coordinate conventions (hollow vs solid structure front walls), path placement (BaseY-1 with grass cleared), fence placement (BaseY with 4-block clearance), and maximum resource limits (~45 resources before world border issues at 256 blocks).
- **Key architectural insights captured**: RCON throughput is the hidden bottleneck for large deployments (50 resources = ~75 seconds initial build at 10 cmd/sec). World border at 256 blocks limits village to ~45 resources safely. Azure RG integration will need MaxResources cap and resource type filtering.
- **Documentation placement rationale**: Both files in docs/ root for high visibility. architecture-diagram.md is educational (helps contributors understand), minecraft-constraints.md is prescriptive (enforces consistency). Together they form complete reference for world-building features.
 Team update (2026-02-11): All sprints must include README and user documentation updates to be considered complete  decided by Jeffrey T. Fritz
 Team update (2026-02-11): All plans must be tracked as GitHub issues and milestones; each sprint is a milestone  decided by Jeffrey T. Fritz
� Team update (2026-02-11): CI pipelines now skip docs-only changes (docs/**, user-docs/**, *.md, .ai-team/**)  decided by Wong
 Team update (2026-02-11): 14 user-facing docs now live in user-docs/ with consistent structure (What  How  What You'll See  Use Cases  Code Example)  decided by Vision

### 2026-02-11: Sprint 4 Planning Analysis

- **Project maturity assessment:** v0.3.1 released. 31 extension methods, 361 tests, 24 worker services, 14 user-docs, full CI/CD (build + release + CodeQL). API surface frozen since v0.2.0. README is comprehensive. The project is past "proof of concept" and entering "polish for adoption" territory.
- **Tech debt identified:** (1) `WithAllFeatures()` convenience method still missing (recommended since Sprint 2 API review). (2) Feature env var checks use `!string.IsNullOrEmpty` instead of `== "true"` (Sprint 2 recommendation #4). (3) No `IRconCommandSender` interface for testability (Sprint 2 recommendation #3). (4) User-docs missing coverage for 5 features: Weather Effects, Particle Effects, Sound Effects, Fireworks, Deployment Fanfare. (5) SourceLink floating version `8.*` in Directory.Build.props not pinned.
- **Azure epic status:** Architecture fully designed, SDK research done, visual design complete. Ready for Phase 1 implementation. Separate NuGet package (`Fritz.Aspire.Hosting.Minecraft.Azure`) is the agreed approach. NOT ready for Sprint 4 — it's a multi-sprint epic marked for v1.0/Sprint 5+.
- **Feature gaps for new users:** No `WithAllFeatures()` convenience (13 `With*()` calls is intimidating). No interactive "getting started" experience. No NuGet package icon. No GitHub Pages documentation site.
- **Only 1 open GitHub issue:** #48 (Pre-baked Docker image — v1.0/Sprint 5+).
- **Key observation:** The project has shipped 3 sprints of features. Sprint 4 should focus on DX polish, missing docs, convenience APIs, and making the NuGet package irresistible to first-time users — not new Minecraft features.

### 2026-02-12: Sprint 4 Brainstorming — Aspire Observability Visualization Ideas

- **Jeff's request:** "What would be fun to browse and wander around in Minecraft? Some way to visualize traces?" — Jeff is looking to go beyond resource health into OpenTelemetry observability data (traces, metrics, logs).
- **10 ideas brainstormed**, ranging from Easy to Hard feasibility. Full write-up in `.ai-team/decisions/inbox/rhodey-sprint4-visualization-ideas.md`.
- **Top 3 for Sprint 4 (no new data source needed):** Dragon Health Egg (SLO visualization with Ender Dragon theming), Redstone Clock Dashboard (time-series health display), Sculk Error Network (cascading failure visualization using Deep Dark blocks).
- **Jeff's trace interest → Trace River is the headline feature for Sprint 5.** Water channels between buildings with boats representing requests, lava for errors. Requires OTLP trace ingestion — a new architectural capability the worker doesn't have today.
- **Critical architectural decision identified:** All OTLP-dependent features (7 of 10 ideas) require the worker to consume trace/metric/log data. Today it only polls health endpoints. Must design the OTLP ingestion architecture before committing to any individual feature. Three options documented: secondary OTLP receiver, Aspire dashboard API polling, or shared collector.
- **RCON budget is the hidden constraint** for continuous visualization features. Metrics Tower and Minecart Rails have high RCON costs. Dragon Egg and Sculk Network are RCON-efficient. This should influence prioritization.

### 2026-02-12: Sprint 4 Technical Design Decisions

- **Full technical design created** at `docs/designs/sprint-4-design.md` covering Redstone Dashboard Wall, Enhanced Building Architecture, and Sprint 4 scope with 14 issues.
- **Redstone Dashboard Wall** placed at X=-5, west of village, facing east. Uses `/clone` shift-register technique for RCON-efficient scrolling (N+1 commands per cycle instead of N×columns). Health history stored in a `HealthHistoryTracker` ring buffer. Concrete bar charts below show uptime percentages.
- **Jeff's enhanced building vision implemented:** Database resources (postgres, redis, sqlserver, etc.) get cylindrical buildings using 3-block radius in the 7×7 grid with polished deepslate. Azure resources get light_blue_banner flags on rooftops. Azure detection via resource type string matching (no SDK dependency). New `AzureThemed` structure type is a Cottage variant with light_blue_concrete walls.
- **Building type mapping expanded:** Project→Watchtower, Container→Warehouse, Executable→Workshop, Database→Cylinder, Azure→AzureThemed, Unknown→Cottage. Azure banner is additive — a database cylinder from Azure gets both the cylinder shape AND the azure banner.
- **Sprint 4 scoped to 14 issues:** 4 dashboard, 3 enhanced buildings, 1 Dragon Egg, 3 DX polish (WithAllFeatures, env var tightening, welcome teleport), 3 documentation (README, user-docs, tests). Cut line: welcome teleport drops first, then Dragon Egg.
- **Key design principle:** Azure detection is a visual signal (banner), not an architectural integration. String matching on resource types keeps the main package free of Azure SDK dependencies, consistent with the separate-package decision from Sprint 2.
- **Cylinder RCON cost is ~88 commands** (4× a watchtower) due to circular geometry. Acceptable as one-time build; databases are typically <30% of resources.
- **Decisions logged** in `.ai-team/decisions/inbox/rhodey-sprint4-design.md` (7 decisions).
- **Decisions logged** in `.ai-team/decisions/inbox/rhodey-sprint4-design.md` (7 decisions).

### 2026-02-12: BlueMap Integration Testing Strategy Design

- **BlueMap has no block-level REST API.** Its web server serves pre-rendered tile files (geometry JSON and PNGs). There is no endpoint to query "what block is at X,Y,Z?" — only a Java API for server-side plugins. This rules out BlueMap REST as a primary test verification method.
- **RCON `execute if block` is the right primary approach.** The command checks if a specific block type exists at exact coordinates and returns empty string on match. It's immediate (no render delay), deterministic, and uses our existing `RconClient` library. Combined with `VillageLayout` constants, we can assert exact block placement.
- **Playwright/BlueMap screenshots are secondary, not primary.** 3D rendering is non-deterministic (lighting, camera angle, anti-aliasing). BlueMap needs 30–60s to render after blocks are placed. Screenshot comparison requires reference images and tolerance tuning. Good for visual regression but not for correctness assertions.
- **Shared fixture is mandatory for test performance.** Minecraft server takes 45–60s to start. A single `MinecraftAppFixture` using `DistributedApplicationTestingBuilder` starts the AppHost once per test run. Tests share the fixture via xUnit `[CollectionFixture]`.
- **Poll-based readiness beats fixed delays.** The fixture polls `execute if block` on a known coordinate (first structure corner) every 5s until the village is confirmed built. This adapts to variable server startup times instead of hoping a `Task.Delay(90000)` is enough.
- **Integration tests belong in a separate project with Linux-only CI.** They're slow (2–3 min), require Docker, and should not block PR CI. Run as a gated job after unit tests pass, on `main` and release branches only.
- **Design document written** at `docs/designs/bluemap-integration-tests.md` with architecture, sample code, first 5 tests, CI considerations, and risk analysis.

📌 Team update (2026-02-12): BlueMap integration testing strategy designed — hybrid RCON verification + BlueMap smoke tests, shared Aspire test fixture, Linux-only CI job — decided by Rhodey

### 2026-02-12: Sprint 5 "Grand Village" Architecture Design

- **Full technical design created** at `docs/designs/sprint-5-design.md` covering three pillars: Walk-in Buildings (15×15), Ornate Project Towers (20 blocks tall, 3 floors), and Minecart Rail Network (powered rails between dependent resources).
- **VillageLayout constants become properties.** `Spacing`, `StructureSize`, `FenceClearance` change from `const` to `static { get; private set; }` with a `ConfigureGrandLayout()` method. Backward compatible — default values match Sprint 4. This is the least-disruptive approach; all existing services adapt automatically through `VillageLayout.GetStructureOrigin()`.
- **15×15 is the sweet spot for structure size.** 11×11 (9×9 interior) was too cramped for meaningful multi-floor buildings with staircases. 21×21 would balloon RCON costs (>200 commands per watchtower) and exceed world border with 4 resources. 15×15 gives 13×13 usable interior — room for spiral staircases, furniture, multiple floors.
- **Spacing 24 = building 15 + gap 9 (walking + rail corridor).** This doubles village footprint per row, requiring `MAX_WORLD_SIZE` bump from 256 to 512. Supports ~20 resources comfortably.
- **RCON burst mode is critical for Grand Village.** 600 commands at 10 cmd/sec = 60 seconds. With burst mode at 40 cmd/sec = 15 seconds. The Minecraft server handles 40 `/setblock`+`/fill` per second in bursts since each command completes in <1ms.
- **Rails coexist with redstone, not replace.** `WithMinecartRails()` runs alongside `WithRedstoneDependencyGraph()` with 1-block X offset. Both visual systems provide different information: redstone shows health reactivity, rails show physical connection.
- **Grand Village is opt-in** via `WithGrandVillage()`. Standard 7×7 layout remains the default. No breaking changes for existing consumers.
- **15 GitHub issues created** (#76-90) covering: layout foundation, extension methods, 6 building redesigns, rail service, burst mode, fence/paths/forceload, service adaptation, tests, documentation, and release prep.
- **7 architectural decisions logged** in `.ai-team/decisions/inbox/rhodey-sprint5-grand-village.md`.
- **Key risk: Grand Silo (radius 7 cylinder) at ~130 commands** is the most expensive building due to circular geometry not being `/fill`-friendly. Octagonal approximation may reduce by ~30%.
- **Phased plan:** Phase 1 Layout (Shuri), Phase 2 Buildings (Rocket, 3 parallel tracks), Phase 3 Rails (Rocket), Phase 4 Tests/Docs (Nebula/Rhodey), Phase 5 Polish (Rhodey). ~2 weeks total with parallel execution.

📌 Team update (2026-02-12): Sprint 5 "Grand Village" architecture designed — 15×15 walkable buildings, multi-story watchtowers, minecart rail network, RCON burst mode — 15 issues created (#76-90) — decided by Rhodey

### Upcoming Sprint 5 Planning

- **Three pillars requested by Jeff:** (1) Larger walkable buildings — scale up to 20+ blocks, navigable interiors. (2) Ornate project towers — themed materials by technology stack. (3) Minecart rail network — connects dependent resources, visual build order representation.
- **Technical feasibility assessed:** Language color coding logic is foundation for ornate towers. Pathfinding via `execute` commands feasible. No hard blockers identified.
- **Out of scope for Sprint 4:** All three pillars are multi-sprint features for v0.4/Sprint 5+.

### 2026-02-12: Famous Buildings Feature — API Design

- **New API pattern: extension method on monitored resources, not on Minecraft server.** `AsMinecraftFamousBuilding()` extends `IResourceBuilder<T> where T : IResource`, using `FamousBuildingAnnotation` to store the selection. This is the first extension method in the project that targets arbitrary Aspire resources rather than `MinecraftServerResource`. The annotation-based approach with deferred env var callback guarantees call-order independence.
- **Data flow pattern for resource metadata:** AppHost annotation → `WithMonitoredResource` reads annotation → sets `ASPIRE_RESOURCE_{NAME}_FAMOUS_BUILDING` env var on worker → `AspireResourceMonitor.DiscoverResources()` reads it → `StructureBuilder` checks before auto-detection. This extends the existing `_TYPE`/`_URL`/`_HOST`/`_PORT`/`_DEPENDS_ON` env var convention.
- **Building models are pure C#, one file per building.** Located in `src/Aspire.Hosting.Minecraft.Worker/Services/FamousBuildingModels/`. Implements `IFamousBuildingModel` interface with `Width`, `Depth`, `Height`, and `BuildAsync()`. Shared geometry helpers in `BuildingHelpers.cs`.
- **FamousBuilding enum has 15 members** spanning 6 continents. All constrained to 15×15 footprint, 200 RCON command cap. Requires `WithGrandVillage()` — falls back to auto-detection on 7×7 grid.
- **Two-sprint phasing:** Sprint A = API + infrastructure + 3 starter models (Pyramid, Castle, Lighthouse). Sprint B = remaining 12 models. Depends on Sprint 5 Grand Village layout landing first.
- **Design document:** `docs/designs/famous-buildings-design.md`. Decision log: `.ai-team/decisions/inbox/rhodey-famous-buildings-design.md`.
- **Key files:** `FamousBuilding.cs` (enum), `FamousBuildingAnnotation.cs` (annotation), `FamousBuildingExtensions.cs` (extension method) — all in `src/Aspire.Hosting.Minecraft/`.

### 2026-02-12: MonitorAllResources Convenience API — Architecture Design

- **Eager discovery over deferred eventing.** `MonitorAllResources()` iterates `builder.ApplicationBuilder.Resources` at call time (Option A) rather than subscribing to `BeforeStartEvent` (Option B). Rationale: consistency with existing `WithMonitoredResource` (which is eager), predictability for debugging, and avoidance of builder-state risks during Aspire's `BeforeStartEvent`. The constraint that resources must exist before the call is naturally satisfied by AppHost coding patterns.
- **Structural exclusion, not name-based.** Minecraft infrastructure (server, worker, children) is excluded via object identity (`ReferenceEquals`) and `IResourceWithParent` graph traversal. This automatically covers BlueMap sidecars and any future infrastructure without maintaining name allowlists.
- **ExcludeFromMonitoring ships alongside.** Annotation-based opt-out via `ExcludeFromMonitoringAnnotation` — trivial to implement (1 annotation class + 1 extension method + 1 line in exclusion check) and completes the user story.
- **Naming: `MonitorAllResources()` not `WithAllMonitoredResources()`.** Breaks the `With*` convention intentionally — it's a convenience aggregate, not a feature toggle. The verb phrase reads naturally and distinguishes it from the per-resource method.
- **Duplicate prevention is bidirectional.** `MonitoredResourceNames.Contains()` check prevents duplicates when manual calls precede `MonitorAllResources()`. Calls after are additive and safe (env vars are idempotent).
- **Design document:** `docs/designs/monitor-all-resources-design.md`. Decision: `.ai-team/decisions/inbox/rhodey-monitor-all-resources.md`.
 Team update (2026-02-12): Sprint 5 Grand Village architecture designed  VillageLayout constants become mutable properties, structure size increases to 1515 (1313 usable interior), spacing becomes 24 blocks (15 + 9 gap for rails/paths), MAX_WORLD_SIZE increases to 512, RCON burst mode (1040 cmd/s during build), minecart rails coexist with redstone wires, opt-in via WithGrandVillage()  decided by Rhodey
 Team update (2026-02-12): Famous Buildings API designed  AsMinecraftFamousBuilding(FamousBuilding enum), 15 iconic buildings (geographic diversity), pure C# build models, annotation-based with env var flow, requires WithGrandVillage(), 200 RCON command max per building, two-sprint phasing (3 buildings in Sprint A, 12 remaining in Sprint B)  decided by Rhodey
 Team update (2026-02-12): MonitorAllResources convenience API design approved  .MonitorAllResources() extension auto-discovers all non-Minecraft resources, ExcludeFromMonitoring() opt-out, eliminates manual WithMonitoredResource() calls, eager discovery, Famous Building annotations pass through  decided by Rhodey
 Team update (2026-02-12): Aspire observability visualization ideas documented (10 ideas: Trace River, Enchanting Tower, Log Campfires, Nether Portal Gateway, Sculk Sensor Network, Minecart Rails, Villager Trading Hall, Redstone Clock Dashboard, Ender Chest Trace Explorer, Dragon Health Egg)  Dragon Egg + Redstone Clock + Sculk recommended for Sprint 4; Trace River + Log Campfires + Nether Portal for Sprint 5 (requires OTLP architecture investment)  decided by Rhodey
📌 Team update (2026-02-12): Terminology directive — use "milestones" instead of "sprints" going forward for all planning documents and discussions — decided by Jeffrey T. Fritz

### 2026-02-13: v0.5.0 API Review & Release Verification

- **API surface is clean.** 35 public extension methods, 5 public types, 0 internal type leakage. `WithGrandVillage()` and `WithMinecartRails()` follow the established guard clause pattern (WorkerBuilder null check → env var set → fluent return). Both included in `WithAllFeatures()`. XML docs complete on all public members.
- **Build passes:** 0 errors. 1 pre-existing CS8604 warning (nullable in `MinecraftServerResource.ConnectionStringExpression`). 1 pre-existing xUnit1026 warning (unused parameter in `VillageLayoutTests`).
- **434 unit tests pass:** 45 Rcon + 19 Hosting + 370 Worker. 0 unit test failures.

📌 Team update (2026-02-15): Structural validation requirements — all acceptance tests must verify door accessibility, staircase connectivity, and wall-mounted items. Session milestone plan created, GitHub issues #93, #94, #95 generated. — decided by Jeff (Jeffrey T. Fritz)

📌 Team update (2026-02-15): MCA Inspector milestone launched — read-only Minecraft Anvil format (NBT) library for bulk structural verification without RCON latency. Complements RCON testing. 4 phases: (1) Library foundation (3-4 days), (2) Test infrastructure (2 days), (3) Watchtower bulk verification (3 days), (4) Polish & release (1 day). Timeline: ~1.5 weeks. Success: AnvilRegionReader reads block state from real .mca files, 20+ unit tests (>90% coverage), integration tests verify 200+ block coordinates per building. — decided by Rhodey
- **5 integration test failures are expected** — they require a running Minecraft Docker container. The `--filter "Category!=Integration"` doesn't exclude them because integration tests are missing `[Trait("Category", "Integration")]` — they use `[Collection("Minecraft")]` instead. Non-blocking, filed as observation.
- **NuGet package created:** `Fritz.Aspire.Hosting.Minecraft.0.1.0-dev.nupkg` (~39.6 MB). Package validation passed. Version set to `0.5.0` at release time via CI pipeline `-p:Version` override.
- **Three non-blocking observations for future work:** (1) Add `[Trait("Category", "Integration")]` to integration tests. (2) Fix CS8604 nullable warning. (3) Confirm CI sets correct version from git tag.
- **Release decision:** APPROVED — written to `.ai-team/decisions/inbox/rhodey-v050-release-ready.md`.

📌 Team update (2026-02-13): v0.5.0 release readiness APPROVED — 35 public methods, 434 tests pass, build clean, package verified, 3 non-blocking observations documented — decided by Rhodey

📌 Team update (2026-02-15): Grand Watchtower exterior redesigned with ornate medieval aesthetics (deepslate buttresses, iron bar arrow slits, taller turrets with pinnacles, portcullis gatehouse, string courses) — stays under 100 RCON command budget — decided by Rocket

### 2026-02-15: MCA Inspector Milestone Planned

- **Milestone document created** at `.ai-team/decisions/inbox/rhodey-mca-inspector-milestone.md` — comprehensive plan for reading Minecraft Anvil region files directly to verify block state.
- **Goal:** Bypass RCON for bulk structure verification. Today's tests make 225+ RCON calls to verify one 15×15 watchtower; MCA inspector will query all blocks from disk in <1 second.
- **Architecture:** New optional NuGet package `Aspire.Hosting.Minecraft.Anvil` (separate from main, keeps main free of NBT dependencies). AnvilRegionReader class provides `GetBlockAt(x, y, z)` API.
- **Four phases planned:** (1) Library + NBT selection (~4 days), (2) Test fixture integration (~2 days), (3) Bulk verification tests (~3 days), (4) Polish + docs (~1 day). Total ~1.5 weeks with 1 FTE.
- **Why separate package?** Other Aspire consumers might want to audit Minecraft world saves independently. Cleaner separation of concerns.
- **Why MCA over RCON?** RCON has ~50ms latency per call; 200 blocks = 10+ seconds. MCA on same filesystem ≈ <1 second. Trade-off: MCA is offline-only; RCON verifies real-time behavior.
- **Why not BlueMap REST?** BlueMap serves rendered tiles, not raw block data. No block-level query API.
- **Why not NBT parsing in Worker?** Worker is for gameplay. Parsing is a test concern. Separation keeps logic clean.
- **GitHub issues created:** #92 (NBT library spike), #93 (AnvilRegionReader), #94 (MinecraftAppFixture integration).

### 2026-02-16: Minecart Representation Brainstorm

- **MinecartRailService foundation:** Places L-shaped powered rail networks between dependent resources, spawns chest minecarts at start, health-reactive disable/restore of powered rails. Already handles station placement (detector+powered+detector sequence), rail positioning math, and connection state tracking.
- **Six minecart concept ideas evaluated:** (1) HTTP Request Flows (spawn minecarts per request), (2) Health Check Polling (round-trip cycle), (3) OTLP Trace Propagation (trace as minecart chain), (4) Log Message Flow (log level + color), (5) Startup Sequence (dependency propagation at boot), (6) Queue Depth Visualization (consumer/enqueue rate mismatch).
- **Recommendation: HTTP Request Flows (#1).** Immediate feasibility (service spawns minecarts per active request, ~5 max per rail for visual clarity), real diagnostic value (visualizes actual runtime request traffic, not just infrastructure), conference demo narrative (visible cause/effect: API call → minecart move → service degradation → minecart stall), no new data source required (leverages existing health checks), scales cleanly to 20+ resources.
- **Secondary pick: Startup Sequence (#5).** Low RCON cost (one-time event), visually memorable, fits existing dependency ordering logic, could ship as phase 2 if request flows is ambitious.
- **Reject OTLP Traces (#3) for now.** Dream feature but requires Sprint 5 OTLP ingestion architecture to land first. Put on v1.0 roadmap.
- **Feasibility analysis:** Request flow = Medium risk (minecart spawning + hitbox counting). Health checks = Hard (pathfinding). Queue depth = Medium (metric polling + spawn/despawn logic). All others = Hard or Very Hard (new data sources or complex routing).
- **Key MinecartRailService files:** `src/Aspire.Hosting.Minecraft.Worker/Services/MinecartRailService.cs` (L-shaped path calculation, station placement, health-reactive rail disable). `VillageLayout.cs` (structure positioning, dependency ordering). `AspireResourceMonitor.cs` (health polling source). Extension method: `WithMinecartRails()` in main package.

### 2026-02-17: Village Redesign Architecture — Canals, Tracks, Docker Image

- **Comprehensive architecture proposal written** at `.ai-team/decisions/inbox/rhodey-village-redesign-architecture.md` covering Jeff's village redesign vision: custom Docker image, wider village spacing, canal system, error boats, and track/canal bridge interactions.
- **6-phase implementation plan:** (1) Layout expansion (spacing 24→36), (2) Custom Docker image (parallel), (3) Canal system, (4) Error boats, (5) Track/canal bridges, (6) Tests/docs. ~3 weeks with parallel execution.
- **VillageLayout.Spacing increases to 36 for Grand layout.** 15 (building) + 21 (corridor: 3 path + 3 rail + 5 canal + 10 buffer). MAX_WORLD_SIZE increases to 768.
- **Canal architecture: blue ice floor + water layer.** Blue ice gives boats 72.73 blocks/sec speed without needing slope engineering. 3-block-wide channels, 2 blocks deep, stone_brick walls. Branch canals per building merge into trunk canal feeding a shared lake at Z-max + 20.
- **Error boat lifecycle uses 3-layer anti-pileup:** (1) Location-based despawn in lake zone every update cycle, (2) Per-resource cap of 3 active boats, (3) Global cap of 20 boats + 5-second spawn cooldown. Boats spawned via `summon` with `Motion` tag for initial velocity, creeper passengers have `NoAI:1b` to prevent explosion.
- **Bridge design for track/canal crossings:** `stone_brick_slab` deck at SurfaceY + 1 spanning canal width + 2. Rails on top, water underneath. CanalService exposes canal positions as HashSet for O(1) bridge detection.
- **Docker image strategy:** Extend `itzg/minecraft-server:latest` with pre-baked BlueMap, DecentHolograms, OTEL agent. Publish to `ghcr.io`. Keep MODRINTH_PROJECTS as fallback. Marker env var `ASPIRE_PREBAKED` for extension method detection.
- **6 open questions for Jeff:** Canal floor material (blue ice vs stone), error trigger (health vs OTLP logs), boat wood type, Docker registry, canal wall material, lake decorations.
- **Key files impacted:** `VillageLayout.cs` (spacing + new methods), `MinecartRailService.cs` (bridge detection), `MinecraftServerBuilderExtensions.cs` (new extension methods + Docker image), `MinecraftWorldWorker.cs` (new service wiring).
- **New files needed:** `CanalService.cs`, `ErrorBoatService.cs`, `LakeBuilder.cs`, `docker/Dockerfile`, `.github/workflows/docker.yml`.

📌 Team update (2026-02-17): Village redesign architecture proposed — 6-phase plan covering canals, error boats, Docker image, track/canal bridges, spacing increase to 36 blocks — decided by Rhodey

## Learnings

### MinecartRailService Architecture Insights

1. **Minecart rails are health-reactive but static.** Current design builds rails once, then toggles powered rails on/off based on parent health. Minecarts (if spawned) would inherit this reactive behavior automatically — they stop when powered rails are disabled, resume when restored. This is the hook for request flow visualization: spawn minecarts per request, let the health-reactive rail system do the "stalling on degradation" part naturally.

2. **L-shaped path geometry is optimal for the village grid.** Rails travel X-axis first, then Z-axis. This avoids corners that would require sloped rail blocks (not implemented) and keeps minecarts aligned to grid. Powered rails every 8 blocks is the refresh rate (minecart speed in Minecraft).

3. **Station design (detector + powered + detector) is the integration point.** Detector rails trigger redstone at endpoints. If we want to count minecarts arriving at a station or measure request completion, we hook into the detector rail's redstone output. This is the path to "request latency visualization" later.

4. **RCON cost scales with rail length, not minecart count.** Building a 50-block rail = ~50 setblock commands (one-time cost). Spawning 5 minecarts = 5 summon commands. The visual density problem isn't RCON; it's entity performance. Paper can handle ~50 minecarts server-wide before TPS drops, so limiting to 5 per rail and ~20 total active is safe.

5. **Minecart respawning logic is trivial but needs debounce.** Spawn on demand (when request count increases), despawn when not needed (request count decreases). Use a `RequestCountTracker` polling the health endpoints every 5s, comparing desired count to actual minecarts via `execute as @e[type=chest_minecart]` count query. Avoid rapid spawn/despawn thrashing with 10s hysteresis.

### Canal & Error Boat Architecture Insights

1. **Blue ice is the key enabler for autonomous boat movement.** Boats on blue ice travel at 72.73 blocks/sec without player input. Combined with a `Motion` NBT tag on summon, boats self-propel from building to lake. No slope engineering, no water current blocks, no redstone. This is dramatically simpler than flowing water canals.

2. **Entity lifecycle is the hardest part of error boats.** Minecraft boats don't have an Age tag, so you can't use vanilla despawn timers. Location-based despawn (`kill @e[type=boat,distance=..N]` near lake) is the most reliable approach. Must run every update cycle to prevent accumulation.

3. **Per-resource spawn throttling prevents visual noise.** Without caps, a flapping health check could spawn dozens of boats per minute. Three-layer defense: 5s cooldown per resource, 3 max per resource, 20 max globally.

4. **Canal RCON cost is trivially low.** `/fill` for the channel walls + floor + water = ~3 commands per canal. For 10 resources: ~45 total commands. Compare to a single Grand Watchtower at ~100 commands. Canals are the cheapest major feature we've proposed.

5. **Bridge detection must be cooperative.** `MinecartRailService` and `CanalService` must share coordinate data. CanalService should build first (Phase 3) and expose canal positions. MinecartRailService (already Phase 1 feature) then checks for crossings during rail placement. Service initialization order in `MinecraftWorldWorker` must reflect this dependency.

### MCA Inspector Architecture Decisions

1. **Separate package is the right call.** By making Anvil optional, we:
   - Keep the main package free of NBT library dependencies (lighter for non-testing consumers)
   - Allow version independence (Anvil can patch independently)
   - Enable other Aspire consumers to adopt the library without taking the full Minecraft hosting SDK
   - Match the precedent of `Aspire.Hosting.Minecraft.Azure` being separate

2. **Bulk verification is what MCA enables that RCON can't.** A single `GetBlockAt()` call on disk is ~1ms; RCON calls are ~50ms. When you need to verify 200+ blocks (entire structure geometry), the difference is material: 200ms (MCA) vs. 10s (RCON). But for single-point verification (did this command work?), RCON is fine. Tests will use both.

3. **NBT parsing is commodity work; library selection matters.** fNbt is the obvious choice (MIT, widely used, documented), but the spike (Issue #92) is essential. We could find performance surprises or licensing issues. 2–3 days of prototyping saves regret later.

4. **World save directory exposure is a small but critical fixture change.** The MinecraftAppFixture already mounts the world directory; we just need to expose the path. This unblocks all Phase 2+ work.

5. **Coordinate system complexity is the risky part.** Anvil format uses: world coords (x, y, z) → region coords (rx, rz) → chunk coords (cx, cz) → section index (y/16) → block index (x%16, y%16, z%16). We'll need careful unit tests with known test data (.mca files with pre-placed blocks) to verify the math.

### Milestone Planning Approach

1. **Four-phase sequencing with clear blockers.** NBT selection (Phase 1.1) is a hard blocker for implementation. MinecraftAppFixture world path (Phase 2.1) is a hard blocker for integration tests. By identifying blockers upfront, we unblock parallel work (Phases 1 & 2 can overlap once 1.1 is done).

2. **Performance baseline is mandatory.** We're proposing MCA *because* it's faster than RCON. If we ship without proving it, the claim is unvalidated. Phase 3.3 (performance doc) is not optional.

3. **Complementary, not competitive.** The milestone plan explicitly frames MCA as a complement to RCON, not a replacement. This is important for adoption: teams will use both methods in the same test suite, each for what it's best at.

4. **Risk mitigation table is underrated.** The milestone includes a risk table; this forces us to think about what could go wrong (unmaintained library, parsing errors, format changes) and what we'll do about each. Makes sprint planning more realistic.
