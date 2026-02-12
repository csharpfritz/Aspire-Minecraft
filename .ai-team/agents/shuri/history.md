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